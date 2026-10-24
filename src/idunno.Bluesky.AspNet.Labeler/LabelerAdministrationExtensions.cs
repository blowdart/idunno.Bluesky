// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Installs opt-in administration adapters without changing public label distribution.</summary>
public static class LabelerAdministrationExtensions
{
    private static readonly TimeSpan s_defaultMaximumServiceTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly object s_transportEvidenceKey = new();

    /// <summary>Configures forwarded HTTPS metadata from explicitly trusted reverse proxies.</summary>
    /// <param name="application">The application pipeline.</param>
    /// <param name="configure">The trusted proxy and forwarding options.</param>
    /// <returns>The application pipeline.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="application"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No trusted proxy or network is configured, or paired forwarded protocol/address handling is disabled.</exception>
    /// <remarks><para>Call this before mapping endpoints. The middleware processes paired <c>X-Forwarded-For</c>
    /// and <c>X-Forwarded-Proto</c> entries with header symmetry required. It records trusted HTTPS evidence
    /// only when the connection is encrypted or a configured proxy transformed the request scheme. Add every
    /// proxy hop to the trusted
    /// proxy or network lists in <paramref name="configure"/>, and set <see cref="ForwardedHeadersOptions.ForwardLimit"/>
    /// to the number of trusted hops. Each trusted proxy must sanitize forwarded headers from untrusted clients.
    /// The application must not be directly reachable around those proxies.</para></remarks>
    public static IApplicationBuilder UseLabelerAdministrationForwardedHeaders(
        this IApplicationBuilder application,
        Action<ForwardedHeadersOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(configure);

        ForwardedHeadersOptions options = new()
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            RequireHeaderSymmetry = true
        };
        options.KnownProxies.Clear();
#if NET10_0_OR_GREATER
        options.KnownIPNetworks.Clear();
#else
        options.KnownNetworks.Clear();
#endif
        configure(options);
        ForwardedHeaders requiredHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        if ((options.ForwardedHeaders & requiredHeaders) != requiredHeaders || !options.RequireHeaderSymmetry)
        {
            throw new InvalidOperationException("Paired forwarded address and protocol handling with header symmetry must remain enabled.");
        }

        bool hasTrustedNetworks;
#if NET10_0_OR_GREATER
        hasTrustedNetworks = options.KnownIPNetworks.Count > 0;
#else
        hasTrustedNetworks = options.KnownNetworks.Count > 0;
#endif
        if (options.KnownProxies.Count == 0 && !hasTrustedNetworks)
        {
            throw new InvalidOperationException("At least one trusted proxy or network must be configured.");
        }

