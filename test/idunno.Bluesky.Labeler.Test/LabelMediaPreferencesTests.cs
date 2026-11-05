// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Nodes;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;

namespace idunno.Bluesky.Labeler.Test;

public class LabelMediaPreferencesTests
{
    [Fact]
    public async Task MediaPreferencesSurviveReopeningStoreAndTeamChanges()
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync("sqlite");
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelTableStore preferences = (ILabelTableStore)fixture.Store;
        Assert.Equal(new(), (await preferences.ReadMediaPreferencesAsync(caller, token)).Result);
        LabelMediaPreferences selected = new(
            GraphicMedia: LabelMediaFilter.Blur | LabelMediaFilter.Grayscale | LabelMediaFilter.Translucent,
            Porn: LabelMediaFilter.None,
            Nudity: LabelMediaFilter.Grayscale,
            Sexual: LabelMediaFilter.Translucent,
            SelfHarm: LabelMediaFilter.Blur);
        Assert.Equal(selected, (await preferences.SaveMediaPreferencesAsync(caller, selected, token)).Result);
        ILabelStore reopened = fixture.NewStore();
        Assert.True((await ((ILabelAdministrationStore)reopened).ChangeTeamAsync(
            caller: caller,
            change: new(
                operationId: "preferences-enroll",
                action: LabelTeamAction.Enroll,
                target: new("did:plc:another-member"),
                reason: "Check unrelated team changes preserve personal preferences."),
            cancellationToken: token)).Succeeded);
        Assert.Equal(selected, (await ((ILabelTableStore)fixture.NewStore()).ReadMediaPreferencesAsync(caller, token)).Result);
        Assert.Equal(0, (await reopened.GetStateAsync(token)).IssuedThrough);
    }

    [Fact]
    public async Task LegacyTeamPayloadsDefaultToBlurWithoutPublishingPersonalSettings()
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync("sqlite");
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        await using (SqliteConnection connection = new(fixture.ConnectionString))
        {
            await connection.OpenAsync(token);
            await using SqliteCommand read = connection.CreateCommand();
            read.CommandText = "SELECT payload FROM label_team WHERE id=1";
            JsonNode document = JsonNode.Parse((byte[])(await read.ExecuteScalarAsync(token))!)!;
            JsonObject member = document["Members"]![caller.Actor.Value]!.AsObject();
            member.Remove("MediaPreferences");
            await using SqliteCommand write = connection.CreateCommand();
            write.CommandText = "UPDATE label_team SET payload=$payload WHERE id=1";
            write.Parameters.AddWithValue("$payload", Encoding.UTF8.GetBytes(document.ToJsonString()));
            await write.ExecuteNonQueryAsync(token);
        }
        Assert.Equal(new(), (await ((ILabelTableStore)fixture.NewStore()).ReadMediaPreferencesAsync(caller, token)).Result);
        LabelMediaPreferences preferences = new();
        Assert.Equal(LabelMediaFilter.Blur, preferences.ForLabel("porn"));
        Assert.Equal(LabelMediaFilter.Blur, preferences.ForLabel("self-harm"));
        Assert.Equal(LabelMediaFilter.Blur, preferences.ForLabel("!hide"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    [InlineData(16)]
    public void UnknownFilterBitsAreRejected(int bits)
    {
        LabelMediaPreferences preferences = new(Porn: (LabelMediaFilter)bits);
        Assert.Throws<ArgumentOutOfRangeException>(preferences.Validate);
    }
}
