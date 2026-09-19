// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Labels;

/// <summary>
/// Metadata tags on an atproto record, published by the author within the record.
/// </summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(SelfLabels), typeDiscriminator: "com.atproto.label.defs#selfLabels")]
[SuppressMessage("Major Code Smell", "S4035:Classes implementing \"IEquatable<T>\" should be sealed", Justification = "Sealing the type would be a breaking change. Equals guards against asymmetric comparisons by requiring both instances to be of the same runtime type.")]
public class SelfLabels : IEquatable<SelfLabels>
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
#if NET9_0_OR_GREATER
    private readonly Lock _syncLock = new();
#else
    private readonly object _syncLock = new();
#endif

    List<SelfLabel> _values;

    /// <summary>
    /// Creates a new instance of <see cref="SelfLabels"/>.
    /// </summary>
    public SelfLabels()
    {
        _values = [];
    }

    /// <summary>
    /// Creates a new instance of <see cref="SelfLabels"/>.
    /// </summary>
    /// <param name="value">The label to be applied to the record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public SelfLabels(SelfLabel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _values = [value];
    }

    /// <summary>
    /// Creates a new instance of <see cref="SelfLabels"/>.
    /// </summary>
    /// <param name="values">The collection of labels applied to the record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="values"/> contains more than 10 items.</exception>
    [JsonConstructor]
    public SelfLabels(IReadOnlyList<SelfLabel> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(values.Count, 10);

        _values = [.. values];
    }

    /// <summary>
    /// The collection of self labels applied to the record.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when setting to <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when setting to a collection with more than 10 items.</exception>
    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<SelfLabel> Values
    {
        get => _values.AsReadOnly();

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Count, 10);
            lock (_syncLock)
            {
                _values = [.. value];
            }
        }
    }

    /// <summary>
    /// Returns a flag indicating whether the specified <paramref name="label"/> is present.
    /// </summary>
    /// <param name="label">The label to search for.</param>
    /// <returns>A flag indicating whether the label is present.</returns>
    public bool Contains(string label)
    {
        lock (_syncLock)
        {
            SelfLabel? value = (from existingLabel in Values where existingLabel.Value.Equals(label, StringComparison.Ordinal) select existingLabel).FirstOrDefault();
            return value is not null;
        }
    }

    /// <summary>
    /// Returns a flag indicating whether the specified <paramref name="label"/> is present.
    /// </summary>
    /// <param name="label">The label to search for.</param>
    /// <returns>A flag indicating whether the label is present.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is <see langword="null"/>, or the label's value is <see langword="null"/>.</exception>
    public bool Contains(SelfLabel label)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(label.Value);
        return Contains(label.Value);
    }

    /// <summary>
    /// Adds a <see cref="SelfLabel"/> with the specified <paramref name="name"/> if one does not already exist.
    /// </summary>
    /// <param name="name">The name of the <see cref="SelfLabel"/> to add.</param>
    public void AddLabel(string name)
    {
        lock (_syncLock)
        {
            if (!Contains(name))
            {
                List<string> values = [.. from existingLabel in _values select existingLabel.Value, name];

                List<SelfLabel> updatedLabels = [];
                foreach (string value in values)
                {
                    updatedLabels.Add(new SelfLabel(value));
                }

                _values = updatedLabels;
            }
        }
    }

    /// <summary>
    /// Adds a <see cref="SelfLabel"/> if one does not already exist.
    /// </summary>
    /// <param name="label">The <see cref="SelfLabel"/> to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> or the label's value is <see langword="null"/>.</exception>
    public void AddLabel(SelfLabel label)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(label.Value);

        AddLabel(label.Value);
    }

    /// <summary>
    /// Removes a <see cref="SelfLabel"/> with the specified <paramref name="name"/> if it exists.
    /// </summary>
    /// <param name="name">The name of the <see cref="SelfLabel"/> to remove.</param>
    public void RemoveLabel(string name)
    {
        lock (_syncLock)
        {
            if (Contains(name))
            {
                List<string> values = [.. from existingLabel in Values select existingLabel.Value];
                values.Remove(name);

                List<SelfLabel> updatedLabels = [];
                foreach (string value in values)
                {
                    updatedLabels.Add(new SelfLabel(value));
                }

                _values = updatedLabels;
            }
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="SelfLabels"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="SelfLabels"/> to compare against the current instance.</param>
    /// <returns><see langword="true" /> if <paramref name="other"/> is equal to the current instance, otherwise <see langword="false" />.</returns>
    /// <remarks>
    /// <para>Two instances are equal when they contain the same labels, in the same order.</para>
    /// </remarks>
    public bool Equals(SelfLabels? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || other.GetType() != GetType())
        {
            return false;
        }

        List<SelfLabel> snapshot;

        lock (_syncLock)
        {
            snapshot = [.. _values];
        }

        return snapshot.SequenceEqual(other.Values);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current instance.
    /// </summary>
    /// <param name="obj">The object to compare against the current instance.</param>
    /// <returns><see langword="true" /> if <paramref name="obj"/> is equal to the current instance, otherwise <see langword="false" />.</returns>
    public override bool Equals(object? obj) => Equals(obj as SelfLabels);

    /// <summary>
    /// Returns the hash code for the current instance.
    /// </summary>
    /// <returns>The hash code for the current instance.</returns>
    /// <remarks>
    /// <para>
    ///   The hash code is derived from the labels the instance currently contains, so it changes if the instance is mutated.
    ///   Do not mutate an instance while it is being used as a key in a hashed collection.
    /// </para>
    /// </remarks>
    [SuppressMessage("Major Code Smell", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "Value equality over a mutable collection requires the hash code to be derived from the same state, which is documented on the member.")]
    public override int GetHashCode()
    {
        HashCode hashCode = new();

        lock (_syncLock)
        {
            foreach (SelfLabel label in _values)
            {
                hashCode.Add(label);
            }
        }

        return hashCode.ToHashCode();
    }
}