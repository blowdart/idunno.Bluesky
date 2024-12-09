// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat
#pragma warning restore IDE0130
{
    /// <summary>
    /// Encapsulates a paged read only collection of chat log entries.
    /// </summary>
    public sealed class Logs : PagedReadOnlyCollection<LogBase>
    {
        /// <summary>
        /// Creates a new instance of <see cref="Logs"/>.
        /// </summary>
        /// <param name="list">The list of <see cref="LogBase"/> to create this instance from.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        public Logs(IList<LogBase> list, string? cursor = null) : base(list, cursor)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Logs"/>.
        /// </summary>
        /// <param name="collection">A collection of <see cref="LogBase"/> to create this instance from.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        public Logs(IEnumerable<LogBase> collection, string? cursor = null) : this(new List<LogBase>(collection), cursor)
        {
        }

        /// <summary>
        /// Creates a new instance of with an empty list.
        /// </summary>
        /// <param name="cursor">An optional cursor for pagination.</param>
        public Logs(string? cursor = null) : this(new List<LogBase>(), cursor)
        {
        }
    }
}
