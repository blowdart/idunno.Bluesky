// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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

    // The multicodec identifier for DAG-CBOR encoded content.
    private const ulong DagCborCodec = 0x71;

    // The multihash identifier for SHA-256.
    private const byte Sha256MultihashCode = 0x12;

    // The hash is never handed out directly, so a Cid cannot be changed after it is created and the values derived
    // from it below can be cached.
    private readonly byte[] _hash;

    private IReadOnlyList<byte>? _hashView;
    private string? _value;
    private int _hashCode;
    private bool _hashCodeCalculated;

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
                _hash = bytes;
            }
            else
            {
                // CIDv1 - multibase encoded

                byte[] bytes = SimpleBase.Multibase.Decode(value);

                (byte Version, ulong Codec, byte[] Hash) result = ParseBytes(bytes);

                Version = result.Version;
                Codec = result.Codec;
                _hash = result.Hash;
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
            (byte Version, ulong Codec, byte[] Hash) result = ParseBytes(bytes);
            Version = result.Version;
            Codec = result.Codec;
            _hash = result.Hash;
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
            (byte Version, ulong Codec, byte[] Hash) result = ParseBytes(bytes);
            Version = result.Version;
            Codec = result.Codec;
            _hash = result.Hash;
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
        _hash = (byte[])hash.Clone();
    }

    /// <summary>
    /// Creates a new instance of a version 1 <see cref="Cid"/> which takes ownership of <paramref name="hash"/>, rather than copying it.
    /// </summary>
    /// <param name="codec">The codec used to encode the hash.</param>
    /// <param name="hash">The multihash, which must not be changed, or used elsewhere, afterwards.</param>
    private Cid(ulong codec, byte[] hash)
    {
        Version = 1;
        Codec = codec;
        _hash = hash;
    }

    /// <summary>
    /// Creates the <see cref="Cid"/> which identifies the specified DAG-CBOR encoded <paramref name="content"/>.
    /// </summary>
    /// <param name="content">The DAG-CBOR encoded block to create a content identifier for.</param>
    /// <returns>A version 1 <see cref="Cid"/> over the SHA-256 hash of <paramref name="content"/>, using the DAG-CBOR codec.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="content"/> is empty.</exception>
    /// <remarks>
    /// <para>The content identifier is calculated over the bytes exactly as supplied. As DAG-CBOR is a deterministic encoding
    /// re-encoding a decoded block before calling this method may produce a different, and incorrect, identifier.</para>
    /// </remarks>
    public static Cid FromDagCbor(ReadOnlySpan<byte> content)
    {
        ArgumentOutOfRangeException.ThrowIfZero(content.Length);

        // A multihash is the hash function identifier, the digest length, then the digest itself.
        byte[] multihash = new byte[2 + SHA256.HashSizeInBytes];
        multihash[0] = Sha256MultihashCode;
        multihash[1] = SHA256.HashSizeInBytes;
        SHA256.HashData(content, multihash.AsSpan(2));

        // The multihash was created here, so the Cid can own it rather than copy it.
        return new Cid(DagCborCodec, multihash);
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
    public IReadOnlyList<byte> Hash => _hashView ??= Array.AsReadOnly(_hash);

    /// <summary>
    /// Gets the value of the Content Identifier.
    /// </summary>
    [JsonPropertyName("cid")]
    public string Value => ToString();

    /// <summary>
    /// Returns a string that represents the current <see cref="Cid"/> object.
    /// </summary>
    /// <returns>A string representation of the current <see cref="Cid"/>.</returns>
    public override string ToString() => _value ??= Format();

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "AT Proto normalizes the base32 used by CIDv1 to lower case.")]
    private string Format()
    {
        if (Version == 0)
        {
            // CIDv0 is base58btc, whose alphabet is case sensitive, so unlike the base32 used by CIDv1
            // the result cannot be case normalized without producing a different, unparsable identifier.
            return SimpleBase.Base58.Bitcoin.Encode(_hash);
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
        if (Version != 1)
        {
            return [];
        }

        int codecLength = VarIntLength(Codec);
        byte[] result = new byte[1 + codecLength + _hash.Length];

        result[0] = Version;
        EncodeVarInt(Codec, result.AsSpan(1, codecLength));
        _hash.CopyTo(result, 1 + codecLength);

        return result;
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
    [SuppressMessage("Major Bug", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "The cached hash code is computed solely from readonly state, so it never changes once calculated.")]
    public override int GetHashCode()
    {
        if (!Volatile.Read(ref _hashCodeCalculated))
        {
            HashCode hashAlgorithm = default;

            hashAlgorithm.Add(Version);
            hashAlgorithm.Add(Codec);
            hashAlgorithm.AddBytes(_hash);

            _hashCode = hashAlgorithm.ToHashCode();
            Volatile.Write(ref _hashCodeCalculated, true);
        }

        return _hashCode;
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
            _hash.AsSpan().SequenceEqual(other._hash);
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

    private static (byte Version, ulong Codec, byte[] Hash) ParseBytes(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty)
        {
            throw new ArgumentException("Value contains no data.", nameof(span));
        }

        byte version = span[0];

        if (version == 0)
        {
            ReadOnlySpan<byte> multihash = span[1..];

            if (multihash.IsEmpty)
            {
                throw new ArgumentException("Value contains no multihash.", nameof(span));
            }

            return (version, 0x70, multihash.ToArray());
        }
        else if (version == 1)
        {
            (ulong codec, int codecLength) = DecodeVarInt(span[1..]);

            ReadOnlySpan<byte> multihash = span[(1 + codecLength)..];

            if (multihash.IsEmpty)
            {
                throw new ArgumentException("Value contains no multihash.", nameof(span));
            }

            return new(version, codec, multihash.ToArray());
        }
        else
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Version {version} is unsupported."), nameof(span));
        }
    }

    private static int VarIntLength(ulong value)
    {
        int length = 1;

        while (value >= 0x80)
        {
            value >>= 7;
            length++;
        }

        return length;
    }

    private static void EncodeVarInt(ulong value, Span<byte> destination)
    {
        int i = 0;

        while (value >= 0x80)
        {
            destination[i++] = (byte)(value | 0x80);
            value >>= 7;
        }

        destination[i] = (byte)value;
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