        application.Use((context, next) =>
        {
            context.Items[s_transportEvidenceKey] = new ForwardedTransportState(
                context.Connection.RemoteIpAddress,
                context.Features.Get<Microsoft.AspNetCore.Connections.Features.ITlsHandshakeFeature>() is not null,
                context.Request.Scheme);
            return next(context);
        });
        application.UseForwardedHeaders(options);
        application.Use(async (context, next) =>
        {
            ForwardedTransportState state = (ForwardedTransportState)context.Items[s_transportEvidenceKey]!;
            bool trustedProxyHttps = !state.DirectTls &&
                state.ImmediatePeer is { } immediatePeer &&
                IsTrustedProxy(immediatePeer, options) &&
                !string.Equals(state.OriginalScheme, "https", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.Scheme, "https", StringComparison.OrdinalIgnoreCase);
            context.Items[s_transportEvidenceKey] = state.DirectTls || trustedProxyHttps;
            await next(context).ConfigureAwait(false);
        });
        return application;
    }

    /// <summary>Checks whether the connection peer belongs to the configured forwarded-header trust set.</summary>
    private static bool IsTrustedProxy(System.Net.IPAddress address, ForwardedHeadersOptions options)
    {
        if (options.KnownProxies.Contains(address))
        {
            return true;
        }

#if NET10_0_OR_GREATER
        return options.KnownIPNetworks.Any(network => network.Contains(address));
#else
        return options.KnownNetworks.Any(network => network.Contains(address));
#endif
    }

    /// <summary>Registers administration with mandatory provider-backed team and issuance boundaries.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="audience">The exact DID service reference for administrative service tokens.</param>
    /// <param name="signingKey">The established caller-owned label signing key.</param>
    /// <param name="httpClient">The caller-owned, hardened identity-resolution client.</param>
    /// <param name="maximumServiceTokenLifetime">The maximum accepted service-token lifetime. The default is 15 minutes.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumServiceTokenLifetime"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Storage, policy or administration registration is missing or ambiguous.</exception>
    /// <remarks><para>Initialize the database and bootstrap explicitly before accepting commands.
    /// Keys and clients are borrowed, not disposed. No permissive authorizer or HTTP bootstrap is installed.</para></remarks>
    public static IServiceCollection AddLabelerAdministration(
        this IServiceCollection services,
        AtProtoServiceReference audience,
        LabelSigningKey signingKey,
        HttpClient httpClient,
        TimeSpan? maximumServiceTokenLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentNullException.ThrowIfNull(httpClient);
        TimeSpan lifetimeLimit = maximumServiceTokenLifetime ?? s_defaultMaximumServiceTokenLifetime;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetimeLimit, TimeSpan.Zero);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(AdministrationDispatcher)) ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelStore)) != 1 ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelAdministrationStore)) != 1 ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelIssuancePolicyProvider)) != 1)
        {
            throw new InvalidOperationException("Register exactly one administrative store and policy, and register administration once.");
        }

        services.AddLogging();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new AdministrationConfiguration(audience, httpClient, lifetimeLimit));
        services.AddSingleton(provider =>
        {
            ILabelStore store = provider.GetRequiredService<ILabelStore>();
            ILabelAdministrationStore administration = provider.GetRequiredService<ILabelAdministrationStore>();
            if (!ReferenceEquals(store, administration))
            {
                throw new InvalidOperationException("Team authorization and label decisions must use the same provider instance.");
            }

            return new AdministrationDispatcher(
                store: store,
                administration: administration,
                httpClient: httpClient,
                signingKey: signingKey,
                policy: provider.GetRequiredService<ILabelIssuancePolicyProvider>(),
                clock: provider.GetRequiredService<TimeProvider>());
        });
        return services;
    }

    /// <summary>Maps HTTPS administration methods using exact-audience service authentication.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="remoteEnabled"><see langword="true"/> to expose remote administration routes at host startup; otherwise, <see langword="false"/>.</param>
    /// <returns>The endpoint route builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    /// <remarks><para>When <paramref name="remoteEnabled"/> is <see langword="false"/>, no remote routes are mapped;
    /// local administration remains available. This is a startup-time setting, so changing it requires a host restart.
    /// Only service JWTs over encrypted HTTPS or a verified trusted-proxy HTTPS chain are accepted. For TLS-terminating
    /// proxies, install
    /// <see cref="UseLabelerAdministrationForwardedHeaders(IApplicationBuilder, Action{ForwardedHeadersOptions})"/>
    /// before mapping endpoints and configure all trusted proxy hops. Browser OAuth cookies and PDS OAuth
    /// tokens do not grant access.</para></remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6667:Logging in a catch clause should pass the caught exception as a parameter",
        Justification = "Untrusted authentication and JSON exceptions may contain sensitive input; log only their category.")]
    public static IEndpointRouteBuilder MapLabelerAdministration(
        this IEndpointRouteBuilder endpoints,
        bool remoteEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (!remoteEnabled)
        {
            return endpoints;
        }

        AdministrationDispatcher dispatcher = endpoints.ServiceProvider.GetRequiredService<AdministrationDispatcher>();
        AdministrationConfiguration configuration = endpoints.ServiceProvider.GetRequiredService<AdministrationConfiguration>();
        ILabelAdministrationStore store = endpoints.ServiceProvider.GetRequiredService<ILabelAdministrationStore>();
        TimeProvider clock = endpoints.ServiceProvider.GetRequiredService<TimeProvider>();
        ILogger logger = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("LabelerAdministration");
        foreach (string method in AdministrationDispatcher.s_getMethods)
        {
            RequestDelegate handler = context => HandleRequestAsync(
                context: context, method: method, isPost: false, dispatcher: dispatcher, configuration: configuration,
                store: store, clock: clock, logger: logger);
            endpoints.MapGet("/xrpc/dev.idunno.labeler.admin." + method, handler).AllowAnonymous();
        }

        foreach (string method in AdministrationDispatcher.s_postMethods)
        {
            RequestDelegate handler = context => HandleRequestAsync(
                context: context, method: method, isPost: true, dispatcher: dispatcher, configuration: configuration,
                store: store, clock: clock, logger: logger);
            endpoints.MapPost("/xrpc/dev.idunno.labeler.admin." + method, handler).AllowAnonymous();
        }

        return endpoints;
    }

    /// <summary>Authenticates one fixed-method route and dispatches its bounded request.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6667:Logging in a catch clause should pass the caught exception as a parameter",
        Justification = "Untrusted authentication and JSON exceptions may contain sensitive input; log only their category.")]
    private static async Task HandleRequestAsync(
        HttpContext context,
        string method,
        bool isPost,
        AdministrationDispatcher dispatcher,
        AdministrationConfiguration configuration,
        ILabelAdministrationStore store,
        TimeProvider clock,
        ILogger logger)
    {
        bool secureTransport = context.Features.Get<Microsoft.AspNetCore.Connections.Features.ITlsHandshakeFeature>() is not null ||
            context.Items.TryGetValue(s_transportEvidenceKey, out object? transportEvidence) &&
            transportEvidence is true;
        if (!secureTransport)
        {
            AdministrationResponse transportResponse = new(false, "HttpsRequired", "HTTPS is required.");
            int transportStatus = StatusCodes.Status403Forbidden;
            LabelServiceResult<LabelAdministrationReceipt> audit = await store.RecordAdministrationDenialAsync(
                reason: "Insecure transport rejected.",
                cancellationToken: context.RequestAborted).ConfigureAwait(false);
            if (!audit.Succeeded)
            {
                transportResponse = new(false, "ServiceUnavailable", "The required denial audit could not be persisted.");
                transportStatus = StatusCodes.Status503ServiceUnavailable;
            }

            await WriteResponseAsync(context, transportResponse, transportStatus).ConfigureAwait(false);
            return;
        }

        AdministrationResponse response;
        int status;
        LabelAdministrationCaller? authenticatedCaller = null;
        try
        {
            string header = context.Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || header.Length > 16384)
            {
                throw new InvalidDataException("A service authentication token is required.");
            }

            ServiceAuthenticationResult authentication = await ServiceAuthentication.VerifyAsync(
                token: header[7..],
                audience: configuration.Audience,
                method: new Nsid("dev.idunno.labeler.admin." + method),
                httpClient: configuration.HttpClient,
                timeProvider: clock,
                cancellationToken: context.RequestAborted,
                maximumLifetime: configuration.MaximumServiceTokenLifetime).ConfigureAwait(false);
            LabelServiceResult<LabelAdministrationCaller> caller = await store.AuthenticateServiceAsync(authentication, context.RequestAborted).ConfigureAwait(false);
            if (!caller.Succeeded)
            {
                response = new(false, caller.Error.Code.ToString(), caller.Error.Message);
            }
            else
            {
                authenticatedCaller = caller.Result;
                AdministrationCommand command;
                if (isPost)
                {
                    byte[] body = await AdministrationWire.ReadAsync(
                        context.Request.Body, AdministrationWire.MaximumRequestBytes, context.RequestAborted).ConfigureAwait(false);
                    command = JsonSerializer.Deserialize(body, AdministrationJsonContext.Default.AdministrationCommand)
                        ?? throw new JsonException("The command is null.");
                    command.Validate();
                    if (command.Method.Length != 0 && command.Method != method)
                    {
                        throw new JsonException("The body method does not match the authenticated endpoint.");
                    }

                    command = command with { Method = method };
                }
                else
                {
                    if (context.Request.QueryString.Value?.Length > 2048 ||
                        context.Request.Query.Any(pair => pair.Value.Count != 1 ||
                            pair.Key is not ("afterSequence" or "limit" or "operationId" or "reason")))
                    {
                        throw new JsonException("The query parameters are invalid.");
                    }

                    command = new(
                        Method: method,
                        OperationId: context.Request.Query["operationId"].ToString(),
                        Reason: context.Request.Query["reason"].ToString() is { Length: > 0 } reason ? reason : "Administrative inspection.",
                        AfterSequence: context.Request.Query.ContainsKey("afterSequence") ? long.Parse(context.Request.Query["afterSequence"].ToString(), System.Globalization.CultureInfo.InvariantCulture) : 0,
                        Limit: context.Request.Query.ContainsKey("limit") ? int.Parse(context.Request.Query["limit"].ToString(), System.Globalization.CultureInfo.InvariantCulture) : AdministrationCommand.MaximumAuditPageSize);
                }

                response = await dispatcher.DispatchAsync(caller.Result, command, context.RequestAborted).ConfigureAwait(false);
            }

            status = ResponseStatus(response);
        }
        catch (Exception exception) when (exception is CryptographicException or InvalidDataException)
        {
            AdministrationLog.Rejected(logger, exception.GetType().Name);
            response = new(false, "InvalidToken", "Authentication failed or the message exceeds its bounds.");
            status = StatusCodes.Status401Unauthorized;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or OverflowException)
        {
            AdministrationLog.Rejected(logger, exception.GetType().Name);
            response = new(false, "InvalidRequest", "The administrative input is invalid.");
            status = StatusCodes.Status400BadRequest;
        }
        catch (HttpRequestException)
        {
            AdministrationLog.Rejected(logger, "identity-unavailable");
            response = new(false, "ServiceUnavailable", "Identity verification is unavailable.");
            status = StatusCodes.Status503ServiceUnavailable;
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            AdministrationLog.Rejected(logger, "identity-timeout");
            response = new(false, "ServiceUnavailable", "Identity verification timed out.");
            status = StatusCodes.Status503ServiceUnavailable;
        }

        if (status is StatusCodes.Status401Unauthorized or StatusCodes.Status400BadRequest)
        {
            LabelServiceResult<LabelAdministrationReceipt> audit = await store
                .RecordAdministrationDenialAsync(
                    reason: status == StatusCodes.Status401Unauthorized ? "Authentication rejected." : "Malformed administrative invocation.",
                    caller: authenticatedCaller,
                    cancellationToken: context.RequestAborted).ConfigureAwait(false);
            if (!audit.Succeeded)
            {
                response = new(false, "ServiceUnavailable", "The required denial audit could not be persisted.");
                status = StatusCodes.Status503ServiceUnavailable;
            }
        }

        await WriteResponseAsync(context, response, status).ConfigureAwait(false);
    }

    /// <summary>Writes a bounded administrative response with no-store caching.</summary>
    /// <param name="context">The response context.</param>
    /// <param name="response">The administrative result.</param>
    /// <param name="status">The HTTP status code.</param>
    /// <returns>An asynchronous operation.</returns>
    private static async Task WriteResponseAsync(HttpContext context, AdministrationResponse response, int status)
    {
        byte[] payload = AdministrationWire.SerializeResponse(response, out response);
        if (response.Error == "ResponseTooLarge")
        {
            status = StatusCodes.Status503ServiceUnavailable;
        }

        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        if (status == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        context.Response.ContentType = "application/json";
        await context.Response.Body.WriteAsync(payload, context.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>Maps classified service outcomes without collapsing denials into successful empty responses.</summary>
    internal static int ResponseStatus(AdministrationResponse response) => response.Error switch
    {
        null => StatusCodes.Status200OK,
        "Unauthorized" => StatusCodes.Status403Forbidden,
        "StorageUnavailable" or "OutcomeUnknown" or "ServiceUnavailable" or "ResponseTooLarge" => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status400BadRequest
    };
}

/// <summary>Captures connection-owned transport and request-scheme state before proxy headers are applied.</summary>
internal sealed record ForwardedTransportState(
    System.Net.IPAddress? ImmediatePeer,
    bool DirectTls,
    string OriginalScheme);

/// <summary>Retains trusted configured service identity independently from incoming request headers.</summary>
internal sealed record AdministrationConfiguration(
    AtProtoServiceReference Audience,
    HttpClient HttpClient,
    TimeSpan MaximumServiceTokenLifetime);
