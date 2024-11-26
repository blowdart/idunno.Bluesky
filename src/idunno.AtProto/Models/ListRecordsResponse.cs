// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.AtProto.Models
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in ListRecords.")]
    internal sealed record ListRecordsResponse<T>
    {
        public ListRecordsResponse(IList<T> records, string? cursor)
        {
            Records = records;
            Cursor = cursor;
        }

        public IList<T> Records { get; set; }

        public string? Cursor { get; set; }
    }
}
