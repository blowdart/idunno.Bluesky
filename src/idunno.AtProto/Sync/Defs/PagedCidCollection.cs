// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Sync;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A read only list of <see cref="Cid"/>s, with an optional cursor for pagination.
/// </summary>
/// <param name="list">The list of <see cref="Cid"/>s to wrap.</param>
/// <param name="cursor">An optional cursor for pagination.</param>
/// <remarks>
/// <para>
/// Creates a new instance of <see cref="PagedCidCollection"/>.
/// </para>
/// </remarks>
public sealed class PagedCidCollection(IList<Cid> list, string? cursor = null) : ReadOnlyCollection<Cid>(list)
{
    /// <summary>
    /// Gets an optional cursor returned by the underlying API, used to retrieve the next page of results.
    /// </summary>
    public string? Cursor { get; } = cursor;
}
