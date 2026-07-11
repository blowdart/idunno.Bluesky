// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Repo;
using idunno.Bluesky.Chat.Actor;

namespace idunno.Bluesky.Serialization.Test.Conversations;

public class DeclarationTests
{
    [Fact]
    public void AtProtoRepositoryRecordWrapperDeclarationDeserializesCorrectly()
    {
        string json = """
            {
                "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/chat.bsky.actor.declaration/self",
                "cid": "bafyreigsxv3tc4fjy27ry7j3vhcp2qphm3o4ex5wx6gqhp7ax63sdnxoh4",
                "value": {
                    "$type": "chat.bsky.actor.declaration",
                    "allowIncoming": "following",
                    "allowGroupInvites": "following"
                }
            }
            """;

        AtProtoRepositoryRecord<Declaration>? declarationRecord = JsonSerializer.Deserialize<AtProtoRepositoryRecord<Chat.Actor.Declaration>>(json, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(declarationRecord);
        Assert.Equal("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/chat.bsky.actor.declaration/self", declarationRecord.Uri);
        Assert.Equal("bafyreigsxv3tc4fjy27ry7j3vhcp2qphm3o4ex5wx6gqhp7ax63sdnxoh4", declarationRecord.Cid);

        Assert.NotNull(declarationRecord.Value);
        Assert.IsType<Chat.Actor.Declaration>(declarationRecord.Value);
        var declaration = declarationRecord.Value as Chat.Actor.Declaration;
        Assert.NotNull(declaration);
        Assert.Equal(Chat.Actor.AllowIncoming.Following, declaration.AllowIncoming);
        Assert.Equal(Chat.Actor.AllowGroupInvites.Following, declaration.AllowGroupInvites);
    }

    [Fact]
    public void DeclarationSerializesCorrectly()
    {
        var declaration = new Chat.Actor.Declaration(Chat.Actor.AllowIncoming.Following, Chat.Actor.AllowGroupInvites.Following);

        string json = JsonSerializer.Serialize(declaration, BlueskyJsonSerializerOptions.Options);
        Assert.Equal("""{"$type":"chat.bsky.actor.declaration","allowIncoming":"following","allowGroupInvites":"following"}""", json);
    }
}