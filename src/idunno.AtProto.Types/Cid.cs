// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides an object representation of the AT Proto implementation of a Content Identifier (CID).
    /// </summary>
    /// <remarks>
    /// <para>See https://github.com/multiformats/cid for specification.</para>
    /// </remarks>
    [JsonConverter(typeof(Json.CidConverter))]
    public sealed class Cid : IEquatable<Cid>
    {
        /// <summary>
        /// Creates a new instance of a <see cref="Cid"/> class using the specified parameters.
        /// </summary>
        /// <param name="value">The value of the content identifier.</param>
        /// <exception cref="ArgumentNullException">Thrown when the provided value is null.</exception>
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/> is null or empty.</exception>
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/> is null or empty.</exception>
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
        public Cid(byte version, ulong codec, byte[] hash)
        {
            Version = version;
            Codec = codec;
            Hash = hash;
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

        /// <inheritdoc/>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "AT Proto normalizes to lower case")]
        public override string ToString()
        {
            if (Version == 0)
            {
                return SimpleBase.Base58.Bitcoin.Encode(Hash.ToArray()).ToLowerInvariant();
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
        /// <returns>An instance of <see cref="Cid"/>. from the specified string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Cid(string s) => new(s);

        /// <summary>
        /// Creates a <see cref="Cid"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>An instance of <see cref="Cid"/>. from the specified string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Cid FromString(string s) => new(s);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            HashCode hashAlgorithm = default;

            hashAlgorithm.Add(Version);
            hashAlgorithm.Add(Codec);
            hashAlgorithm.AddBytes(Hash.ToArray());

            return hashAlgorithm.ToHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as Cid);

        /// <inheritdoc/>
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
            return  Version == other.Version &&
                Codec == other.Codec &&
                Hash.SequenceEqual(other.Hash);
        }

        private static (byte Version, ulong Codec, IReadOnlyList<byte> Hash) ParseBytes(byte[] bytes)
        {
            Span<byte> span = new (bytes);

            byte version = span[0];

            if (version == 0)
            {
                return (version, 0x70, span[1..].ToArray());
            }
            else if (version == 1)
            {
                (ulong codec, int codecLength) = DecodeVarInt(span[1..]);

                return new(version, codec, span[(1 + codecLength)..].ToArray());
            }
            else
            {
                throw new ArgumentException($"Version {BitConverter.ToString(new byte[version])} is unsupported");
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
                length++;
                value |= (ulong)(b & 0x7F) << shift;

                if ((b & 0x80) == 0)
                {
                    break;
                }

                shift += 7;
            }

            return (value, length);
        }
    }
}
