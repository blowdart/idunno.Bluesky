// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Maps the optional labeler-focused Ozone administration adapter.</summary>
public static class LabelerOzoneAdministrationExtensions
{
    private const int MaximumResponseBytes = 1024 * 1024;

    /// <summary>Maps Ozone discovery and optional labeler-focused administration.</summary>
    /// <param name="app">The application being configured.</param>
    /// <returns>The application.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Labeler administration has not been registered.</exception>
    /// <remarks>
    /// <para>Omit this call to leave all Ozone compatibility routes unregistered.
    /// Administrative calls still obey the existing remote mode and owner switch, require HTTPS and
    /// method-bound PDS service JWTs, and share live team checks and durable replay protection with native administration.
    /// Browser cookies and basic passwords never grant access.</para>
    /// <para>Moderation operations require the shared moderation services. Known unsupported operations
    /// return explicit errors; roles do not grant PDS, AppView, queue, template or team-management capabilities.
    /// Ozone itself is not modified.</para>
    /// <para>No cross-origin browser access is enabled. Use the overload accepting allowed origins
    /// to permit a separately hosted Ozone UI without separate CORS registration or middleware.</para>
    /// </remarks>
    public static WebApplication MapOzoneCompatibility(this WebApplication app) =>
        MapOzoneCompatibility(app, []);

