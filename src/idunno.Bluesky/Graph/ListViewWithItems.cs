// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// An addition context view of a Bluesky list, with items the list contains.
    /// </summary>
    public class ListViewWithItems : PagedViewReadOnlyCollection<ListItemView>
    {
        /// <summary>
        /// Creates a new instance of <see cref="ListViewWithItems"/>.
        /// </summary>
        /// <param name="list">A <see cref="ListView" /> of the list whose items will be in the collection.</param>
        /// <param name="items">A collection of <see cref="ListItemView"/>s of items in the list.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        public ListViewWithItems(ListView list, ICollection<ListItemView> items, string? cursor) : base(items, cursor)
        {
            List = list;
        }

        /// <summary>
        /// A <see cref="ListView" /> of the list whose items are in the collection.
        /// </summary>
        public ListView List { get; init; }
    }
}
