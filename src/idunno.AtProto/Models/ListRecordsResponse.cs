// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Models
{
    internal record ListRecordsResponse<T>
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
