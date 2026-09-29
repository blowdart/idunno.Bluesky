// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace idunno.AtProto.Firehose;

/// <summary>
/// The names of the DAG-CBOR map fields a decoder reads. Other fields are skipped without being allocated.
/// </summary>
internal sealed class CborFieldNames
{
    private readonly string[] _names;
    private readonly byte[][] _encodedNames;

    /// <summary>
    /// Creates a new instance of <see cref="CborFieldNames"/>.
    /// </summary>
    /// <param name="names">The field names.</param>
    public CborFieldNames(params string[] names)
    {
        _names = [.. names.Distinct(StringComparer.Ordinal)];
        _encodedNames = [.. _names.Select(Encoding.UTF8.GetBytes)];
    }

    /// <summary>
    /// Creates a set containing these names and <paramref name="other"/>'s.
    /// </summary>
    /// <param name="other">The names to add.</param>
    /// <returns>The combined names.</returns>
    public CborFieldNames Union(CborFieldNames other) => new([.. _names, .. other._names]);

    /// <summary>
    /// Finds the name whose UTF-8 encoding is <paramref name="encodedName"/>.
    /// </summary>
    /// <param name="encodedName">The UTF-8 encoded field name.</param>
    /// <returns>The name, or <see langword="null"/> if it is not one of these names.</returns>
    public string? Find(ReadOnlySpan<byte> encodedName)
    {
        for (int i = 0; i < _encodedNames.Length; i++)
        {
            if (encodedName.SequenceEqual(_encodedNames[i]))
            {
                return _names[i];
            }
        }

        return null;
    }
}
