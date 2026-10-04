// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;
using idunno.Bluesky;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Samples.AspNetProgressiveAuthentication;

internal static class ProfilePermissions
{
    internal const string SessionKey = "profile-edit-session";
    internal const string PendingEditKey = "profile-edit";
    internal const string WriteScope = "repo:app.bsky.actor.profile?action=update";
    internal const string CreateScope = "repo:app.bsky.actor.profile?action=create";

    internal static string[] ReadScopes =>
    [
        "atproto",
        "rpc:app.bsky.feed.getTimeline?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview",
        "rpc:app.bsky.actor.getPreferences?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview",
        "rpc:app.bsky.actor.getProfile?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview"
    ];

    // putRecord checks both actions before determining whether the record already exists.
    internal static string[] WriteScopes => [WriteScope, CreateScope];

    internal static string[] MaximumScopes => [.. ReadScopes, .. WriteScopes];

    internal static void Configure(BlueskyAgentOptions options)
    {
        OAuthOptions oauth = options.OAuthOptions ?? throw new InvalidOperationException("Configure OAuth options.");
        oauth.Scopes = ReadScopes;
        oauth.PermissionSets = [];

        // Localhost virtual metadata must declare the maximum, not the initial PAR subset.
        Uri clientId = new(oauth.ClientId);
        if (clientId.Scheme == Uri.UriSchemeHttp && clientId.Host == "localhost")
        {
            var parameters = QueryHelpers.ParseQuery(clientId.Query);
            parameters["scope"] = string.Join(' ', MaximumScopes);
            oauth.ClientId = QueryHelpers.AddQueryString(
                clientId.GetLeftPart(UriPartial.Path), parameters);
        }
    }

    internal static string[] EffectiveScopes(DPoPAccessCredentials credentials)
    {
        JsonWebToken token = new(credentials.AccessJwt);
        string? scope = token.TryGetClaim("scope", out var claim) ? claim.Value : null;

        // A reference grant cannot be expanded locally. The write API remains authoritative.
        if (scope?.StartsWith("ref:", StringComparison.Ordinal) == true)
        {
            scope = credentials.RequestedScope;
        }

        return scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
    }

    internal static bool CanUpdate(DPoPAccessCredentials credentials) =>
        WriteScopes.All(scope => EffectiveScopes(credentials).Contains(scope, StringComparer.Ordinal));

    internal static bool HasRequiredScopes(DPoPAccessCredentials credentials, bool requireWrite)
    {
        string[] effective = EffectiveScopes(credentials);
        return (requireWrite ? MaximumScopes : ReadScopes).All(scope => effective.Contains(scope, StringComparer.Ordinal));
    }

    internal static string[] ExpandedScopes(DPoPAccessCredentials credentials) =>
        [.. ReadScopes.Concat(EffectiveScopes(credentials).Where(scope =>
            MaximumScopes.Contains(scope, StringComparer.Ordinal))).Concat(WriteScopes).Distinct(StringComparer.Ordinal)];
}
