// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Authentication;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Configures the optional fields in a web client's OAuth metadata document.
/// </summary>
/// <remarks>
/// <para>The client ID, optional name and homepage, callback, initial scopes and any client signing key come from the application's <see cref="OAuthOptions"/>.</para>
/// <para>Native applications are not supported by this document generator.</para>
/// </remarks>
public sealed class BlueskyOAuthClientMetadataOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlueskyOAuthClientMetadataOptions"/> class.
    /// </summary>
    public BlueskyOAuthClientMetadataOptions()
    {
    }

    /// <summary>
    /// Gets or sets the HTTPS URL of the client logo.
    /// </summary>
    public Uri? LogoUri { get; set; }

    /// <summary>
    /// Gets the additional scopes the client might request after its initial login.
    /// </summary>
    /// <remarks>
    /// <para>These scopes are advertised in metadata only; they do not change the scopes requested at login.</para>
    /// </remarks>
    public IList<string> AdditionalScopes { get; } = [];

    /// <summary>
    /// Generates a web client's AT Protocol OAuth metadata JSON document.
    /// </summary>
    /// <param name="oAuthOptions">The application's OAuth configuration.</param>
    /// <returns>A JSON document suitable for publishing at the configured client ID.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="oAuthOptions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A URL or scope is invalid, the configured scopes do not include <c>atproto</c>, or signing keys share an identifier.</exception>
    /// <remarks>
    /// <para>The document declares DPoP, authorization code and refresh token support.</para>
    /// <para>
    ///   When <see cref="OAuthOptions.ClientSigningKey"/> is set the document describes a confidential client, publishing the
    ///   public keys of it and of any <see cref="OAuthOptions.AdditionalClientSigningKeys"/> in <c>jwks</c> with
    ///   <c>private_key_jwt</c> client authentication. Otherwise it describes a public client with no client authentication.
    /// </para>
    /// <para>Localhost development client IDs use virtual metadata supplied by the authorization server and cannot be published with this generator.</para>
    /// </remarks>
    public string GenerateJson(OAuthOptions oAuthOptions)
    {
        ArgumentNullException.ThrowIfNull(oAuthOptions);
        oAuthOptions.ValidateClientSigningKeyIds();

        if (!Uri.TryCreate(oAuthOptions.ClientId, UriKind.Absolute, out Uri? clientId) ||
            !IsHttpsUrl(clientId) ||
            HasExplicitPort(clientId) ||
            !string.IsNullOrEmpty(clientId.Fragment))
        {
            throw new ArgumentException("The client ID must be an absolute HTTPS URL without credentials, a port or a fragment.", nameof(oAuthOptions));
        }

        Uri? returnUri = oAuthOptions.ReturnUri;
        if (returnUri is null ||
            !IsHttpsUrl(returnUri) ||
            !string.IsNullOrEmpty(returnUri.Fragment) ||
            (returnUri.IsDefaultPort && HasExplicitPort(returnUri)))
        {
            throw new ArgumentException("The callback must be an absolute HTTPS URL without credentials, a fragment or an explicit default port.", nameof(oAuthOptions));
        }

        string[] scopes = [.. oAuthOptions.GetRequestedScopes().Concat(AdditionalScopes).Distinct(StringComparer.Ordinal)];
        if (scopes.Any(scope => string.IsNullOrEmpty(scope) || scope.Any(character =>
            character < '!' || character > '~' || character is '"' or '\\')) ||
            !scopes.Contains("atproto", StringComparer.Ordinal))
        {
            throw new ArgumentException("Scopes must be nonempty OAuth scope tokens and must include atproto.", nameof(oAuthOptions));
        }

        if (oAuthOptions.ClientUri is Uri clientUri &&
            (!IsWebUrl(clientUri) ||
             !string.Equals(clientUri.IdnHost, clientId.IdnHost, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("ClientUri must be an HTTP or HTTPS URL with the same hostname as the client ID.", nameof(oAuthOptions));
        }

        ValidateHttpsUrl(LogoUri, nameof(LogoUri));
        ValidateHttpsUrl(oAuthOptions.TosUri, nameof(oAuthOptions.TosUri));
        ValidateHttpsUrl(oAuthOptions.PolicyUri, nameof(oAuthOptions.PolicyUri));

        OAuthClientSigningKey? signingKey = oAuthOptions.ClientSigningKey;
        OAuthClientJsonWebKey[] keys =
        [
            .. oAuthOptions.GetClientSigningKeys()
                .Select(key => new OAuthClientJsonWebKey(
                    "EC",
                    OAuthClientSigningKey.Curve,
                    key.X,
                    key.Y,
                    key.KeyId,
                    OAuthClientSigningKey.Algorithm,
                    "sig"))
        ];

        OAuthClientMetadata document = new(
            oAuthOptions.ClientId,
            [returnUri.OriginalString],
            string.Join(' ', scopes),
            oAuthOptions.ClientName,
            oAuthOptions.ClientUri?.OriginalString,
            LogoUri?.OriginalString,
            oAuthOptions.TosUri?.OriginalString,
            oAuthOptions.PolicyUri?.OriginalString,
            signingKey is null ? "none" : "private_key_jwt",
            signingKey is null ? null : OAuthClientSigningKey.Algorithm,
            signingKey is null ? null : new OAuthClientJsonWebKeySet(keys));

        return JsonSerializer.Serialize(document, SourceGenerationContext.Default.OAuthClientMetadata);
    }

    private static bool IsWebUrl(Uri uri) =>
        uri.IsAbsoluteUri && uri.IsWellFormedOriginalString() &&
        !uri.OriginalString.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
        !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);

    private static bool IsHttpsUrl(Uri uri) => IsWebUrl(uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static void ValidateHttpsUrl(Uri? uri, string propertyName)
    {
        if (uri is not null && !IsHttpsUrl(uri))
        {
            throw new ArgumentException($"{propertyName} must use HTTPS and must not include credentials.");
        }
    }

    private static bool HasExplicitPort(Uri uri)
    {
        // Uri removes explicit default ports from its normalized authority.
        string authority = uri.OriginalString[(uri.OriginalString.IndexOf("://", StringComparison.Ordinal) + 3)..].Split('/', '?', '#')[0];

        return authority.LastIndexOf(':') > authority.LastIndexOf(']');
    }
}
