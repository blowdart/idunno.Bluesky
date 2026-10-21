// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class BossettLabelerDeclarationTests
{
    // Captured on 2026-10-20 from the account's PDS:
    // https://maitake.us-west.host.bsky.network/xrpc/com.atproto.repo.getRecord?repo=did:plc:i65enriuag7n5fgkopbqtkyk&collection=app.bsky.labeler.service&rkey=self
    private const string Response = """
        {"uri":"at://did:plc:i65enriuag7n5fgkopbqtkyk/app.bsky.labeler.service/self","cid":"bafyreigh5fjr6an3qujvfbm6xmso6jyhido2dguixaeqp3jtcqft2gofba","value":{
        "$type":"app.bsky.labeler.service","policies":{
        "labelValues":["changedhandle","incomplete","onlyreplies","rapidposts","nonplcdid","bridgy","nostr","threads","stephanie"],
        "labelValueDefinitions":[
        {"blurs":"none","locales":[{"lang":"en","name":"Changed Handle","description":"This user began using this handle on this account less than 14 days ago."}],"severity":"inform","adultOnly":false,"identifier":"changedhandle","defaultSetting":"warn"},
        {"blurs":"none","locales":[{"lang":"en","name":"Incomplete Profile","description":"This user is missing a profile picture or display name."}],"severity":"inform","adultOnly":false,"identifier":"incomplete","defaultSetting":"warn"},
        {"blurs":"none","locales":[{"lang":"en","name":"Only Replies","description":"This user has not made a non-reply post."}],"severity":"alert","adultOnly":false,"identifier":"onlyreplies","defaultSetting":"warn"},
        {"blurs":"none","locales":[{"lang":"en","name":"Periodic","description":"This account has been posting at closely timed intervals without a break the last 30 posts."}],"severity":"alert","adultOnly":false,"identifier":"rapidposts","defaultSetting":"warn"},
        {"blurs":"none","locales":[{"lang":"en","name":"Non-PLC DID","description":"This user has a did from a namespace other than did:plc: (and therefore handle history not available)."}],"severity":"inform","adultOnly":false,"identifier":"nonplcdid","defaultSetting":"warn"},
        {"blurs":"none","locales":[{"lang":"en","name":"Bridgy User","description":"This user is on a non-ATProto network, bridged in from Bridgy (https://fed.brid.gy/docs)."}],"severity":"inform","adultOnly":false,"identifier":"bridgy","defaultSetting":"warn"},
        {"blurs":"none","locales":[{"lang":"en","name":"Nostr User","description":"This user originates from Nostr and has been bridged in via other networks by Bridgy. Does NOT include users who have just validated a handle with an npub signature."}],"severity":"inform","adultOnly":false,"identifier":"nostr","defaultSetting":"warn"},
        {"blurs":"none","locales":[{"lang":"en","name":"Threads User","description":"This user originates from Threads and has been bridged in by Bridgy."}],"severity":"inform","adultOnly":false,"identifier":"threads","defaultSetting":"warn"},
        {"blurs":"none","locales":[{"lang":"en","name":"Stephanie","description":"Stephanie goes by Stephanie."}],"severity":"inform","adultOnly":false,"identifier":"stephanie","defaultSetting":"warn"}
        ]},"createdAt":"2024-11-22T19:10:24.224Z","reasonTypes":["com.atproto.moderation.defs#reasonOther"],"subjectTypes":["account"],"subjectCollections":["app.bsky.actor.profile"]}}
        """;

    [Fact]
    public void CapturedDeclarationDeserializesThroughTheRepositoryEnvelopeAndBaseRecord()
    {
        AtProtoRepositoryRecord<Service> record = JsonSerializer.Deserialize<AtProtoRepositoryRecord<Service>>(
            Response, BlueskyServer.BlueskyJsonSerializerOptions)!;

        Assert.Equal(new AtUri("at://did:plc:i65enriuag7n5fgkopbqtkyk/app.bsky.labeler.service/self"), record.Uri);
        Assert.Equal(new Cid("bafyreigh5fjr6an3qujvfbm6xmso6jyhido2dguixaeqp3jtcqft2gofba"), record.Cid);
        Service declaration = record.Value;
        Assert.Equal(DateTimeOffset.Parse("2024-11-22T19:10:24.224Z", CultureInfo.InvariantCulture), declaration.CreatedAt);
        Assert.Equal(["com.atproto.moderation.defs#reasonOther"], declaration.ReasonTypes);
        Assert.Equal(["account"], declaration.SubjectTypes);
        Assert.Equal(new Nsid("app.bsky.actor.profile"), Assert.Single(declaration.SubjectCollections!));
        Assert.Null(declaration.Labels);
        Assert.Equal(
            ["changedhandle", "incomplete", "onlyreplies", "rapidposts", "nonplcdid", "bridgy", "nostr", "threads", "stephanie"],
            declaration.Policies.LabelValues);

        using JsonDocument captured = JsonDocument.Parse(Response);
        JsonElement expectedDefinitions = captured.RootElement.GetProperty("value").GetProperty("policies").GetProperty("labelValueDefinitions");
        Assert.Equal(9, declaration.Policies.LabelValueDefinitions.Count);
        int index = 0;
        foreach (LabelValueDefinition definition in declaration.Policies.LabelValueDefinitions)
        {
            JsonElement expected = expectedDefinitions[index++];
            Assert.Equal(expected.GetProperty("identifier").GetString(), definition.Identifier);
            Assert.Equal(expected.GetProperty("severity").GetString(), definition.Severity);
            Assert.Equal("none", definition.Blurs);
            Assert.Equal("warn", definition.DefaultSetting);
            Assert.False(definition.AdultOnly);
            LabelValueDefinitionStrings locale = Assert.Single(definition.Locales);
            Assert.Equal("en", locale.Lang);
            Assert.Equal(expected.GetProperty("locales")[0].GetProperty("name").GetString(), locale.Name);
            Assert.Equal(expected.GetProperty("locales")[0].GetProperty("description").GetString(), locale.Description);
        }

        string valueJson = captured.RootElement.GetProperty("value").GetRawText();
        Assert.IsType<Service>(JsonSerializer.Deserialize<BlueskyRecord>(valueJson, BlueskyServer.BlueskyJsonSerializerOptions));
        string serialized = JsonSerializer.Serialize(declaration, BlueskyServer.BlueskyJsonSerializerOptions);
        using JsonDocument written = JsonDocument.Parse(serialized);
        Assert.Equal(RecordType.LabelerDeclaration, written.RootElement.GetProperty("$type").GetString());
        Service roundTrip = JsonSerializer.Deserialize<Service>(serialized, BlueskyServer.BlueskyJsonSerializerOptions)!;
        Assert.Equal(declaration.ReasonTypes, roundTrip.ReasonTypes);
        Assert.Equal(declaration.SubjectTypes, roundTrip.SubjectTypes);
        Assert.Equal(declaration.Policies.LabelValues, roundTrip.Policies.LabelValues);
    }
}