    /// <summary>Maps Ozone compatibility with cross-origin browser access limited to explicit UI origins.</summary>
    /// <param name="app">The application being configured.</param>
    /// <param name="allowedOrigins">The exact HTTP(S) UI origins, including non-default ports, without trailing slashes.</param>
    /// <returns>The application.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> or <paramref name="allowedOrigins"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An origin is not an absolute HTTP(S) origin or contains credentials, a path, query, fragment or wildcard.</exception>
    /// <exception cref="InvalidOperationException">Labeler administration has not been registered.</exception>
    /// <remarks>
    /// <para>An empty list enables no cross-origin browser access. CORS applies only to mapped Ozone
    /// discovery and administration routes, permits their GET or POST method and Authorization and
    /// Content-Type headers, and does not permit browser credentials. No separate CORS setup is required.</para>
    /// <para>Preflight requests do not require authentication. Actual administrative requests retain
    /// the HTTPS, remote-mode, owner-switch, method-bound service-token, live-team and replay checks.
    /// CORS is not an authentication or authorization boundary.</para>
    /// </remarks>
    public static WebApplication MapOzoneCompatibility(this WebApplication app, IEnumerable<string> allowedOrigins)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(allowedOrigins);
        string[] origins = [.. allowedOrigins];
        foreach (string origin in origins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) ||
                uri.Scheme is not ("http" or "https") ||
                uri.UserInfo.Length != 0 ||
                origin.Contains('*', StringComparison.Ordinal) ||
                !string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Allowed origins must be absolute HTTP(S) origins without credentials, paths, queries, fragments or wildcards.", nameof(allowedOrigins));
            }
        }

        AdministrationConfiguration configuration = app.Services.GetRequiredService<AdministrationConfiguration>();
        ILabelAdministrationStore store = app.Services.GetRequiredService<ILabelAdministrationStore>();
        IOptionsMonitor<LabelerAdministrationOptions> options =
            app.Services.GetRequiredService<IOptionsMonitor<LabelerAdministrationOptions>>();
        TimeProvider clock = app.Services.GetRequiredService<TimeProvider>();
        OzoneTeamPresence presence = app.Services.GetRequiredService<OzoneTeamPresence>();
        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LabelerOzoneAdministration");
        CorsService cors = new(Options.Create(new CorsOptions()), app.Services.GetRequiredService<ILoggerFactory>());

        MapRoute(
            path: "/.well-known/ozone-metadata.json",
            httpMethod: HttpMethods.Get,
            handler: async context =>
        {
            Uri? origin = app.Services.GetService<LabelerHostingOptions>()?.PublicEndpoint;
            ILabelSigningKeyRing? keys = app.Services.GetService<ILabelSigningKeyRing>();
            if (origin is null || keys is null)
            {
                await WritePayloadAsync(
                    context: context,
                    payload: JsonSerializer.SerializeToUtf8Bytes(
                        new OzoneError("ServiceUnavailable", "Public service identity and a signing key are required for discovery."),
                        OzoneAdministrationJsonContext.Default.OzoneError),
                    status: StatusCodes.Status503ServiceUnavailable).ConfigureAwait(false);
                return;
            }

            await WritePayloadAsync(
                context: context,
                payload: JsonSerializer.SerializeToUtf8Bytes(
                    new OzoneMetadata(
                        Did: configuration.Audience.Did.Value,
                        Url: origin.AbsoluteUri,
                        PublicKey: "did:key:" + keys.GetPublicKeyMultibase(keys.ActiveKeyId)),
                    OzoneAdministrationJsonContext.Default.OzoneMetadata),
                status: StatusCodes.Status200OK).ConfigureAwait(false);
        });

        MapMethod("tools.ozone.server.getConfig", false);
        MapMethod("tools.ozone.team.listMembers", false);
        MapMethod("tools.ozone.team.listOnlineMembers", false);
        foreach (string method in new[]
        {
            "tools.ozone.moderation.queryStatuses", "tools.ozone.moderation.queryEvents",
            "tools.ozone.moderation.getEvent", "tools.ozone.moderation.getRepo", "tools.ozone.moderation.getRecord",
            "tools.ozone.moderation.getRepos", "tools.ozone.moderation.getRecords",
            "app.bsky.actor.getProfile", "app.bsky.feed.getPostThread", "tools.ozone.setting.listOptions",
            "tools.ozone.report.queryReports", "tools.ozone.report.getReport", "tools.ozone.report.listActivities"
        })
        {
            MapMethod(method, false);
        }
        foreach (string method in new[]
        {
            "tools.ozone.moderation.emitEvent", "tools.ozone.team.addMember",
            "tools.ozone.team.updateMember", "tools.ozone.team.deleteMember",
            "tools.ozone.report.closeReports", "tools.ozone.report.createActivity", "tools.ozone.setting.upsertOption"
        })
        {
            MapMethod(method, true);
        }

        return app;

        void MapMethod(string method, bool post)
        {
            async Task HandleAsync(HttpContext context)
            {
                byte[]? payload = null;
                await LabelerAdministrationExtensions.HandleRemoteRequestAsync(
                    context: context,
                    method: new(method),
                    ozone: true,
                    configuration: configuration,
                    remoteOptions: options,
                    fixedRemoteEnabled: null,
                    store: store,
                    clock: clock,
                    logger: logger,
                    dispatch: async caller =>
                    {
                        LabelServiceResult<IReadOnlyList<LabelTeamMember>> team =
                            await store.GetTeamAsync(caller, context.RequestAborted).ConfigureAwait(false);
                        if (!team.Succeeded)
                        {
                            return new(false, team.Error.Code.ToString(), team.Error.Message);
                        }

                        presence.Observe(caller.Actor, team.Result);
                        if (method == "tools.ozone.server.getConfig")
                        {
                            ValidateQuery(context, []);
                            LabelTeamMember member = team.Result.Single(member => member.Did == caller.Actor);
                            payload = JsonSerializer.SerializeToUtf8Bytes(
                                new OzoneConfigView(new OzoneViewer(Role(member.Role))),
                                OzoneAdministrationJsonContext.Default.OzoneConfigView);
                        }
                        else if (method == "tools.ozone.team.listMembers")
                        {
                            payload = JsonSerializer.SerializeToUtf8Bytes(
                                TeamPage(context, team.Result),
                                OzoneAdministrationJsonContext.Default.OzoneTeamPage);
                        }
                        else if (method == "tools.ozone.team.listOnlineMembers")
                        {
                            ValidateQuery(context, ["limit"]);
                            int limit = context.Request.Query.ContainsKey("limit")
                                ? int.Parse(context.Request.Query["limit"].ToString(), CultureInfo.InvariantCulture)
                                : 50;
                            ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
                            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);
                            payload = JsonSerializer.SerializeToUtf8Bytes(
                                new OzoneOnlineTeamPage(presence.List(team.Result, limit)),
                                OzoneAdministrationJsonContext.Default.OzoneOnlineTeamPage);
                        }
                        else
                        {
                            OzoneModerationAdapter adapter = new(
                                store: app.Services.GetRequiredService<ILabelStore>(),
                                administration: store,
                                policy: app.Services.GetRequiredService<ILabelIssuancePolicyProvider>(),
                                keys: app.Services.GetService<ILabelSigningKeyRing>(),
                                identity: configuration.HttpClient,
                                clock: clock,
                                host: app.Services.GetService<LabelerHost>());
                            (AdministrationResponse response, byte[]? body) = await adapter.DispatchAsync(context, method, caller).ConfigureAwait(false);
                            payload = body;
                            if (response.Succeeded)
                            {
                                return response;
                            }
                            LabelServiceResult<LabelAdministrationReceipt> audit = await store.RecordAdministrationDenialAsync(
                                reason: "Rejected Ozone administration method.",
                                caller: caller,
                                cancellationToken: context.RequestAborted).ConfigureAwait(false);
                            return audit.Succeeded
                                ? response
                                : new(false, "ServiceUnavailable", "The required denial audit could not be persisted.");
                        }

                        return new(true);
                    },
                    write: (responseContext, response, status) => WritePayloadAsync(
                        context: responseContext,
                        payload: response.Succeeded
                            ? payload ?? throw new InvalidOperationException("A successful Ozone response requires a payload.")
                            : JsonSerializer.SerializeToUtf8Bytes(
                                new OzoneError(response.Error!, response.Message!),
                                OzoneAdministrationJsonContext.Default.OzoneError),
                        status: response.Error == "NotImplemented" ? StatusCodes.Status501NotImplemented : status)).ConfigureAwait(false);
            }

            MapRoute("/xrpc/" + method, post ? HttpMethods.Post : HttpMethods.Get, HandleAsync);
        }

        void MapRoute(string path, string httpMethod, RequestDelegate handler)
        {
            CorsPolicy policy = new CorsPolicyBuilder()
                .WithOrigins(origins)
                .WithMethods(httpMethod)
                .WithHeaders("Authorization", "Content-Type")
                .Build();

            // Evaluate CORS inside these routes so callers need no global middleware or service setup.
            app.MapMethods(path, [httpMethod, HttpMethods.Options], (RequestDelegate)(async context =>
            {
                CorsResult result = cors.EvaluatePolicy(context, policy);
                if (HttpMethods.IsOptions(context.Request.Method))
                {
                    cors.ApplyResult(result, context.Response);
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }

                context.Response.OnStarting(() =>
                {
                    cors.ApplyResult(result, context.Response);
                    return Task.CompletedTask;
                });
                await handler(context).ConfigureAwait(false);
            })).AllowAnonymous();
        }
    }

    /// <summary>Projects a bounded live team page without inventing profile or audit timestamps.</summary>
    private static OzoneTeamPage TeamPage(HttpContext context, IReadOnlyList<LabelTeamMember> team)
    {
        ValidateQuery(context, ["q", "disabled", "roles", "limit", "cursor"]);
        IQueryCollection query = context.Request.Query;
        int limit = query.ContainsKey("limit") ? int.Parse(query["limit"].ToString(), CultureInfo.InvariantCulture) : 50;
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);
        string text = query["q"].ToString();
        string cursor = query["cursor"].ToString();
        if (cursor.Length > 0)
        {
            _ = new Did(cursor);
        }
        bool? disabled = query.ContainsKey("disabled") ? bool.Parse(query["disabled"].ToString()) : null;
        string[] roles = [.. query["roles"].Select(value => value ?? throw new JsonException("A role is required."))];
        OzoneMember[] members = [.. team
            .Where(member => member.Did.Value.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                (disabled is null || disabled == (!member.Active || member.Removed)) &&
                (roles.Length == 0 || roles.Contains(Role(member.Role), StringComparer.Ordinal)) &&
                string.CompareOrdinal(member.Did.Value, cursor) > 0)
            .OrderBy(member => member.Did.Value, StringComparer.Ordinal)
            .Take(limit + 1)
            .Select(member => new OzoneMember(member.Did.Value, Role(member.Role), !member.Active || member.Removed))];
        bool more = members.Length > limit;
        return new(more ? members[..limit] : members, more ? members[limit - 1].Did : null);
    }

    /// <summary>Rejects unbounded, unknown or ambiguous query parameters while allowing repeated role filters.</summary>
    private static void ValidateQuery(HttpContext context, string[] allowed)
    {
        if (context.Request.QueryString.Value?.Length > 2048 ||
            context.Request.Query.Any(pair => !allowed.Contains(pair.Key, StringComparer.Ordinal) ||
                pair.Key == "roles" && pair.Value.Count > 4 ||
                pair.Key != "roles" && pair.Value.Count != 1))
        {
            throw new JsonException("The Ozone query parameters are invalid.");
        }
    }

    /// <summary>Maps existing roles without changing their underlying provider permissions.</summary>
    private static string Role(LabelTeamRole role) => role switch
    {
        LabelTeamRole.Administrator => "tools.ozone.team.defs#roleAdmin",
        LabelTeamRole.Moderator => "tools.ozone.team.defs#roleModerator",
        LabelTeamRole.Viewer => "tools.ozone.team.defs#roleTriage",
        _ => throw new InvalidOperationException("The retained team role is invalid.")
    };

    /// <summary>Writes source-generated wire output with bounded size and no-store caching.</summary>
    private static async Task WritePayloadAsync(HttpContext context, byte[] payload, int status)
    {
        if (payload.Length > MaximumResponseBytes)
        {
            payload = JsonSerializer.SerializeToUtf8Bytes(
                new OzoneError("ServiceUnavailable", "The Ozone response exceeds its bounds."),
                OzoneAdministrationJsonContext.Default.OzoneError);
            status = StatusCodes.Status503ServiceUnavailable;
        }
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.ContentType = "application/json";
        if (status == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }
        await context.Response.Body.WriteAsync(payload, context.RequestAborted).ConfigureAwait(false);
    }
}

