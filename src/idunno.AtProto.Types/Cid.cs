// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace idunno.AtProto;

/// <summary>
/// Provides an object representation of the AT Proto implementation of a Content Identifier (CID).
/// </summary>
/// <remarks>
/// <para>See https://github.com/multiformats/cid for specification.</para>
/// </remarks>
[JsonConverter(typeof(Json.CidConverter))]
public sealed class Cid : IEquatable<Cid>
{
    // The multiformats unsigned-varint specification limits values to 9 bytes / 63 bits.
    private const int MaximumVarIntLength = 9;

    /// <summary>
    /// Creates a new instance of a <see cref="Cid"/> class using the specified parameters.
    /// </summary>
    /// <param name="value">The value of the content identifier.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the provided value is empty.</exception>
    [JsonConstructor]
    public Cid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrEmpty(value);

        try
        {
            if (value.StartsWith("Qm", StringComparison.Ordinal) && value.Length == 46)
            {
                // CIDv0 - base58btc encoded SHA-256 hash

                byte[] bytes = SimpleBase.Base58.Bitcoin.Decode(value);

                Version = 0;
                Codec = 0x70;
                Hash = bytes;
            }
            else
            {
                // CIDv1 - multibase encoded

                byte[] bytes = SimpleBase.Multibase.Decode(value);

                (byte Version, ulong Codec, IReadOnlyList<byte> Hash) result = ParseBytes(bytes);

                Version = result.Version;
                Codec = result.Codec;
                Hash = result.Hash;
            }
        }
        catch (Exception ex)
        {
            throw new ArgumentOutOfRangeException("Conversion failed", ex);
        }
    }

    /// <summary>
    /// Creates a new instance of a <see cref="Cid"/> class using the specified parameters.
    /// </summary>
    /// <param name="bytes">A byte array containing a Cid.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bytes"/> does not represent a Cid.</exception>
    public Cid(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfEqual(bytes.Length, 0);

        try
        {
            (byte Version, ulong Codec, IReadOnlyList<byte> Hash) result = ParseBytes(bytes);
            Version = result.Version;
            Codec = result.Codec;
            Hash = result.Hash;
        }
        catch (Exception ex)
        {
            throw new ArgumentOutOfRangeException("Conversion failed", ex);
        }
    }

    /// <summary>
    /// Creates a new instance of a <see cref="Cid"/> class using the specified parameters.
    /// </summary>
    /// <param name="bytes">A byte array containing a Cid.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bytes"/> does not represent a Cid.</exception>
    public Cid(Span<byte> bytes)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(bytes.Length, 0);

        try
        {
            (byte Version, ulong Codec, IReadOnlyList<byte> Hash) result = ParseBytes(bytes.ToArray());
            Version = result.Version;
            Codec = result.Codec;
            Hash = result.Hash;
        }
        catch (Exception ex)
        {
            throw new ArgumentOutOfRangeException("Conversion failed", ex);
        }
    }

    /// <summary>
    /// Creates a new instance of a <see cref="Cid"/> class using the specified parameters.
    /// </summary>
    /// <param name="version">The Cid version.</param>
    /// <param name="codec">The codec used to encode the hash.</param>
    /// <param name="hash">The hash value(s).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hash"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="hash"/> is empty, or when <paramref name="version"/> is not 0 or 1.
    /// </exception>
    public Cid(byte version, ulong codec, byte[] hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentOutOfRangeException.ThrowIfZero(hash.Length);

        if (version is not 0 and not 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                string.Create(CultureInfo.InvariantCulture, $"Version {version} is unsupported."));
        }

        Version = version;
        Codec = codec;
        Hash = (byte[])hash.Clone();
    }

    /// <summary>
    /// Gets the Cid version.
    /// </summary>
    [JsonIgnore]
    public byte Version { get; }

    /// <summary>
    /// Gets the codec used to encode the hash(es).
    /// </summary>
    [JsonIgnore]
    public ulong Codec { get; }

    /// <summary>
    /// Gets the hash(es).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<byte> Hash { get; }

    /// <summary>
    /// Gets the value of the Content Identifier.
    /// </summary>
    [JsonPropertyName("cid")]
    public string Value => ToString();

    /// <summary>
    /// Returns a string that represents the current <see cref="Cid"/> object.
    /// </summary>
    /// <returns>A string representation of the current <see cref="Cid"/>.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "AT Proto normalizes the base32 used by CIDv1 to lower case.")]
    public override string ToString()
    {
        if (Version == 0)
        {
            // CIDv0 is base58btc, whose alphabet is case sensitive, so unlike the base32 used by CIDv1
            // the result cannot be case normalized without producing a different, unparsable identifier.
            return SimpleBase.Base58.Bitcoin.Encode(Hash.ToArray());
        }
        else if (Version == 1)
        {
            byte[] cidBytes = ToBytes();

            return $"b{SimpleBase.Base32.Rfc4648.Encode(cidBytes).ToLowerInvariant()}";
        }
        else
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Converts the CID to byte array.
    /// </summary>
    /// <returns>The CID as bytes.</returns>
    public byte[] ToBytes()
    {
        var result = new List<byte>();

        if (Version != 1)
        {
            return [];
        }

        result.Add(Version);
        result.AddRange(EncodeVarInt(Codec));
        result.AddRange(Hash);
        return [.. result];
    }

    /// <summary>
    /// Creates a <see cref="Cid"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>An instance of <see cref="Cid"/>. from <paramref name="s"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Cid(string s) => new(s);

    /// <summary>
    /// Creates a <see cref="Cid"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>An instance of <see cref="Cid"/>. from <paramref name="s"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Cid FromString(string s) => new(s);

    /// <summary>
    /// Gets a hash code for the current object.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        HashCode hashAlgorithm = default;

        hashAlgorithm.Add(Version);
        hashAlgorithm.Add(Codec);
        hashAlgorithm.AddBytes(Hash.ToArray());

        return hashAlgorithm.ToHashCode();
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="obj">An object to compare with this object.</param>
    /// <returns><see langword="true"/> if the current object is equal to the <paramref name="obj"/>; otherwise, <see langword="false" />.</returns>
    public override bool Equals(object? obj) => Equals(obj as Cid);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns><see langword="true"/> if the current object is equal to the <paramref name="other"/>; otherwise, <see langword="false" />.</returns>
    public bool Equals(Cid? other)
    {
        if (other is null)
        {
            return false;
        }

        // Optimization for a common success case.
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // If run-time types are not exactly the same, return false.
        if (GetType() != other.GetType())
        {
            return false;
        }

        // Return true if the fields match.
        return Version == other.Version &&
            Codec == other.Codec &&
            Hash.SequenceEqual(other.Hash);
    }

    /// <summary>
    /// Determines whether two specified <see cref="Cid"/>s the same value.
    /// </summary>
    /// <param name="lhs">The first <see cref="Cid"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="Cid"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Cid? lhs, Cid? rhs)
    {
        if (lhs is null)
        {
            if (rhs is null)
            {
                return true;
            }

            // Only the left side is null.
            return false;
        }
        // Equals handles case of null on right side.
        return lhs.Equals(rhs);
    }

    /// <summary>
    /// Determines whether two specified <see cref="Cid"/>s do not have the same value.
    /// </summary>
    /// <param name="lhs">The first <see cref="Cid"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="Cid"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is different from the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Cid? lhs, Cid? rhs) => !(lhs == rhs);

    private static (byte Version, ulong Codec, IReadOnlyList<byte> Hash) ParseBytes(byte[] bytes)
    {
        Span<byte> span = new(bytes);

        if (span.IsEmpty)
        {
            throw new ArgumentException("Value contains no data.", nameof(bytes));
        }

        byte version = span[0];

        if (version == 0)
        {
            Span<byte> multihash = span[1..];

            if (multihash.IsEmpty)
            {
                throw new ArgumentException("Value contains no multihash.", nameof(bytes));
            }

            return (version, 0x70, multihash.ToArray());
        }
        else if (version == 1)
        {
            (ulong codec, int codecLength) = DecodeVarInt(span[1..]);

            Span<byte> multihash = span[(1 + codecLength)..];

            if (multihash.IsEmpty)
            {
                throw new ArgumentException("Value contains no multihash.", nameof(bytes));
            }

            return new(version, codec, multihash.ToArray());
        }
        else
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Version {version} is unsupported."), nameof(bytes));
        }
    }

    private static byte[] EncodeVarInt(ulong value)
    {
        var bytes = new List<byte>();

        while (value >= 0x80)
        {
            bytes.Add((byte)(value | 0x80));
            value >>= 7;
        }

        bytes.Add((byte)value);
        return [.. bytes];
    }

    private static (ulong Value, int Length) DecodeVarInt(ReadOnlySpan<byte> bytes)
    {
        ulong value = 0;
        int shift = 0;
        int length = 0;

        foreach (byte b in bytes)
        {
            if (length == MaximumVarIntLength)
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Varint is longer than the maximum of {MaximumVarIntLength} bytes."),
                    nameof(bytes));
            }

            length++;
            value |= (ulong)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
            {
                return (value, length);
            }

            shift += 7;
        }

        throw new ArgumentException("Varint is truncated.", nameof(bytes));
    }

    /// <summary>
    /// Converts the string representation of an identifier to its <see cref="Cid"/> equivalent.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="s">A string containing the id to convert.</param>
    /// <param name="result">
    /// When this method returns contains the <see cref="Cid"/> equivalent of the
    /// string contained in s, or <see langword="null"/> if the conversion failed. The conversion fails if the <paramref name="s"/> parameter
    /// is <see langword="null"/> or empty, or is not of the current format. This parameter is passed uninitialized; any value originally
    /// supplied in result will be overwritten.
    /// </param>
    /// <returns><see langword="true"/> if <paramref name="s"/> was converted successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string? s, [NotNullWhen(true)] out Cid? result)
    {
        if (string.IsNullOrEmpty(s))
        {
            result = null;
            return false;
        }

        try
        {
            result = new Cid(s);
            return true;
        }
        catch (ArgumentException)
        {
            result = null;
            return false;
        }
    }
}
