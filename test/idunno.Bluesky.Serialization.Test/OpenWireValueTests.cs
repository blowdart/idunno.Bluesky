// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.Bluesky.Graph;
using idunno.Bluesky.Notifications;
using idunno.Bluesky.Notifications.PreferenceTypes;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class OpenWireValueTests
{
    [Theory]
    [InlineData("all", LimitTo.All)]
    [InlineData("follows", LimitTo.Follows)]
    [InlineData("future", LimitTo.Unknown)]
    [InlineData("", LimitTo.Unknown)]
    public void FilterablePreferencesRetainWireValues(string value, LimitTo expected)
    {
        string json = $$"""{"include":"{{value}}","list":true,"push":false}""";
        FilterablePreference preference = JsonSerializer.Deserialize<FilterablePreference>(
            json, BlueskyServer.BlueskyJsonSerializerOptions)!;

        Assert.Equal(expected, preference.Include);
        preference.Push = true;
        using JsonDocument result = JsonDocument.Parse(JsonSerializer.Serialize(preference, BlueskyServer.BlueskyJsonSerializerOptions));
        Assert.Equal(value, result.RootElement.GetProperty("include").GetString());
        preference.Include = LimitTo.Follows;
        Assert.Equal("follows", preference.IncludeValue);
    }

    [Theory]
    [InlineData("none", NotificationAllowedFrom.None)]
    [InlineData("followers", NotificationAllowedFrom.Followers)]
    [InlineData("future", NotificationAllowedFrom.Unknown)]
    [InlineData("", NotificationAllowedFrom.Unknown)]
    public void DeclarationsRetainWireValues(string value, NotificationAllowedFrom expected)
    {
        string json = $$"""{"$type":"app.bsky.notification.declaration","allowSubscriptions":"{{value}}"}""";
        Declaration declaration = JsonSerializer.Deserialize<Declaration>(json, BlueskyServer.BlueskyJsonSerializerOptions)!;

        Assert.Equal(expected, declaration.AllowSubscriptions);
        using JsonDocument result = JsonDocument.Parse(JsonSerializer.Serialize(declaration, BlueskyServer.BlueskyJsonSerializerOptions));
        Assert.Equal(value, result.RootElement.GetProperty("allowSubscriptions").GetString());
    }

    [Theory]
    [InlineData("app.bsky.graph.defs#modlist", ListPurpose.ModList)]
    [InlineData("app.bsky.graph.defs#referencelist", ListPurpose.ReferenceList)]
    [InlineData("app.bsky.graph.defs#future", ListPurpose.Unknown)]
    [InlineData("", ListPurpose.Unknown)]
    public void ListsRetainWireValues(string value, ListPurpose expected)
    {
        string json = $$"""
            {"$type":"app.bsky.graph.list","name":"List","purpose":"{{value}}","createdAt":"2026-10-15T00:00:00Z"}
            """;
        List list = JsonSerializer.Deserialize<List>(json, BlueskyServer.BlueskyJsonSerializerOptions)!;

        Assert.Equal(expected, list.Purpose);
        list.Name = "Updated";
        using JsonDocument result = JsonDocument.Parse(JsonSerializer.Serialize(list, BlueskyServer.BlueskyJsonSerializerOptions));
        Assert.Equal(value, result.RootElement.GetProperty("purpose").GetString());
        Assert.False(result.RootElement.TryGetProperty("purposeProjection", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListViewsRetainUnknownWireValues(bool detailed)
    {
        string json = """
            {"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.graph.list/test",
             "cid":"bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i",
             "name":"List","purpose":"future",
             "creator":{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz","handle":"example.com"}}
            """;
        ListViewBasic list = detailed
            ? JsonSerializer.Deserialize<ListView>(json, BlueskyServer.BlueskyJsonSerializerOptions)!
            : JsonSerializer.Deserialize<ListViewBasic>(json, BlueskyServer.BlueskyJsonSerializerOptions)!;

        Assert.Equal(ListPurpose.Unknown, list.Purpose);
        using JsonDocument result = JsonDocument.Parse(JsonSerializer.Serialize(list, BlueskyServer.BlueskyJsonSerializerOptions));
        Assert.Equal("future", result.RootElement.GetProperty("purpose").GetString());
        Assert.False(result.RootElement.TryGetProperty("purposeProjection", out _));
    }

    [Fact]
    public void FilterablePreferencesRejectNullWireValuesAndEnumSentinels()
    {
        FilterablePreference preference = new();
        Assert.Throws<ArgumentNullException>(() => preference.IncludeValue = null!);
        Assert.Throws<ArgumentOutOfRangeException>(() => preference.Include = LimitTo.Unknown);
        Assert.Throws<ArgumentOutOfRangeException>(() => new FilterablePreference(LimitTo.Unknown, false, false));
    }

    [Fact]
    public void DeclarationKeepsNullAndDefaultDistinct()
    {
        Assert.Equal(NotificationAllowedFrom.Followers, new Declaration().AllowSubscriptions);
        Assert.Null(new Declaration(null).AllowSubscriptions);
        Declaration missing = JsonSerializer.Deserialize<Declaration>("{}", BlueskyServer.BlueskyJsonSerializerOptions)!;
        Declaration explicitNull = JsonSerializer.Deserialize<Declaration>("""{"allowSubscriptions":null}""", BlueskyServer.BlueskyJsonSerializerOptions)!;
        Assert.Equal(NotificationAllowedFrom.Followers, missing.AllowSubscriptions);
        Assert.Null(explicitNull.AllowSubscriptions);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Declaration(NotificationAllowedFrom.Unknown));
    }
}
