// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace idunno.AtProto.Types.Test;

[ExcludeFromCodeCoverage]
public class BlobTests
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void BlobSerializesProperlyWithSourceGeneratedJsonContext()
    {
        Blob blob = new (new CidLink("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), mimeType: "mime/type", size: 1024);

        string blobAsJson = JsonSerializer.Serialize(blob, SourceGenerationContext.Default.Blob);

        Assert.Equal("{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a\"},\"mimeType\":\"mime/type\",\"size\":1024}", blobAsJson);
    }

    [Fact]
    public void BlobSerializesProperlyWithNoSourceGeneration()
    {
        Blob blob = new(new CidLink("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), mimeType: "mime/type", size: 1024);

        string blobAsJson = JsonSerializer.Serialize(blob, _jsonSerializerOptions);

        Assert.Equal("{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a\"},\"mimeType\":\"mime/type\",\"size\":1024}", blobAsJson);
    }

    [Fact]
    public void BlobDeserializesProperlyWithSourceGeneratedJsonContext()
    {
        string blobAsJson = """
        {
            "$type":"blob",
            "ref":{
                "$link":"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"
             },
             "mimeType":"mime/type",
             "size":1024
        }
        """;

        Blob? blob = JsonSerializer.Deserialize(blobAsJson, SourceGenerationContext.Default.Blob);

        Assert.NotNull(blob);
        Assert.Equal("mime/type", blob.MimeType);
        Assert.Equal(1024, blob.Size);
        Assert.NotNull(blob.Reference);
        Assert.Equal(new Cid("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), blob.Reference.Link);
    }

    [Fact]
    public void BlobDeserializesProperlyWithNoSourceGeneration()
    {
        string blobAsJson = """
        {
            "$type":"blob",
            "ref":{
                "$link":"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"
             },
             "mimeType":"mime/type",
             "size":1024
        }
        """;

        Blob? blob = JsonSerializer.Deserialize<Blob>(blobAsJson, _jsonSerializerOptions);

        Assert.NotNull(blob);
        Assert.Equal("mime/type", blob.MimeType);
        Assert.Equal(1024, blob.Size);
        Assert.NotNull(blob.Reference);
        Assert.Equal(new Cid("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), blob.Reference.Link);
    }
}
