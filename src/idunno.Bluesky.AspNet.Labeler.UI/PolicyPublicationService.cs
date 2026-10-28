// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Uses a one-shot, separately scoped labeler authorization without replacing the administrator's sign-in.</summary>
internal sealed partial class PolicyPublicationService(
    LabelerUiAdministration administration,
    IOptionsMonitor<BlueskyAgentOptions> options,
    IHttpClientFactory clients)
{
    internal const string Scheme = "Xenon.PolicyPublication";
    private const string AuditReason = "Publish the selected declaration from Xenon using separate labeler authorization.";

    /// <summary>Builds a separate web client from configured origins, never request host headers or moderator scopes.</summary>
    internal OAuthOptions CreateOptions(bool? create = null)
    {
        OAuthOptions website = options.CurrentValue.OAuthOptions ??
            throw new InvalidOperationException("Configure website OAuth before publishing a declaration.");
        Uri callback = website.ReturnUri ??
            throw new InvalidOperationException("Configure the website OAuth callback before publishing a declaration.");
        Uri origin = new(callback.GetLeftPart(UriPartial.Authority));
        if (origin.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Web declaration publication requires a configured public HTTPS origin.");
        }
        string writeAction = create is true ? "create" : "update";
        OAuthOptions publication = new()
        {
            ClientId = new Uri(origin, "/Xenon/Policy/ClientMetadata").AbsoluteUri,
            ClientName = "Xenon declaration publication",
            ClientUri = website.ClientUri,
            TosUri = website.TosUri,
            PolicyUri = website.PolicyUri,
            ReturnUri = new Uri(origin, "/Xenon/Policy/PublishCallback"),
            ClientSigningKey = website.ClientSigningKey,
            ClientSigningKeyPath = website.ClientSigningKeyPath,
            ClientSigningKeyId = website.ClientSigningKeyId,
            Scopes = create is null
                ? ["atproto", "repo:app.bsky.labeler.service?action=create", "repo:app.bsky.labeler.service?action=update"]
                : ["atproto", $"repo:app.bsky.labeler.service?action={writeAction}"]
        };
        foreach (OAuthClientSigningKey key in website.AdditionalClientSigningKeys)
        {
            publication.AdditionalClientSigningKeys.Add(key);
        }
        foreach (string path in website.AdditionalClientSigningKeyPaths)
        {
            publication.AdditionalClientSigningKeyPaths.Add(path);
        }
        publication.Validate();
        return publication;
    }

    /// <summary>Checks current administrator authority and exact selection before requesting only the required write action.</summary>
    internal async Task<Uri> StartAsync(HttpContext context, string operationId, Handle handle)
    {
        ModerationPolicyView policy = await ReadAuthorizedPolicyAsync(context, operationId).ConfigureAwait(false);
        if (policy.Phase != "Publishing")
        {
            throw new InvalidOperationException("Finish label withdrawal before signing in to publish the declaration.");
        }
        OAuthOptions oauth = CreateOptions(policy.PreviousCid is null);
        using BlueskyAgent agent = CreateAgent(oauth);
        OAuthClient client = agent.CreateOAuthClient();
        Uri redirect = await agent.BuildOAuth2LoginUri(
            oAuthClient: client,
            handle: handle,
            scopes: oauth.Scopes,
            returnUri: oauth.ReturnUri,
            stateExtraProperties: new()
            {
                ["flow"] = Scheme,
                ["actor"] = context.User.FindFirst(AtProtoClaims.Did)!.Value,
                ["browser"] = BrowserBinding(context),
                ["operation"] = operationId,
                ["issuer"] = policy.Issuer,
                ["version"] = policy.Definition!.Version,
                ["previousCid"] = policy.PreviousCid ?? string.Empty
            },
            cancellationToken: context.RequestAborted).ConfigureAwait(false);
        BlueskySignInManager manager = CorrelationManager(context);
        await manager.SaveStateAndCreateCorrelationCookie(
            state: client.State ?? throw new InvalidOperationException("Publication OAuth state could not be prepared."),
            correlationId: null,
            markCookieAsSecure: true).ConfigureAwait(false);
        return redirect;
    }

    /// <summary>Consumes correlated authorization once and disposes labeler credentials when the conditional write finishes or fails.</summary>
    internal async Task<string> CompleteAsync(HttpContext context)
    {
        OAuthLoginState state = await CorrelationManager(context).LoadState().ConfigureAwait(false) ??
            throw new InvalidOperationException("The publication sign-in expired or was already used. Inspect policy state and start a new sign-in.");
        IDictionary<string, string> saved = state.ExtraProperties ??
            throw new InvalidOperationException("Publication sign-in has no bound operation.");
        if (!saved.TryGetValue("flow", out string? flow) || flow != Scheme ||
            !saved.TryGetValue("actor", out string? actor) || actor != context.User.FindFirst(AtProtoClaims.Did)?.Value ||
            !saved.TryGetValue("browser", out string? browser) || browser != BrowserBinding(context) ||
            !saved.TryGetValue("operation", out string? operation))
        {
            throw new InvalidOperationException("Publication sign-in does not belong to this administrator browser session.");
        }
        ModerationPolicyView policy = await ReadAuthorizedPolicyAsync(context, operation).ConfigureAwait(false);
        if (!Matches(policy, saved) || policy.Phase != "Publishing")
        {
            throw new InvalidOperationException("Publication was abandoned, superseded or completed during sign-in. No PDS write was attempted.");
        }
        OAuthOptions oauth = CreateOptions(policy.PreviousCid is null);
        if (state.RedirectUri != oauth.ReturnUri!.AbsoluteUri)
        {
            throw new InvalidOperationException("Publication callback configuration changed. Start a new sign-in.");
        }
        using BlueskyAgent agent = CreateAgent(oauth);
        OAuthClient client = agent.CreateOAuthClient(state);
        if (!await agent.ProcessOAuth2LoginResponse(
            oAuthClient: client,
            callbackData: context.Request.QueryString.Value?.TrimStart('?') ?? string.Empty,
            expectedDid: new(policy.Issuer),
            cancellationToken: context.RequestAborted).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The labeler account did not authorize publication. No PDS write was attempted.");
        }
        return await PublishAsync(context, operation, saved, agent).ConfigureAwait(false);
    }

    /// <summary>Reads and fences current protected policy state; inspection alone never substitutes for ManagePolicy authority.</summary>
    private async Task<ModerationPolicyView> ReadAuthorizedPolicyAsync(HttpContext context, string operation)
    {
        AdministrationResponse authority = await administration.AuthorizePolicyPublicationAsync(
            context.User, AuditReason, context.RequestAborted).ConfigureAwait(false);
        if (!authority.Succeeded)
        {
            throw new InvalidOperationException(authority.Message ?? "Current administrator publication authority is required.");
        }
        AdministrationResponse response = await administration.DispatchAsync(
            principal: context.User,
            command: new(Method: "getPolicy", Reason: AuditReason),
            cancellationToken: context.RequestAborted).ConfigureAwait(false);
        ModerationPolicyView policy = response.Moderation?.Policy ??
            throw new InvalidOperationException(response.Message ?? "Policy state is unavailable.");
        if (!response.Succeeded || policy.PublicationOperationId != operation || policy.Definition is null)
        {
            throw new InvalidOperationException("The selected publication changed. Reload policy state before continuing.");
        }
        return policy;
    }

    /// <summary>Rechecks live authority and intent after remote reads, writes only with record-CID compare-and-swap and verifies independently.</summary>
    private async Task<string> PublishAsync(
        HttpContext context,
        string operation,
        IDictionary<string, string> saved,
        BlueskyAgent agent)
    {
        ModerationPolicyView policy = await ReadAuthorizedPolicyAsync(context, operation).ConfigureAwait(false);
        if (!Matches(policy, saved) || policy.Phase != "Publishing" || agent.Did?.Value != policy.Issuer)
        {
            throw new InvalidOperationException("Publication selection or labeler identity changed. No PDS write was attempted.");
        }
        JsonElement declaration = policy.Definition!.Declaration;
        AtProtoHttpResult<AtProtoRepositoryRecord> existing = await agent.GetRawRecord(
            repo: agent.Did,
            collection: new("app.bsky.labeler.service"),
            rKey: new("self"),
            cid: null,
            service: null,
            serviceProxy: null,
            cancellationToken: context.RequestAborted).ConfigureAwait(false);
        bool matches = existing.Succeeded && existing.Result.Value is not null &&
            JsonNode.DeepEquals(existing.Result.Value, JsonNode.Parse(declaration.GetRawText()));
        if (!matches && (existing.Succeeded
            ? existing.Result.Cid.ToString() != policy.PreviousCid
            : existing.AtErrorDetail?.Error != "RecordNotFound" || policy.PreviousCid is not null))
        {
            throw new InvalidOperationException("The PDS declaration does not match the expected previous CID. No overwrite was attempted.");
        }
        ModerationPolicyView current = await ReadAuthorizedPolicyAsync(context, operation).ConfigureAwait(false);
        if (!Matches(current, saved) || current.Phase != "Publishing")
        {
            throw new InvalidOperationException("Publication selection changed during the PDS read. No write was attempted.");
        }
        if (!matches)
        {
            AtProtoRecord record = JsonSerializer.Deserialize(declaration.GetRawText(), PublicationJsonContext.Default.AtProtoRecord) ??
                throw new InvalidDataException("The selected declaration could not be encoded.");
            bool succeeded;
            HttpStatusCode status;
            if (policy.PreviousCid is null)
            {
                AtProtoHttpResult<CreateRecordResult> created = await agent.CreateRecord(
                    record: record,
                    jsonSerializerOptions: RecordOptions,
                    collection: new("app.bsky.labeler.service"),
                    rKey: new("self"),
                    validate: true,
                    cancellationToken: context.RequestAborted).ConfigureAwait(false);
                succeeded = created.Succeeded;
                status = created.StatusCode;
            }
            else
            {
                AtProtoHttpResult<PutRecordResult> updated = await agent.PutRecord(
                    record: record,
                    jsonSerializerOptions: RecordOptions,
                    collection: new("app.bsky.labeler.service"),
                    rKey: new("self"),
                    validate: true,
                    swapRecord: new(policy.PreviousCid),
                    cancellationToken: context.RequestAborted).ConfigureAwait(false);
                succeeded = updated.Succeeded;
                status = updated.StatusCode;
            }
            if (!succeeded)
            {
                throw new InvalidOperationException($"The conditional PDS write did not confirm success (HTTP {(int)status}). Inspect the selected operation and remote declaration before retrying; no unconditional overwrite is allowed.");
            }
        }
        current = await ReadAuthorizedPolicyAsync(context, operation).ConfigureAwait(false);
        if (!Matches(current, saved) || current.Phase != "Publishing")
        {
            throw new InvalidOperationException("Publication state changed after the PDS operation. Inspect current state; a completed PDS write is not rolled back.");
        }
        AdministrationResponse verified = await administration.DispatchAsync(
            principal: context.User,
            command: new(Method: "verifyPolicy", Reason: AuditReason),
            cancellationToken: context.RequestAborted).ConfigureAwait(false);
        if (!verified.Succeeded || verified.Moderation?.Policy is not { Phase: "VerifiedActive" } active ||
            !Matches(active, saved))
        {
            throw new InvalidOperationException(verified.Message ??
                "The PDS operation completed but independent verification did not activate this policy. Inspect state and use Verify declaration; do not assume rollback.");
        }
        return "The selected declaration is published and independently verified.";
    }

    /// <summary>Creates a request-local agent without attaching labeler tokens to the administrator identity store.</summary>
    private BlueskyAgent CreateAgent(OAuthOptions oauth) => new(
        httpClientFactory: clients,
        options: new BlueskyAgentOptions
        {
            OAuthOptions = oauth,
            HttpClientOptions = options.CurrentValue.HttpClientOptions
        });

    /// <summary>Separates publication correlation cookies from normal sign-in while reusing its configured encrypted, single-use state cache.</summary>
    private static BlueskySignInManager CorrelationManager(HttpContext context)
    {
        BlueskySignInManager manager = context.RequestServices.GetRequiredService<BlueskySignInManager>();
        manager.AuthenticationScheme = Scheme;
        return manager;
    }

    /// <summary>Binds pending authorization to the authenticated session's DPoP key without persisting the key in publication state.</summary>
    private static string BrowserBinding(HttpContext context)
    {
        string proof = context.User.FindFirst(AtProtoClaims.DPoPProof)?.Value ??
            throw new InvalidOperationException("A current Bluesky OAuth browser session is required to publish.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(proof)));
    }

    /// <summary>Compares immutable publication identity, not a historical receipt or a matching policy ID alone.</summary>
    private static bool Matches(ModerationPolicyView policy, IDictionary<string, string> saved) =>
        saved.TryGetValue("operation", out string? operation) && operation == policy.PublicationOperationId &&
        saved.TryGetValue("issuer", out string? issuer) && issuer == policy.Issuer &&
        saved.TryGetValue("version", out string? version) && version == policy.Definition?.Version &&
        saved.TryGetValue("previousCid", out string? cid) && cid == (policy.PreviousCid ?? string.Empty);

    /// <summary>Surfaces classified failures without putting OAuth codes, tokens or private keys in application logs.</summary>
    [LoggerMessage(EventId = 7420, Level = LogLevel.Warning, Message = "Web policy publication did not complete ({Category}). Inspect the selected operation and authoritative declaration before recovery.")]
    internal static partial void Failed(ILogger logger, string category);

    /// <summary>Uses the SDK envelope metadata alongside raw declaration extension data without reflection.</summary>
    private static JsonSerializerOptions RecordOptions { get; } = AtProtoJsonSerializerOptions.Create(settings =>
    {
        settings.TypeInfoResolverChain.Add(PublicationJsonContext.Default);
        settings.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

    /// <summary>Provides reflection-free metadata for raw declaration writes.</summary>
    [JsonSerializable(typeof(AtProtoRecord))]
    internal sealed partial class PublicationJsonContext : JsonSerializerContext;
}
