// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace GermNetwork.Com;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A declaration of a Germ Network account.
/// </summary>
/// <remarks><para>See https://github.com/germ-network/lexicon/blob/main/lexicons/com/germnetwork/declaration.json</para></remarks>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(Declaration), "com.germnetwork.declaration")]
public record Declaration : AtProtoRecord
{
    /// <summary>
    /// Creates a new instance of <see cref="Declaration"/>.
    /// </summary>
    /// <param name="version">The version of the declaration.</param>
    /// <param name="currentKey">The current key of the declaration.</param>
    /// <param name="messageMe">The message me settings of the declaration.</param>
    /// <param name="keyPackage">The key package of the declaration.</param>
    /// <param name="continuityProofs">The continuity proofs of the declaration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="version"/> or <paramref name="currentKey"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="version"/> is less than 5 characters or greater than 14 characters.</exception>
    [JsonConstructor]
    public Declaration(string version, Bytes currentKey, MessageMe? messageMe = null, Bytes? keyPackage = null, ICollection<Bytes>? continuityProofs = null)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(currentKey);

        ArgumentOutOfRangeException.ThrowIfLessThan(5, version.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(14, version.Length);

        Version = version;
        CurrentKey = currentKey;
        MessageMe = messageMe;
        KeyPackage = keyPackage;
        ContinuityProofs = continuityProofs;
    }

    /// <summary>
    /// Gets or sets the semver version number, without pre-release or build information, for the format of opaque content.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is <see langword="null" /> or consists only of white-space characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 5 characters or greater than 14 characters.</exception>
    [JsonRequired]
    public string Version
    {
        get;

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            ArgumentOutOfRangeException.ThrowIfLessThan(5, value.Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(14, value.Length);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets an ed25519 public key prefixed with a byte enum.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null" />.</exception>
    [JsonRequired]
    public Bytes CurrentKey
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets who can message this account.
    /// </summary>
    public MessageMe? MessageMe { get; set; }

    /// <summary>
    /// Gets or sets an opaque value, containing MLS KeyPackage(s), and other signature data, signed by the currentKey
    /// </summary>
    public Bytes? KeyPackage { get; set; }

    /// <summary>
    /// Gets or sets a collection of opaque values to allow for key rolling.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Replacement is allowable.")]
    public ICollection<Bytes>? ContinuityProofs { get; set; }
}
