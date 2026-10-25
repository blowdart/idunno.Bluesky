// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Installs opt-in administration adapters without changing public label distribution.</summary>
public static class LabelerAdministrationExtensions
{
    private static readonly TimeSpan s_defaultMaximumServiceTokenLifetime = TimeSpan.FromMinutes(15);

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The overloads select between a borrowed single key and a host-owned key ring, which cannot be represented by one unambiguous parameter type.")]
    public static IServiceCollection AddLabelerAdministration(
        this IServiceCollection services,
        AtProtoServiceReference audience,
        LabelSigningKey signingKey,
        HttpClient httpClient,
        TimeSpan? maximumServiceTokenLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        return AddLabelerAdministration(
            services: services,
            audience: audience,
            signingKeyRing: new FixedLabelSigningKeyRing(signingKey),
            httpClient: httpClient,
            maximumServiceTokenLifetime: maximumServiceTokenLifetime);
    }

    /// <summary>Registers administration with a host-owned key ring for issuance and coordinated key rotation.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="audience">The exact DID service reference for administrative service tokens.</param>
    /// <param name="signingKeyRing">The host-owned key ring, including the active and preinstalled replacement keys.</param>
    /// <param name="httpClient">The caller-owned, hardened identity-resolution client.</param>
    /// <param name="maximumServiceTokenLifetime">The maximum accepted service-token lifetime. The default is 15 minutes.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumServiceTokenLifetime"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Storage, policy or administration registration is missing or ambiguous.</exception>
    /// <remarks><para>The key ring owns its keys and persists the active key identity. Do not transfer private key material
    /// through administration requests. Initialize the database and bootstrap explicitly before accepting commands.</para></remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The overloads select between a borrowed single key and a host-owned key ring, which cannot be represented by one unambiguous parameter type.")]
    public static IServiceCollection AddLabelerAdministration(
        this IServiceCollection services,
        AtProtoServiceReference audience,
        ILabelSigningKeyRing signingKeyRing,
        HttpClient httpClient,
        TimeSpan? maximumServiceTokenLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(signingKeyRing);
        ArgumentNullException.ThrowIfNull(httpClient);
        TimeSpan lifetimeLimit = maximumServiceTokenLifetime ?? s_defaultMaximumServiceTokenLifetime;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetimeLimit, TimeSpan.Zero);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(AdministrationDispatcher)) ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelStore)) != 1 ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelAdministrationStore)) != 1 ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelMaintenanceStore)) != 1 ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelIssuancePolicyProvider)) != 1)
        {
            throw new InvalidOperationException("Register exactly one administrative store and policy, and register administration once.");
        }

        services.AddLogging();
        services.AddOptions<LabelerAdministrationOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new AdministrationConfiguration(audience, httpClient, lifetimeLimit));
        services.AddSingleton(signingKeyRing);
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
                maintenance: provider.GetRequiredService<ILabelMaintenanceStore>(),
                httpClient: httpClient,
                signingKeyRing: provider.GetRequiredService<ILabelSigningKeyRing>(),
                policy: provider.GetRequiredService<ILabelIssuancePolicyProvider>(),
                publicEndpoint: provider.GetService<LabelerHostingOptions>()?.PublicEndpoint,
                clock: provider.GetRequiredService<TimeProvider>(),
                host: provider.GetService<LabelerHost>());
        });
        return services;
    }

    /// <summary>Provides a nonrotating adapter for the legacy single-key registration overload.</summary>
    private sealed class FixedLabelSigningKeyRing(LabelSigningKey signingKey) : ILabelSigningKeyRing
    {
        private const string KeyId = "configured";

        /// <inheritdoc/>
        public string ActiveKeyId => KeyId;

        /// <inheritdoc/>
        public LabelSigningKey GetSigningKey(string keyId) =>
            keyId == KeyId ? signingKey : throw new KeyNotFoundException("The configured single signing key has no replacement.");

        /// <inheritdoc/>
        public string GetPublicKeyMultibase(string keyId) =>
            keyId == KeyId ? signingKey.ExportPublicKeyMultibase() : throw new KeyNotFoundException("The configured single signing key has no replacement.");

        /// <inheritdoc/>
        public Task ActivateAsync(string keyId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (keyId != KeyId)
            {
                throw new KeyNotFoundException("The configured single signing key cannot activate a replacement.");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>Maps HTTPS administration methods using exact-audience service authentication.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="remoteEnabled">An optional fixed route setting. When <see langword="null"/>, the current reloadable <see cref="LabelerAdministrationOptions.RemoteEnabled"/> setting controls access.</param>
    /// <returns>The endpoint route builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Remote administration is disabled by default. When <paramref name="remoteEnabled"/> is <see langword="false"/>,
    /// no remote routes are mapped. When it is <see langword="null"/>, mapped remote routes consult the current
    /// <see cref="IOptionsMonitor{TOptions}"/> value on each request, so reloadable host configuration can enable or disable access.
    /// Local administration remains available in either case.
    /// Only service JWTs on requests where <see cref="HttpRequest.IsHttps"/> is <see langword="true"/> are accepted.
    /// For TLS-terminating proxies, call ASP.NET Core's <c>UseForwardedHeaders</c> before mapping endpoints and
    /// configure its trusted proxies and forward limit. Browser OAuth cookies and PDS OAuth
    /// tokens do not grant access.</para></remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6667:Logging in a catch clause should pass the caught exception as a parameter",
        Justification = "Untrusted authentication and JSON exceptions may contain sensitive input; log only their category.")]
    public static IEndpointRouteBuilder MapLabelerAdministration(
        this IEndpointRouteBuilder endpoints,
        bool? remoteEnabled = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (remoteEnabled is false)
        {
            return endpoints;
        }

        AdministrationDispatcher dispatcher = endpoints.ServiceProvider.GetRequiredService<AdministrationDispatcher>();
        IOptionsMonitor<LabelerAdministrationOptions> remoteOptions =
            endpoints.ServiceProvider.GetRequiredService<IOptionsMonitor<LabelerAdministrationOptions>>();
        AdministrationConfiguration configuration = endpoints.ServiceProvider.GetRequiredService<AdministrationConfiguration>();
        ILabelAdministrationStore store = endpoints.ServiceProvider.GetRequiredService<ILabelAdministrationStore>();
        TimeProvider clock = endpoints.ServiceProvider.GetRequiredService<TimeProvider>();
        ILogger logger = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("LabelerAdministration");
        foreach (string method in AdministrationDispatcher.s_getMethods)
        {
            RequestDelegate handler = context => HandleRequestAsync(
                context: context, method: method, isPost: false, dispatcher: dispatcher, configuration: configuration,
                remoteOptions: remoteOptions, fixedRemoteEnabled: remoteEnabled,
                store: store, clock: clock, logger: logger);
            endpoints.MapGet("/xrpc/dev.idunno.labeler.admin." + method, handler).AllowAnonymous();
        }

        foreach (string method in AdministrationDispatcher.s_postMethods)
        {
            RequestDelegate handler = context => HandleRequestAsync(
                context: context, method: method, isPost: true, dispatcher: dispatcher, configuration: configuration,
                remoteOptions: remoteOptions, fixedRemoteEnabled: remoteEnabled,
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
        IOptionsMonitor<LabelerAdministrationOptions> remoteOptions,
        bool? fixedRemoteEnabled,
        ILabelAdministrationStore store,
        TimeProvider clock,
        ILogger logger)
    {
        if (!(fixedRemoteEnabled ?? remoteOptions.CurrentValue.RemoteEnabled))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.Headers.CacheControl = "no-store";
            return;
        }

        // CodeQL cs/user-controlled-bypass is a false positive here: insecure requests return a denial,
        // never dispatch; HTTPS requests still require service-token authentication and team authorization.
        if (!context.Request.IsHttps)
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

/// <summary>Retains trusted configured service identity independently from incoming request headers.</summary>
internal sealed record AdministrationConfiguration(
    AtProtoServiceReference Audience,
    HttpClient HttpClient,
    TimeSpan MaximumServiceTokenLifetime);
