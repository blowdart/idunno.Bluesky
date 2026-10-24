// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Authentication;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Configures an OAuth client metadata document.
/// </summary>
/// <remarks>
/// <para>The client ID, optional name and homepage, callback, initial scopes and any client signing key come from the application's <see cref="OAuthOptions"/>.</para>
/// <para>Web applications are the default. Native applications must explicitly select <see cref="ApplicationType"/> and cannot use client signing keys.</para>
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
    /// Gets or sets the application type advertised in the metadata document.
    /// </summary>
    /// <value>The application type. The default is <see cref="BlueskyOAuthClientApplicationType.Web"/>.</value>
    public BlueskyOAuthClientApplicationType ApplicationType { get; set; }

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
    /// Generates an AT Protocol OAuth metadata JSON document.
    /// </summary>
    /// <param name="oAuthOptions">The application's OAuth configuration.</param>
    /// <returns>A JSON document suitable for publishing at the configured client ID.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="oAuthOptions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A URL, scope or application type is invalid, the configured scopes do not include <c>atproto</c>,
    /// signing keys share an identifier, or a native client has a signing key or key path.
    /// </exception>
    /// <remarks>
    /// <para>The document declares DPoP, authorization code and refresh token support.</para>
    /// <para>The callback URL uses <see cref="Uri.ToString"/> to match the redirect URI sent by the OAuth client.</para>
    /// <para>
    ///   For web applications, when <see cref="OAuthOptions.ClientSigningKey"/> is set the document describes a confidential client, publishing the
    ///   public keys of it and of any <see cref="OAuthOptions.AdditionalClientSigningKeys"/> in <c>jwks</c> with
    ///   <c>private_key_jwt</c> client authentication. Otherwise it describes a public client with no client authentication.
    /// </para>
    /// <para>Localhost development client IDs use virtual metadata supplied by the authorization server and cannot be published with this generator.</para>
    /// <para>
    /// Native public clients can declare an HTTP callback on the literal loopback IP address <c>127.0.0.1</c> or <c>[::1]</c>,
    /// but not the hostname <c>localhost</c>. A port may be omitted to allow an ephemeral callback port under RFC 8252.
    /// This does not permit insecure authorization server or personal data server transport.
    /// </para>
    /// </remarks>
    public string GenerateJson(OAuthOptions oAuthOptions)
    {
        ArgumentNullException.ThrowIfNull(oAuthOptions);
        if (ApplicationType is not BlueskyOAuthClientApplicationType.Web and not BlueskyOAuthClientApplicationType.Native)
        {
            throw new ArgumentException($"{nameof(ApplicationType)} must be web or native.");
        }

        bool native = ApplicationType == BlueskyOAuthClientApplicationType.Native;
        if (native &&
            (oAuthOptions.ClientSigningKey is not null ||
             !string.IsNullOrWhiteSpace(oAuthOptions.ClientSigningKeyPath) ||
             oAuthOptions.AdditionalClientSigningKeys.Count != 0 ||
             oAuthOptions.AdditionalClientSigningKeyPaths.Any(path => !string.IsNullOrWhiteSpace(path))))
        {
            throw new ArgumentException("Native applications must be public clients without client signing keys.", nameof(oAuthOptions));
        }

        oAuthOptions.ValidateClientSigningKeyIds();

        if (!Uri.TryCreate(oAuthOptions.ClientId, UriKind.Absolute, out Uri? clientId) ||
            !IsHttpsUrl(clientId) ||
            HasExplicitPort(clientId) ||
            !string.IsNullOrEmpty(clientId.Fragment) ||
            (native && (clientId.IsLoopback || IsLocalhost(clientId))))
        {
            throw new ArgumentException("The client ID must be an absolute HTTPS URL without credentials, a port or a fragment.", nameof(oAuthOptions));
        }

        Uri? returnUri = oAuthOptions.ReturnUri;
        if (returnUri is null ||
            !(IsHttpsUrl(returnUri) || (native && IsNativeLoopbackCallback(returnUri))) ||
            !string.IsNullOrEmpty(returnUri.Fragment) ||
            (native && IsLocalhost(returnUri)) ||
            (returnUri.Scheme == Uri.UriSchemeHttps && returnUri.IsDefaultPort && HasExplicitPort(returnUri)))
        {
            throw new ArgumentException("The callback must use HTTPS or a native public client's HTTP loopback IP, without credentials or a fragment. HTTPS callbacks cannot specify the default port.", nameof(oAuthOptions));
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
            ClientId: oAuthOptions.ClientId,
            RedirectUris: [returnUri.ToString()],
            Scope: string.Join(' ', scopes),
            ClientName: oAuthOptions.ClientName,
            ClientUri: oAuthOptions.ClientUri?.OriginalString,
            LogoUri: LogoUri?.OriginalString,
            TosUri: oAuthOptions.TosUri?.OriginalString,
            PolicyUri: oAuthOptions.PolicyUri?.OriginalString,
            TokenEndpointAuthMethod: signingKey is null ? "none" : "private_key_jwt",
            TokenEndpointAuthSigningAlgorithm: signingKey is null ? null : OAuthClientSigningKey.Algorithm,
            JsonWebKeySet: signingKey is null ? null : new OAuthClientJsonWebKeySet(keys),
            ApplicationType: native ? "native" : "web");

        return JsonSerializer.Serialize(document, SourceGenerationContext.Default.OAuthClientMetadata);
    }

    private static bool IsWebUrl(Uri uri) =>
        uri.IsAbsoluteUri && uri.IsWellFormedOriginalString() &&
        !uri.OriginalString.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
        !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);

    private static bool IsHttpsUrl(Uri uri) => IsWebUrl(uri) && uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>
    /// Checks the localhost hostname, including its absolute DNS form.
    /// </summary>
    private static bool IsLocalhost(Uri uri) =>
        string.Equals(uri.IdnHost.TrimEnd('.'), "localhost", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks that an HTTP callback uses a literal RFC 8252 loopback address.
    /// </summary>
    private static bool IsNativeLoopbackCallback(Uri uri)
    {
        if (!IsWebUrl(uri) || uri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        string authority = uri.OriginalString[(uri.OriginalString.IndexOf("://", StringComparison.Ordinal) + 3)..].Split('/', '?', '#')[0];

        return authority is "127.0.0.1" or "[::1]" ||
            authority.StartsWith("127.0.0.1:", StringComparison.Ordinal) ||
            authority.StartsWith("[::1]:", StringComparison.Ordinal);
    }

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
