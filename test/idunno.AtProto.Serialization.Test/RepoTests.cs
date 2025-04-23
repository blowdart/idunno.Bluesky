// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Duende.IdentityModel.OidcClient;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo.Models;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class RepoTests
    {
        [Fact]
        public void CreateRecordResponseDeserializesCorrectlyWithNoValidationStatusAndSourceGeneratedContext()
        {
            string json = """
            {
                "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.verification/3lngovulx4e2k",
                "cid": "bafyreifeaujt3xylpukpmz6uzw3imishgeqatbtdbdqa5ywoj3ydznz4sm",
                "commit": {
                    "cid": "bafyreibgvuzm2gea6gxg5gzr4w737dlstdp5a2nur2boiaqumrk23kt5qq",
                    "rev": "3lngovum7vm2k"
                }
            }
            """;

            CreateRecordResponse? actual= JsonSerializer.Deserialize(json, typeof(CreateRecordResponse), AtProtoServer.AtProtoJsonSerializerOptions) as CreateRecordResponse;

            Assert.NotNull(actual);
        }
    }
}
