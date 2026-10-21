// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class LabelerDeclarationTests
{
    private static Service CreateDeclaration() => new()
    {
        CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        Policies = new LabelerPolicies { LabelValues = ["spam"] }
    };

    [Fact]
    public void ConcreteSerializationWritesTheDiscriminatorAndSelfLabelsObject()
    {
        Service declaration = CreateDeclaration() with { Labels = new SelfLabels(new SelfLabel("!no-unauthenticated")) };
        string json = JsonSerializer.Serialize(declaration, BlueskyServer.BlueskyJsonSerializerOptions);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal(RecordType.LabelerDeclaration, root.GetProperty("$type").GetString());
        Assert.Equal("com.atproto.label.defs#selfLabels", root.GetProperty("labels").GetProperty("$type").GetString());
        Assert.Equal("!no-unauthenticated", root.GetProperty("labels").GetProperty("values")[0].GetProperty("val").GetString());
        Assert.Equal("!no-unauthenticated",
            Assert.Single(JsonSerializer.Deserialize<Service>(json, BlueskyServer.BlueskyJsonSerializerOptions)!.Labels!.Values).Value);
        Assert.IsType<Service>(JsonSerializer.Deserialize<BlueskyRecord>(json, BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptionalFieldsPreserveOmittedVersusEmptyWithAnyDefaultIgnoreCondition(bool includeEmpty)
    {
        Service declaration = CreateDeclaration() with
        {
            ReasonTypes = includeEmpty ? [] : null,
            SubjectTypes = includeEmpty ? [] : null,
            SubjectCollections = includeEmpty ? [] : null
        };
        foreach (System.Text.Json.Serialization.JsonIgnoreCondition condition in new[]
        {
            System.Text.Json.Serialization.JsonIgnoreCondition.Never,
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
        })
        {
            JsonSerializerOptions options = new(BlueskyServer.BlueskyJsonSerializerOptions) { DefaultIgnoreCondition = condition };
            string json = JsonSerializer.Serialize(declaration, options);
            using JsonDocument document = JsonDocument.Parse(json);
            foreach (string field in new[] { "reasonTypes", "subjectTypes", "subjectCollections" })
            {
                Assert.Equal(includeEmpty, document.RootElement.TryGetProperty(field, out JsonElement value));
                if (includeEmpty)
                {
                    Assert.Equal(0, value.GetArrayLength());
                }
            }

            Assert.False(document.RootElement.TryGetProperty("labels", out _));
            Service roundTrip = JsonSerializer.Deserialize<Service>(json, options)!;
            Assert.Equal(includeEmpty, roundTrip.ReasonTypes is not null);
            Assert.Equal(includeEmpty, roundTrip.SubjectTypes is not null);
            Assert.Equal(includeEmpty, roundTrip.SubjectCollections is not null);
        }
    }

    [Theory]
    [InlineData("com.atproto.moderation.defs#reasonSpam")]
    [InlineData("tools.ozone.report.defs#reasonSexualNCII")]
    [InlineData("example.future.report#reasonNew")]
    public void RepositoryEnvelopePreservesOpenReportReasonsAndExtensionData(string reason)
    {
        string json = $$"""
            {
                "uri":"at://did:plc:test/app.bsky.labeler.service/self",
                "cid":"bafyreigu3v6uxtmx3rv7emh4yvuvz2tom7ib7a4yl3x2eimard2brqeaei",
                "value":{
                    "$type":"app.bsky.labeler.service",
                    "createdAt":"2026-01-01T00:00:00Z",
                    "policies":{"labelValues":["spam"]},
                    "reasonTypes":["{{reason}}"],
                    "subjectTypes":["record"],
                    "subjectCollections":["app.bsky.feed.post"],
                    "futureField":{"enabled":true}
                }
            }
            """;
        AtProtoRepositoryRecord<Service> record =
            JsonSerializer.Deserialize<AtProtoRepositoryRecord<Service>>(json, BlueskyServer.BlueskyJsonSerializerOptions)!;

        Assert.Equal(reason, Assert.Single(record.Value.ReasonTypes!));
        Assert.Equal("record", Assert.Single(record.Value.SubjectTypes!));
        Assert.Equal(new Nsid("app.bsky.feed.post"), Assert.Single(record.Value.SubjectCollections!));
        Service edited = record.Value with { ReasonTypes = [] };
        Assert.True(edited.ExtensionData["futureField"].GetProperty("enabled").GetBoolean());
        string written = JsonSerializer.Serialize(edited, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.Contains("\"futureField\":{\"enabled\":true}", written, StringComparison.Ordinal);
    }
}
