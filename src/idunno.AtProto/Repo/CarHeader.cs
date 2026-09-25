// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo;

/// <summary>
/// Describes the header of a CAR version 1 file.
/// </summary>
public sealed class CarHeader
{
    /// <summary>
    /// Creates a new <see cref="CarHeader"/>.
    /// </summary>
    /// <param name="roots">The root content identifiers in the CAR.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="roots"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="roots"/> contains a <see langword="null"/> value.</exception>
    public CarHeader(IEnumerable<Cid> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);

        List<Cid> copiedRoots = [.. roots];
        if (copiedRoots.Any(static root => root is null))
        {
            throw new ArgumentException("The roots collection cannot contain null values.", nameof(roots));
        }

        Roots = copiedRoots.AsReadOnly();
    }

    /// <summary>
    /// Gets the CAR roots.
    /// </summary>
    public IReadOnlyList<Cid> Roots { get; }

    /// <summary>
    /// Gets the CAR format version.
    /// </summary>
    public const ulong Version = 1;
}
