// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Installs opt-in administration adapters without changing public label distribution.</summary>
public static class LabelerAdministrationExtensions
{
    /// <summary>Registers administration with mandatory provider-backed team and issuance boundaries.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="audience">The exact DID service reference for administrative service tokens.</param>
    /// <param name="signingKey">The established caller-owned label signing key.</param>
    /// <param name="httpClient">The caller-owned, hardened identity-resolution client.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Storage, policy or administration registration is missing or ambiguous.</exception>
    /// <remarks><para>Initialize the database and bootstrap explicitly before accepting commands.
    /// Keys and clients are borrowed, not disposed. No permissive authorizer or HTTP bootstrap is installed.</para></remarks>
    public static IServiceCollection AddLabelerAdministration(
        this IServiceCollection services,
        AtProtoServiceReference audience,
        LabelSigningKey signingKey,
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentNullException.ThrowIfNull(httpClient);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(AdministrationDispatcher)) ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelStore)) != 1 ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelAdministrationStore)) != 1 ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelIssuancePolicyProvider)) != 1)
        {
            throw new InvalidOperationException("Register exactly one administrative store and policy, and register administration once.");
        }

        services.AddLogging();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new AdministrationConfiguration(audience, httpClient));
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
    /// <returns>The endpoint route builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Only service JWTs are accepted. Configure forwarded headers only for trusted
    /// TLS terminators; arbitrary forwarded headers must not establish HTTPS. Browser OAuth cookies
    /// and PDS OAuth tokens do not grant access to these endpoints.</para></remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6667:Logging in a catch clause should pass the caught exception as a parameter",
        Justification = "Untrusted authentication and JSON exceptions may contain sensitive input; log only their category.")]
    public static IEndpointRouteBuilder MapLabelerAdministration(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        AdministrationDispatcher dispatcher = endpoints.ServiceProvider.GetRequiredService<AdministrationDispatcher>();
        AdministrationConfiguration configuration = endpoints.ServiceProvider.GetRequiredService<AdministrationConfiguration>();
        ILabelAdministrationStore store = endpoints.ServiceProvider.GetRequiredService<ILabelAdministrationStore>();
        TimeProvider clock = endpoints.ServiceProvider.GetRequiredService<TimeProvider>();
        ILogger logger = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("LabelerAdministration");
        foreach (string method in AdministrationDispatcher.s_methods)
        {
            string httpMethod = method.StartsWith("get", StringComparison.Ordinal) ? "GET" : "POST";
            endpoints.MapMethods("/xrpc/dev.idunno.labeler.admin." + method, [httpMethod], async (HttpContext context) =>
            {
                AdministrationResponse response;
                int status;
                LabelAdministrationCaller? authenticatedCaller = null;
                if (!context.Request.IsHttps)
                {
                    response = new(false, "AuthenticationRequired", "HTTPS is required.");
                    status = StatusCodes.Status401Unauthorized;
                }
                else
                {
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
                            cancellationToken: context.RequestAborted).ConfigureAwait(false);
                        LabelServiceResult<LabelAdministrationCaller> caller = await store.AuthenticateServiceAsync(authentication, context.RequestAborted).ConfigureAwait(false);
                        if (!caller.Succeeded)
                        {
                            response = new(false, caller.Error.Code.ToString(), caller.Error.Message);
                        }
                        else
                        {
                            authenticatedCaller = caller.Result;
                            AdministrationCommand command;
                            if (httpMethod == "POST")
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
                                    Limit: context.Request.Query.ContainsKey("limit") ? int.Parse(context.Request.Query["limit"].ToString(), System.Globalization.CultureInfo.InvariantCulture) : 100);
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
                }

                if (status is StatusCodes.Status401Unauthorized or StatusCodes.Status400BadRequest)
                {
                    LabelServiceResult<LabelAdministrationReceipt> audit = await context.RequestServices.GetRequiredService<ILabelAdministrationStore>()
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

                context.Response.StatusCode = status;
                context.Response.Headers.CacheControl = "no-store";
                if (status == StatusCodes.Status401Unauthorized)
                {
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                }

                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(
                    context.Response.Body, response, AdministrationJsonContext.Default.AdministrationResponse, context.RequestAborted).ConfigureAwait(false);
            });
        }

        return endpoints;
    }

    /// <summary>Maps classified service outcomes without collapsing denials into successful empty responses.</summary>
    internal static int ResponseStatus(AdministrationResponse response) => response.Error switch
    {
        null => StatusCodes.Status200OK,
        "Unauthorized" => StatusCodes.Status403Forbidden,
        "StorageUnavailable" or "OutcomeUnknown" or "ServiceUnavailable" => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status400BadRequest
    };
}

/// <summary>Retains trusted configured service identity independently from incoming request headers.</summary>
internal sealed record AdministrationConfiguration(AtProtoServiceReference Audience, HttpClient HttpClient);
