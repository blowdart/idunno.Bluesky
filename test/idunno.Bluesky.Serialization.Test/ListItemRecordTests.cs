// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class ListItemRecordTests
{
    private const string RecordJson = """
        {
            "$type": "app.bsky.graph.listitem",
            "list": "at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/com.example.lists/test",
            "subject": "did:plc:ewvi7nxzyoun6zhxrhs64oiz",
            "createdAt": "2025-04-21T10:49:31.969Z"
        }
        """;

    [Fact]
    public void ConcreteAndBaseRecordsRoundTripWithSourceGeneratedJson()
    {
        ListItem? record = JsonSerializer.Deserialize<ListItem>(RecordJson, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.NotNull(record);
        Assert.Equal("com.example.lists", record.List.Collection?.ToString());
        Assert.Equal("did:plc:ewvi7nxzyoun6zhxrhs64oiz", record.Subject.Value);
        Assert.Equal(new DateTimeOffset(2025, 4, 21, 10, 49, 31, 969, TimeSpan.Zero), record.CreatedAt);

        string concreteJson = JsonSerializer.Serialize(record, BlueskyServer.BlueskyJsonSerializerOptions);
        using JsonDocument document = JsonDocument.Parse(concreteJson);
        Assert.Equal(RecordType.ListItem, document.RootElement.GetProperty("$type").GetString());
        Assert.Equal(record, JsonSerializer.Deserialize<ListItem>(concreteJson, BlueskyServer.BlueskyJsonSerializerOptions));

        string baseJson = JsonSerializer.Serialize<BlueskyRecord>(record, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.Equal(record, Assert.IsType<ListItem>(
            JsonSerializer.Deserialize<BlueskyRecord>(baseJson, BlueskyServer.BlueskyJsonSerializerOptions)));

        string timestampedJson = JsonSerializer.Serialize<BlueskyTimestampedRecord>(record, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.Equal(record, Assert.IsType<ListItem>(
            JsonSerializer.Deserialize<BlueskyTimestampedRecord>(timestampedJson, BlueskyServer.BlueskyJsonSerializerOptions)));
    }

    [Fact]
    public void RepositoryRecordDeserializesWithSourceGeneratedJson()
    {
        string json = $$"""
            {
                "uri": "at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.graph.listitem/test",
                "cid": "bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i",
                "value": {{RecordJson}}
            }
            """;

        AtProtoRepositoryRecord<ListItem>? record =
            JsonSerializer.Deserialize<AtProtoRepositoryRecord<ListItem>>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(record);
        Assert.Equal("com.example.lists", record.Value.List.Collection?.ToString());
        Assert.Equal("did:plc:ewvi7nxzyoun6zhxrhs64oiz", record.Value.Subject.Value);
    }

    [Theory]
    [InlineData("list", false)]
    [InlineData("subject", false)]
    [InlineData("createdAt", false)]
    [InlineData("list", true)]
    [InlineData("subject", true)]
    public void DeserializationRejectsMissingOrNullRequiredProperties(string property, bool nullValue)
    {
        JsonObject properties = JsonNode.Parse(RecordJson)!.AsObject();
        if (nullValue)
        {
            properties[property] = null;
        }
        else
        {
            properties.Remove(property);
        }

        string json = properties.ToJsonString();

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ListItem>(json, BlueskyServer.BlueskyJsonSerializerOptions));
    }
}
