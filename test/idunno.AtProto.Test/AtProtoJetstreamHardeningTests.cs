// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Jetstream;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class AtProtoJetstreamHardeningTests
{
    [Theory]
    [InlineData("ftp://jetstream.example/subscribe")]
    [InlineData("file:///subscribe")]
    [InlineData("net.tcp://jetstream.example/subscribe")]
    public async Task ConnectingToAUriWhichIsNotAWebSocketOrHttpSchemeThrows(string uri)
    {
        using AtProtoJetstream jetstream = new();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => jetstream.ConnectAsync(new Uri(uri, UriKind.Absolute), cursor: null, httpClient: null, TestContext.Current.CancellationToken));

        Assert.Equal("uri", exception.ParamName);
    }

    [Fact]
    public async Task ConnectingToARelativeUriThrows()
    {
        using AtProtoJetstream jetstream = new();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => jetstream.ConnectAsync(new Uri("/subscribe", UriKind.Relative), cursor: null, httpClient: null, TestContext.Current.CancellationToken));

        Assert.Equal("uri", exception.ParamName);
    }

    [Fact]
    public async Task DisposeAsyncOnAJetstreamWhichWasNeverConnectedCompletes()
    {
        AtProtoJetstream jetstream = new();

        await jetstream.DisposeAsync();

        // Disposal must be idempotent, and connecting afterwards must be refused rather than silently doing nothing.
        await jetstream.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => jetstream.ConnectAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ABuilderCompressionDictionaryIsCopiedRatherThanShared()
    {
        byte[] dictionary = [1, 2, 3];

        AtProtoJetstreamBuilder builder = AtProtoJetstream.CreateBuilder().WithCompressionDictionary(dictionary);

        // A caller which keeps a reference to the array must not be able to change it whilst native code reads it.
        dictionary[0] = 42;

        Assert.Equal(1, builder.CompressionDictionary[0]);

        builder.CompressionDictionary[0] = 99;

        Assert.Equal(1, builder.CompressionDictionary[0]);
    }

    [Fact]
    public void AnOptionsCompressionDictionaryIsCopiedRatherThanShared()
    {
        byte[] dictionary = [1, 2, 3];

        JetstreamOptions options = new() { Dictionary = dictionary };

        dictionary[0] = 42;

        Assert.NotNull(options.Dictionary);
        Assert.Equal(1, options.Dictionary[0]);

        options.Dictionary[0] = 99;

        Assert.Equal(1, options.Dictionary[0]);
    }
}
