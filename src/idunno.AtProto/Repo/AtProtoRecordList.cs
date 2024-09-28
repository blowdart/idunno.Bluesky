// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    public class AtProtoRecordList<T> where T : AtProtoRecord
    {
        [JsonConstructor]
        public AtProtoRecordList(IReadOnlyCollection<T> records, string? cursor)
        {
            Records = records;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Cursor { get; }

        [JsonInclude]
        public IReadOnlyCollection<T> Records { get; internal set; }
    }
}
