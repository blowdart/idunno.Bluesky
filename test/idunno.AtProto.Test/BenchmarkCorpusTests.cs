// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using idunno.AtProto.Benchmarks;
using idunno.AtProto.Jetstream;
using ZstdSharp;

namespace idunno.AtProto.Test;

public class BenchmarkCorpusTests
{
    public static TheoryData<string> Corpora => [.. CorpusFile.Names];

    [Theory]
    [MemberData(nameof(Corpora))]
    public void CorpusHasTheExpectedNumberOfMessages(string name) =>
        Assert.Equal(CorpusFile.MessageCount(name), CorpusFile.Read(name).Length);

    [Theory]
    [MemberData(nameof(Corpora))]
    public void CorpusContainsNothingWhichLooksLikeACredential(string name)
    {
        using Decompressor decompressor = new();
        decompressor.LoadDictionary(new JetstreamOptions().Dictionary);

        byte[][] messages = CorpusFile.Read(name);

        for (int i = 0; i < messages.Length; i++)
        {
            byte[] message = name == CorpusFile.JetstreamV1Zstd ? decompressor.Unwrap(messages[i]).ToArray() : messages[i];
            Assert.False(SensitiveContent.IsSensitive(message), $"Message {i} of the {name} corpus looks like it contains a credential.");
        }
    }

    [Theory]
    [MemberData(nameof(Corpora))]
    public void CorpusDoesNotContainTheCaptureAccountCredentials(string name)
    {
        IReadOnlyList<string> credentials = CaptureScrubber.CredentialsFromEnvironment();
        if (credentials.Count == 0)
        {
            Assert.Skip($"Neither {CaptureScrubber.HandleVariable} nor {CaptureScrubber.PasswordVariable} is set.");
        }

        using Decompressor decompressor = new();
        decompressor.LoadDictionary(new JetstreamOptions().Dictionary);

        byte[][] messages = CorpusFile.Read(name);

        for (int i = 0; i < messages.Length; i++)
        {
            byte[] message = name == CorpusFile.JetstreamV1Zstd ? decompressor.Unwrap(messages[i]).ToArray() : messages[i];
            Assert.False(CaptureScrubber.ContainsAny(message, credentials), $"Message {i} of the {name} corpus contains the capture account's handle or password.");
        }
    }

    [Fact]
    public void ScrubFeedRemovesEntriesMentioningSecretsAndReplacesTheViewerDid()
    {
        const string viewer = "did:plc:viewerviewerviewerviewer";
        byte[] json = Encoding.UTF8.GetBytes(
            "{\"feed\":[" +
            "{\"post\":{\"author\":{\"handle\":\"Me.Example.COM\"}}}," +
            "{\"post\":{\"text\":\"my password is hunter2\"}}," +
            "{\"post\":{\"text\":\"keep 🦋\",\"viewer\":{\"like\":\"at://" + viewer + "/app.bsky.feed.like/1\"}}}" +
            "],\"cursor\":\"abc\"}");

        byte[] scrubbed = CaptureScrubber.ScrubFeed(json, viewer, ["me.example.com", "hunter2"], out int removed);

        Assert.Equal(2, removed);
        Assert.Equal(
            "{\"feed\":[{\"post\":{\"text\":\"keep 🦋\",\"viewer\":{\"like\":\"at://" + CaptureScrubber.PlaceholderDid + "/app.bsky.feed.like/1\"}}}],\"cursor\":\"abc\"}",
            Encoding.UTF8.GetString(scrubbed));
        Assert.False(CaptureScrubber.ContainsAny(scrubbed, ["me.example.com", "hunter2", viewer]));
    }

    [Theory]
    [InlineData("{\"text\":\"pass\\\"word\"}", "pass\"word")]
    [InlineData("{\"text\":\"HUNTER2\"}", "hunter2")]
    public void ContainsAnyFindsEscapedAndDifferentlyCasedValues(string json, string value) =>
        Assert.True(CaptureScrubber.ContainsAny(Encoding.UTF8.GetBytes(json), [value]));

    [Theory]
    [InlineData("Authorization: Bearer abc")]
    [InlineData("authorization")]
    [InlineData("DPoP proof")]
    [InlineData("{\"accessJwt\":\"x\"}")]
    [InlineData("{\"refresh_token\":\"x\"}")]
    [InlineData("eyJhbGciOiJFUzI1NiJ9.eyJzdWIiOiJkaWQ6cGxjOmFiYyJ9.signature")]
    [InlineData("my app-password is")]
    public void SensitiveContentDetectsCredentials(string value) =>
        Assert.True(SensitiveContent.IsSensitive(Encoding.UTF8.GetBytes(value)));

    [Theory]
    [InlineData("{\"did\":\"did:plc:abc\",\"kind\":\"commit\",\"commit\":{\"collection\":\"app.bsky.feed.like\"}}")]
    [InlineData("just a normal post about the weather")]
    public void SensitiveContentAllowsOrdinaryMessages(string value) =>
        Assert.False(SensitiveContent.IsSensitive(Encoding.UTF8.GetBytes(value)));
}