/// <summary>Describes the configured public labeler identity in Ozone's discovery format.</summary>
internal sealed record OzoneMetadata(string Did, string Url, string PublicKey);

/// <summary>Describes the authenticated viewer without claiming unavailable upstream services.</summary>
internal sealed record OzoneConfigView(OzoneViewer Viewer);

/// <summary>Describes the live mapped team role.</summary>
internal sealed record OzoneViewer(string Role);

/// <summary>Describes a retained team member without exposing native notification addresses.</summary>
internal sealed record OzoneMember(string Did, string Role, bool Disabled);

/// <summary>Describes a bounded team page and its exclusive DID continuation.</summary>
internal sealed record OzoneTeamPage(OzoneMember[] Members, string? Cursor);

/// <summary>Describes observed activity without claiming a persistent browser connection.</summary>
internal sealed record OzoneOnlineMember(string Did, DateTimeOffset LastActive);

/// <summary>Describes recent authenticated activity on this host instance.</summary>
internal sealed record OzoneOnlineTeamPage(OzoneOnlineMember[] Members);

/// <summary>Describes a standard XRPC error rather than a native administration envelope.</summary>
internal sealed record OzoneError(string Error, string Message);

/// <summary>Provides reflection-free Ozone discovery and administration wire metadata.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OzoneMetadata))]
[JsonSerializable(typeof(OzoneConfigView))]
[JsonSerializable(typeof(OzoneTeamPage))]
[JsonSerializable(typeof(OzoneOnlineTeamPage))]
[JsonSerializable(typeof(OzoneError))]
internal sealed partial class OzoneAdministrationJsonContext : JsonSerializerContext;
