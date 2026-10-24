// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>Identifies the authorization decision granted by an embedding authorizer.</summary>
/// <param name="version">The nonblank authorization decision version.</param>
/// <exception cref="ArgumentException"><paramref name="version"/> is blank, contains U+0000 or is not valid UTF-8.</exception>
/// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> exceeds 256 UTF-8 bytes.</exception>
/// <remarks>
/// <para>The version is recorded with the operation audit outcome; it does not itself enforce revocation or provide an atomic authorization fence.</para>
/// </remarks>
public sealed class LabelAuthorizationGrant(string version)
{
    /// <summary>Gets the version identifying the authorization decision.</summary>
    /// <value>The nonblank authorization version.</value>
    public string Version { get; } = LabelInput.ValidateText(version, nameof(version), 256);

    /// <summary>Gets the optional provider-issued administration evidence.</summary>
    public LabelAdministrationCaller? AdministrationCaller { get; internal init; }
}
