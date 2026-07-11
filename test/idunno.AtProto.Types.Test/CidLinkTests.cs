// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace idunno.AtProto.Types.Test;

public class CidLinkTests
{
    [Fact]
    public void CidLinkConstructorThrowsWhenLinkIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new CidLink(null!));
    }

    [Fact]
    public void CidLinkConstructorSetsLinkProperty()
    {
        Cid cid = new("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a");
        CidLink cidLink = new(cid);
        Assert.Equal(cid, cidLink.Link);
    }

    [Fact]
    public void CidLinkSerializesProperly()
    {
        CidLink cidLink = new(new Cid("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"));
        string json = JsonSerializer.Serialize(cidLink);
        Assert.Equal("{\"$link\":\"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a\"}", json);
    }

    [Fact]
    public void CidLinkSerializesProperlyWithSourceGeneration()
    {
        CidLink cidLink = new(new Cid("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"));
        string json = JsonSerializer.Serialize(cidLink, SourceGenerationContext.Default.CidLink);
        Assert.Equal("{\"$link\":\"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a\"}", json);
    }

    [Fact]
    public void CidLinkDeserializesProperly()
    {
        string json = "{\"$link\":\"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a\"}";
        CidLink? cidLink = JsonSerializer.Deserialize<CidLink>(json);
        Assert.NotNull(cidLink);
        Assert.Equal(new Cid("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), cidLink.Link);
    }

    [Fact]
    public void CidLinkDeserializesProperlyWithSourceGeneration()
    {
        string json = "{\"$link\":\"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a\"}";
        CidLink? cidLink = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.CidLink);
        Assert.NotNull(cidLink);
        Assert.Equal(new Cid("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), cidLink.Link);
    }
}