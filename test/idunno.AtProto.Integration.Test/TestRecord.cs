// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

namespace idunno.AtProto.Integration.Test
{
    [ExcludeFromCodeCoverage]
    public record TestRecordValue : AtProtoRecordValue
    {
        public required string TestValue { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public record TestRecord : AtProtoRecord<TestRecordValue>
    {
        public TestRecord(AtUri uri, Cid cid, TestRecordValue value) : base(uri, cid, value)
        {
        }
    }
}
