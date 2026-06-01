// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace idunno.AtProto;

/// <summary>
/// Represents a Atproto byte array.
/// </summary>
[JsonConverter(typeof(Json.ByteConverter))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class Bytes : IEquatable<Bytes>
{
    private readonly byte[] _bytes;

    private readonly string _base64Encoded;

    /// <summary>
    /// Creates a new instance of <see cref="Bytes"/> from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array to convert.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="bytes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="bytes"/> length is 0.</exception>
    public Bytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfZero(bytes.Length);

        _bytes = (byte[])bytes.Clone();
        _base64Encoded = Convert.ToBase64String(_bytes);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Bytes"/> class from a base64 encoded string.
    /// </summary>
    /// <param name="s">The Base64 string to convert.</param>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="s"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="FormatException">Thrown when the <paramref name="s"/> is not a valid Base64 format.</exception>
    [JsonConstructor]
    public Bytes(string s)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(s);

        _bytes = Convert.FromBase64String(s);

        // As Convert.FromBaseString() ignores spaces, reconvert back to a normalized form to ensure the string is in a consistent format.
        _base64Encoded = Convert.ToBase64String(_bytes);
    }

    /// <summary>
    /// Gets the value of this instance in bytes.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("$bytes")]
    public ICollection<byte> Value => [.. _bytes];

    /// <summary>
    /// Gets the base64 encoded value of this instance.
    /// </summary>
    [JsonIgnore]
    public string Base64EncodedValue => _base64Encoded;

    /// <summary>
    /// Serializes the bytes of this instance to a base64 encoded string.
    /// </summary>
    /// <returns>A base64 encoded string representation of the bytes.</returns>
    public override string ToString()
    {
        return _base64Encoded;
    }

    /// <summary>
    /// Gets the value of this instance as an array of bytes.
    /// </summary>
    /// <returns>The byte value of this instance.</returns>
    public byte[] ToBytes()
    {
        return [.. _bytes];
    }

    /// <summary>
    /// Creates a <see cref="Bytes"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>An <see cref="Bytes"/> from the specified string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Bytes(string s) => new(s);

    /// <summary>
    /// Creates a <see cref="Bytes"/> from the specified string.
    /// </summary>
    /// <param name="bytes">The byte array to convert.</param>
    /// <returns>An <see cref="Bytes"/> from the specified byte array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Bytes(byte[] bytes) => new(bytes);

    /// <summary>
    /// Indicates where this <see cref="Bytes"/> equals another.
    /// </summary>
    /// <param name="other">A <see cref="Bytes"/> or <see langword="null"/> to compare to this <see cref="Bytes"/>.</param>
    /// <returns>
    /// <see langword="true"/> if this <see cref="Bytes"/> and the specified <paramref name="other"/>> refer to the same object,
    /// this Bytes and the specified obj are both the same type of object and those objects are equal,
    /// or if this Bytes and the specified obj are both <see langword="null"/>, otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals([NotNullWhen(true)] Bytes? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // Using the FixedTimeEquals method to prevent timing attacks when comparing byte arrays as a just in case, even though
        // the bytes may not represent cryptographic material.
        return CryptographicOperations.FixedTimeEquals(new ReadOnlySpan<byte>(_bytes), new ReadOnlySpan<byte>(other._bytes));
    }

    /// <summary>
    /// Indicates where an object is equal to this <see cref="Bytes"/>.
    /// </summary>
    /// <param name="obj">An object to compare to this <see cref="Bytes"/>.</param>
    /// <returns>
    /// <see langword="true"/> if this <see cref="Bytes"/> and the specified <paramref name="obj"/>> refer to the same object,
    /// this Bytes and the specified obj are both the same type of object and those objects are equal,
    /// or if this Bytes and the specified obj are both <see langword="null"/>, otherwise, <see langword="false"/>.
    /// </returns>
    public override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as Bytes);

    /// <summary>
    /// Returns the hash code for this <see cref="Bytes"/>.
    /// </summary>
    /// <returns>The hash code for this <see cref="Bytes"/>.</returns>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_base64Encoded);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString();
}
