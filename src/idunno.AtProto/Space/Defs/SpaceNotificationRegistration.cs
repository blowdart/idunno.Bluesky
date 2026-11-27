// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>Identifies an application's optional space-update notification registration.</summary>
/// <remarks>
/// <para>The endpoint must implement registration confirmation and space-update callbacks.
/// Treat the token as a secret and retain the same endpoint and token when reauthorizing.</para>
/// </remarks>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class SpaceNotificationRegistration
{
    /// <summary>Initializes a new instance of the <see cref="SpaceNotificationRegistration"/> class.</summary>
    /// <param name="endpoint">The base HTTPS endpoint for notification callbacks.</param>
    /// <param name="token">The opaque callback secret, at most 512 characters.</param>
    /// <exception cref="ArgumentException">The endpoint is not absolute HTTPS or the token is empty or too long.</exception>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public SpaceNotificationRegistration(Uri endpoint, string token)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Fragment) || token.Length > 512)
        {
            throw new ArgumentException("A base HTTPS endpoint and a token of at most 512 characters are required.");
        }

        Endpoint = endpoint;
        Token = token;
    }

    /// <summary>Gets the base HTTPS endpoint for notification callbacks.</summary>
    /// <value>The application's absolute HTTPS callback endpoint.</value>
    public Uri Endpoint { get; }

    /// <summary>Gets the opaque secret included in notification requests.</summary>
    /// <value>The secret shared with the notification receiver.</value>
    public string Token { get; }
}
