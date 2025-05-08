// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

namespace idunno.AtProto.Integration.Test
{
    [ExcludeFromCodeCoverage]
    public record TestRecord : AtProtoRecord
    {
        public required string TestValue { get; set; }
    }
}
