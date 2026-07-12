// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using GermNetwork.Com;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Serialization.Test;

public class GermNetworkTests
{
    [Fact]
    public void GermNetworkDeclarationDeserializesCorrectly()
    {
        var json = """
            {
                "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/com.germnetwork.declaration/self",
                "cid": "bafyreifovknw2emzxmjn2m7oeajmwul55gqk2h5y4wqj5kapfoasnowjji",
                "value": {
                    "$type": "com.germnetwork.declaration",
                    "version": "1.1.0",
                    "messageMe": {
                        "messageMeUrl": "https://landing.ger.mx/newUser",
                        "showButtonTo": "usersIFollow"
                    },
                    "currentKey": {
                        "$bytes": "A6gSirvoepY1Qw9zIiAyc4XnNll429PIOo4jPqYbXozY"
                    },
                    "keyPackage": {
                        "$bytes": "AGVi79PcKGsrj3IV3wuED6zEtoqBVr8tFrDVpB+/KKP+gZP9AcqrO+U2AOesz5FuiyHAfOTDTBEbT32f5Lr1+Qz/AaUAAQN2BfczHFWdiJr8qGtldB7PSnx1TE4huL44Iyvq5W/afQICAAAB/wE5AAEABQABAAMg17W1o+mpxYh+1KXKaHDAcrMbLt4VeCaKBK1HjVOwyVAgjYV8QfxTOHUjMcWUIi63ryvMlvDVByqpHklODdYcqgwg81NlRQ7C9dhapCOabaSHCNsc+PziH/fpGZ+tLZhU620AASEDdgX3MxxVnYia/KhrZXQez0p8dUxOIbi+OCMr6uVv2n0CAAEKAAIABwAFAAEAAwAAAgABAQAAAABp8lCOAAAAAGvThA4AQECGXXYuDVO+Jq2+PG6eO+znZ4BDPs/n3baXx1/FUGDsXbGbwqxcHUibxwj7yEI4O9e2JI4K+wwM9WtV/PcKK40EAEBAiQElfHbD2t1KoKC3sWrl5pY7Qwhebesf2z6KiGE8cnF4jd6NN9g7DDcAU11NQmba1kDNwjvyQnQy10rCrcgiBwAt5nIBaTxRcrtGa5VH+okfvxvFr7L6eE+xiGcNZ9A4T/Kmy9xaGReZ1aNOYcMdO0KSOYv34WzEZYOyGAQa0FkA"
                    }
                }
            }
            """;

        AtProtoRepositoryRecord<Declaration>? declarationRecord = JsonSerializer.Deserialize<AtProtoRepositoryRecord<Declaration>>(json, options: JsonSerializerOptions.Web);

        Assert.NotNull(declarationRecord);
        Assert.Equal(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/com.germnetwork.declaration/self"), declarationRecord!.Uri);
        Assert.Equal(new Cid("bafyreifovknw2emzxmjn2m7oeajmwul55gqk2h5y4wqj5kapfoasnowjji"), declarationRecord.Cid);
        Assert.NotNull(declarationRecord.Value);

        Assert.IsType<Declaration>(declarationRecord.Value);
        Assert.Equal("1.1.0", declarationRecord.Value!.Version);
        Assert.Equal(new Uri("https://landing.ger.mx/newUser"), declarationRecord.Value.MessageMe!.MessageMeUrl);
        Assert.Equal("usersIFollow", declarationRecord.Value.MessageMe.ShowButtonTo);
        Assert.Equal([3, 168, 18, 138, 187, 232, 122, 150, 53, 67, 15, 115, 34, 32, 50, 115, 133, 231, 54, 89, 120, 219, 211, 200, 58, 142, 35, 62, 166, 27, 94, 140, 216], declarationRecord.Value.CurrentKey.Value!);
        Assert.Equal([0, 101, 98, 239, 211, 220, 40, 107, 43, 143, 114, 21, 223, 11, 132, 15, 172, 196, 182, 138, 129, 86, 191, 45, 22, 176, 213, 164, 31, 191, 40, 163, 254, 129, 147, 253, 1, 202, 171, 59, 229, 54, 0, 231, 172, 207, 145, 110, 139, 33, 192, 124, 228, 195, 76, 17, 27, 79, 125, 159, 228, 186, 245, 249, 12, 255, 1, 165, 0, 1, 3, 118, 5, 247, 51, 28, 85, 157, 136, 154, 252, 168, 107, 101, 116, 30, 207, 74, 124, 117, 76, 78, 33, 184, 190, 56, 35, 43, 234, 229, 111, 218, 125, 2, 2, 0, 0, 1, 255, 1, 57, 0, 1, 0, 5, 0, 1, 0, 3, 32, 215, 181, 181, 163, 233, 169, 197, 136, 126, 212, 165, 202, 104, 112, 192, 114, 179, 27, 46, 222, 21, 120, 38, 138, 4, 173, 71, 141, 83, 176, 201, 80, 32, 141, 133, 124, 65, 252, 83, 56, 117, 35, 49, 197, 148, 34, 46, 183, 175, 43, 204, 150, 240, 213, 7, 42, 169, 30, 73, 78, 13, 214, 28, 170, 12, 32, 243, 83, 101, 69, 14, 194, 245, 216, 90, 164, 35, 154, 109, 164, 135, 8, 219, 28, 248, 252, 226, 31, 247, 233, 25, 159, 173, 45, 152, 84, 235, 109, 0, 1, 33, 3, 118, 5, 247, 51, 28, 85, 157, 136, 154, 252, 168, 107, 101, 116, 30, 207, 74, 124, 117, 76, 78, 33, 184, 190, 56, 35, 43, 234, 229, 111, 218, 125, 2, 0, 1, 10, 0, 2, 0, 7, 0, 5, 0, 1, 0, 3, 0, 0, 2, 0, 1, 1, 0, 0, 0, 0, 105, 242, 80, 142, 0, 0, 0, 0, 107, 211, 132, 14, 0, 64, 64, 134, 93, 118, 46, 13, 83, 190, 38, 173, 190, 60, 110, 158, 59, 236, 231, 103, 128, 67, 62, 207, 231, 221, 182, 151, 199, 95, 197, 80, 96, 236, 93, 177, 155, 194, 172, 92, 29, 72, 155, 199, 8, 251, 200, 66, 56, 59, 215, 182, 36, 142, 10, 251, 12, 12, 245, 107, 85, 252, 247, 10, 43, 141, 4, 0, 64, 64, 137, 1, 37, 124, 118, 195, 218, 221, 74, 160, 160, 183, 177, 106, 229, 230, 150, 59, 67, 8, 94, 109, 235, 31, 219, 62, 138, 136, 97, 60, 114, 113, 120, 141, 222, 141, 55, 216, 59, 12, 55, 0, 83, 93, 77, 66, 102, 218, 214, 64, 205, 194, 59, 242, 66, 116, 50, 215, 74, 194, 173, 200, 34, 7, 0, 45, 230, 114, 1, 105, 60, 81, 114, 187, 70, 107, 149, 71, 250, 137, 31, 191, 27, 197, 175, 178, 250, 120, 79, 177, 136, 103, 13, 103, 208, 56, 79, 242, 166, 203, 220, 90, 25, 23, 153, 213, 163, 78, 97, 195, 29, 59, 66, 146, 57, 139, 247, 225, 108, 196, 101, 131, 178, 24, 4, 26, 208, 89, 0], declarationRecord.Value.KeyPackage!.Value);
    }

    [Fact]
    public void GermNetworkDeclarationDeserializesCorrectlyWithSerializerOptions()
    {
        var json = """
            {
                "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/com.germnetwork.declaration/self",
                "cid": "bafyreifovknw2emzxmjn2m7oeajmwul55gqk2h5y4wqj5kapfoasnowjji",
                "value": {
                    "$type": "com.germnetwork.declaration",
                    "version": "1.1.0",
                    "messageMe": {
                        "messageMeUrl": "https://landing.ger.mx/newUser",
                        "showButtonTo": "usersIFollow"
                    },
                    "currentKey": {
                        "$bytes": "A6gSirvoepY1Qw9zIiAyc4XnNll429PIOo4jPqYbXozY"
                    },
                    "keyPackage": {
                        "$bytes": "AGVi79PcKGsrj3IV3wuED6zEtoqBVr8tFrDVpB+/KKP+gZP9AcqrO+U2AOesz5FuiyHAfOTDTBEbT32f5Lr1+Qz/AaUAAQN2BfczHFWdiJr8qGtldB7PSnx1TE4huL44Iyvq5W/afQICAAAB/wE5AAEABQABAAMg17W1o+mpxYh+1KXKaHDAcrMbLt4VeCaKBK1HjVOwyVAgjYV8QfxTOHUjMcWUIi63ryvMlvDVByqpHklODdYcqgwg81NlRQ7C9dhapCOabaSHCNsc+PziH/fpGZ+tLZhU620AASEDdgX3MxxVnYia/KhrZXQez0p8dUxOIbi+OCMr6uVv2n0CAAEKAAIABwAFAAEAAwAAAgABAQAAAABp8lCOAAAAAGvThA4AQECGXXYuDVO+Jq2+PG6eO+znZ4BDPs/n3baXx1/FUGDsXbGbwqxcHUibxwj7yEI4O9e2JI4K+wwM9WtV/PcKK40EAEBAiQElfHbD2t1KoKC3sWrl5pY7Qwhebesf2z6KiGE8cnF4jd6NN9g7DDcAU11NQmba1kDNwjvyQnQy10rCrcgiBwAt5nIBaTxRcrtGa5VH+okfvxvFr7L6eE+xiGcNZ9A4T/Kmy9xaGReZ1aNOYcMdO0KSOYv34WzEZYOyGAQa0FkA"
                    }
                }
            }
            """;

        AtProtoRepositoryRecord<Declaration>? declarationRecord = JsonSerializer.Deserialize<AtProtoRepositoryRecord<Declaration>>(json, options: BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(declarationRecord);
        Assert.Equal(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/com.germnetwork.declaration/self"), declarationRecord!.Uri);
        Assert.Equal(new Cid("bafyreifovknw2emzxmjn2m7oeajmwul55gqk2h5y4wqj5kapfoasnowjji"), declarationRecord.Cid);
        Assert.NotNull(declarationRecord.Value);

        Assert.IsType<Declaration>(declarationRecord.Value);
        Assert.Equal("1.1.0", declarationRecord.Value!.Version);
        Assert.Equal(new Uri("https://landing.ger.mx/newUser"), declarationRecord.Value.MessageMe!.MessageMeUrl);
        Assert.Equal("usersIFollow", declarationRecord.Value.MessageMe.ShowButtonTo);
        Assert.Equal([3, 168, 18, 138, 187, 232, 122, 150, 53, 67, 15, 115, 34, 32, 50, 115, 133, 231, 54, 89, 120, 219, 211, 200, 58, 142, 35, 62, 166, 27, 94, 140, 216], declarationRecord.Value.CurrentKey.Value!);
        Assert.Equal([0, 101, 98, 239, 211, 220, 40, 107, 43, 143, 114, 21, 223, 11, 132, 15, 172, 196, 182, 138, 129, 86, 191, 45, 22, 176, 213, 164, 31, 191, 40, 163, 254, 129, 147, 253, 1, 202, 171, 59, 229, 54, 0, 231, 172, 207, 145, 110, 139, 33, 192, 124, 228, 195, 76, 17, 27, 79, 125, 159, 228, 186, 245, 249, 12, 255, 1, 165, 0, 1, 3, 118, 5, 247, 51, 28, 85, 157, 136, 154, 252, 168, 107, 101, 116, 30, 207, 74, 124, 117, 76, 78, 33, 184, 190, 56, 35, 43, 234, 229, 111, 218, 125, 2, 2, 0, 0, 1, 255, 1, 57, 0, 1, 0, 5, 0, 1, 0, 3, 32, 215, 181, 181, 163, 233, 169, 197, 136, 126, 212, 165, 202, 104, 112, 192, 114, 179, 27, 46, 222, 21, 120, 38, 138, 4, 173, 71, 141, 83, 176, 201, 80, 32, 141, 133, 124, 65, 252, 83, 56, 117, 35, 49, 197, 148, 34, 46, 183, 175, 43, 204, 150, 240, 213, 7, 42, 169, 30, 73, 78, 13, 214, 28, 170, 12, 32, 243, 83, 101, 69, 14, 194, 245, 216, 90, 164, 35, 154, 109, 164, 135, 8, 219, 28, 248, 252, 226, 31, 247, 233, 25, 159, 173, 45, 152, 84, 235, 109, 0, 1, 33, 3, 118, 5, 247, 51, 28, 85, 157, 136, 154, 252, 168, 107, 101, 116, 30, 207, 74, 124, 117, 76, 78, 33, 184, 190, 56, 35, 43, 234, 229, 111, 218, 125, 2, 0, 1, 10, 0, 2, 0, 7, 0, 5, 0, 1, 0, 3, 0, 0, 2, 0, 1, 1, 0, 0, 0, 0, 105, 242, 80, 142, 0, 0, 0, 0, 107, 211, 132, 14, 0, 64, 64, 134, 93, 118, 46, 13, 83, 190, 38, 173, 190, 60, 110, 158, 59, 236, 231, 103, 128, 67, 62, 207, 231, 221, 182, 151, 199, 95, 197, 80, 96, 236, 93, 177, 155, 194, 172, 92, 29, 72, 155, 199, 8, 251, 200, 66, 56, 59, 215, 182, 36, 142, 10, 251, 12, 12, 245, 107, 85, 252, 247, 10, 43, 141, 4, 0, 64, 64, 137, 1, 37, 124, 118, 195, 218, 221, 74, 160, 160, 183, 177, 106, 229, 230, 150, 59, 67, 8, 94, 109, 235, 31, 219, 62, 138, 136, 97, 60, 114, 113, 120, 141, 222, 141, 55, 216, 59, 12, 55, 0, 83, 93, 77, 66, 102, 218, 214, 64, 205, 194, 59, 242, 66, 116, 50, 215, 74, 194, 173, 200, 34, 7, 0, 45, 230, 114, 1, 105, 60, 81, 114, 187, 70, 107, 149, 71, 250, 137, 31, 191, 27, 197, 175, 178, 250, 120, 79, 177, 136, 103, 13, 103, 208, 56, 79, 242, 166, 203, 220, 90, 25, 23, 153, 213, 163, 78, 97, 195, 29, 59, 66, 146, 57, 139, 247, 225, 108, 196, 101, 131, 178, 24, 4, 26, 208, 89, 0], declarationRecord.Value.KeyPackage!.Value);
    }
}