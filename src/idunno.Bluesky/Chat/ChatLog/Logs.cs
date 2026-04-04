// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using idunno.AtProto;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// Encapsulates a paged read only collection of chat log entries.
/// </summary>
/// <param name="list">The list of <see cref="LogBase"/> to create this instance from.</param>
/// <param name="cursor">An optional cursor for pagination.</param>
/// <remarks>
/// <para>Creates a new instance of <see cref="Logs"/></para>
/// </remarks>
[SuppressMessage("Design", "CA1724:Type names should not match namespaces", Justification = "Class name matches lexicon definition.")]
public sealed class Logs(IList<LogBase> list, string? cursor = null) : PagedReadOnlyCollection<LogBase>(list, cursor)
{

    /// <summary>
    /// Creates a new instance of <see cref="Logs"/>.
    /// </summary>
    /// <param name="collection">A collection of <see cref="LogBase"/> to create this instance from.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    public Logs(IEnumerable<LogBase> collection, string? cursor = null) : this([.. collection], cursor)
    {
    }

    /// <summary>
    /// Creates a new instance of with an empty list.
    /// </summary>
    /// <param name="cursor">An optional cursor for pagination.</param>
    public Logs(string? cursor = null) : this([], cursor)
    {
    }
}
