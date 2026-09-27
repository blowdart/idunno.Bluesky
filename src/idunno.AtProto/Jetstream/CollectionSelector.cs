// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// A collection to filter jetstream commit events on, either an exact <see cref="Nsid"/> or, with
/// <see cref="JetstreamProtocolVersion.V2"/>, a namespace ending in <c>.*</c> which matches every collection under it.
/// </summary>
/// <remarks>
/// <para>A wildcard is validated as the server validates it, by checking the namespace before the <c>.*</c> is one a
/// collection could sit under. This is looser than <see cref="Nsid"/> itself, which is why a wildcard cannot be
/// expressed as an <see cref="Nsid"/>: <c>app.bsky.*</c> is accepted here, although <c>app.bsky</c> is not a valid NSID.</para>
/// <para>Only a trailing <c>.*</c> is a wildcard. A value such as <c>app.bsky.fo*</c> has no <c>.</c> before the
/// <c>*</c>, so it is validated as an ordinary NSID and rejected, and a leading or embedded <c>*</c> is never a wildcard.</para>
/// <para>A <see cref="JetstreamProtocolVersion.V1"/> server also accepts the wildcard form.</para>
/// </remarks>
[JsonConverter(typeof(CollectionSelectorConverter))]
public sealed class CollectionSelector : IEquatable<CollectionSelector>
{
    private const string WildcardSuffix = ".*";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly string _value;

    /// <summary>
    /// Creates a new instance of <see cref="CollectionSelector"/> from the specified string.
    /// </summary>
    /// <param name="s">The collection, or namespace ending in <c>.*</c>, to filter on.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="s"/> is <see langword="null"/>, empty or white space.</exception>
    /// <exception cref="NsidFormatException">Thrown when <paramref name="s"/> is neither a valid NSID nor a valid wildcard.</exception>
    [JsonConstructor]
    public CollectionSelector(string s)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(s);

        if (s.EndsWith(WildcardSuffix, StringComparison.Ordinal))
        {
            string authority = s[..^WildcardSuffix.Length];

            // The namespace is checked by appending a name a collection could have and validating the whole thing,
            // which is how the server checks it, so the grammar is not restated here and cannot drift from Nsid.
            if (!Nsid.TryParse(authority + ".x", out _))
            {
                throw new NsidFormatException($"{s} is not a valid collection wildcard.");
            }

            _value = s;
            IsWildcard = true;

            return;
        }

        Collection = new Nsid(s);
        _value = Collection.ToString();
    }

    /// <summary>
    /// Creates a new instance of <see cref="CollectionSelector"/> which matches <paramref name="collection"/> exactly.
    /// </summary>
    /// <param name="collection">The collection to filter on.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="collection"/> is <see langword="null"/>.</exception>
    public CollectionSelector(Nsid collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        Collection = collection;
        _value = collection.ToString();
    }

    /// <summary>
    /// Gets a value indicating whether this instance matches every collection under a namespace rather than one collection.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if this instance ends in <c>.*</c>, otherwise <see langword="false"/>.
    /// </value>
    public bool IsWildcard { get; }

    /// <summary>
    /// Gets the collection this instance matches, or <see langword="null"/> when <see cref="IsWildcard"/> is <see langword="true"/>.
    /// </summary>
    /// <value>
    /// The collection this instance matches, or <see langword="null"/> when <see cref="IsWildcard"/> is <see langword="true"/>.
    /// </value>
    public Nsid? Collection { get; }

    /// <summary>
    /// Returns a string representation of the current <see cref="CollectionSelector"/> instance.
    /// </summary>
    /// <returns>A string representation of the current <see cref="CollectionSelector"/> instance.</returns>
    public override string ToString() => _value;

    /// <summary>
    /// Creates a <see cref="CollectionSelector"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>A <see cref="CollectionSelector"/> created from the specified string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CollectionSelector(string s) => new(s);

    /// <summary>
    /// Creates a <see cref="CollectionSelector"/> which matches <paramref name="collection"/> exactly.
    /// </summary>
    /// <param name="collection">The collection to convert.</param>
    /// <returns>A <see cref="CollectionSelector"/> created from the specified collection.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CollectionSelector(Nsid collection) => new(collection);

    /// <summary>
    /// Creates a <see cref="CollectionSelector"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>A <see cref="CollectionSelector"/> created from the specified string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollectionSelector FromString(string s) => new(s);

    /// <summary>
    /// Creates a <see cref="CollectionSelector"/> which matches <paramref name="collection"/> exactly.
    /// </summary>
    /// <param name="collection">The collection to convert.</param>
    /// <returns>A <see cref="CollectionSelector"/> created from the specified collection.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollectionSelector FromNsid(Nsid collection) => new(collection);

    /// <summary>
    /// Converts the string representation of a collection filter to its <see cref="CollectionSelector"/> equivalent.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="s">A string containing the collection filter to convert.</param>
    /// <param name="result">
    /// When this method returns contains the <see cref="CollectionSelector"/> equivalent of <paramref name="s"/>, or
    /// <see langword="null"/> if the conversion failed. This parameter is passed uninitialized; any value originally
    /// supplied in result will be overwritten.
    /// </param>
    /// <returns><see langword="true"/> if <paramref name="s"/> was converted successfully, otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string s, out CollectionSelector? result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = null;

            return false;
        }

        try
        {
            result = new CollectionSelector(s);

            return true;
        }
        catch (NsidFormatException)
        {
            result = null;

            return false;
        }
    }

    /// <summary>
    /// Returns the hash code for this <see cref="CollectionSelector"/>.
    /// </summary>
    /// <returns>The hash code for this <see cref="CollectionSelector"/>.</returns>
    public override int GetHashCode() => _value.GetHashCode(StringComparison.Ordinal);

    /// <summary>
    /// Indicates whether an object is equal to this <see cref="CollectionSelector"/>.
    /// </summary>
    /// <param name="obj">An object to compare to this <see cref="CollectionSelector"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="obj"/> is a <see cref="CollectionSelector"/> with the same value, otherwise <see langword="false"/>.</returns>
    public override bool Equals(object? obj) => Equals(obj as CollectionSelector);

    /// <summary>
    /// Indicates whether this <see cref="CollectionSelector"/> equals another.
    /// </summary>
    /// <param name="other">A <see cref="CollectionSelector"/> or <see langword="null"/> to compare to this <see cref="CollectionSelector"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="other"/> has the same value as this instance, otherwise <see langword="false"/>.</returns>
    public bool Equals(CollectionSelector? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(_value, other._value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether two specified <see cref="CollectionSelector"/>s have the same value.
    /// </summary>
    /// <param name="lhs">The first <see cref="CollectionSelector"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="CollectionSelector"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs"/>, otherwise <see langword="false"/>.</returns>
    public static bool operator ==(CollectionSelector? lhs, CollectionSelector? rhs)
    {
        if (lhs is null)
        {
            return rhs is null;
        }

        return lhs.Equals(rhs);
    }

    /// <summary>
    /// Determines whether two specified <see cref="CollectionSelector"/>s do not have the same value.
    /// </summary>
    /// <param name="lhs">The first <see cref="CollectionSelector"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="CollectionSelector"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is different to the value of <paramref name="rhs"/>, otherwise <see langword="false"/>.</returns>
    public static bool operator !=(CollectionSelector? lhs, CollectionSelector? rhs) => !(lhs == rhs);
}
