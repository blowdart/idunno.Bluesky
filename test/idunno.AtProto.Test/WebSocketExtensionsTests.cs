// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;
using System.Security.Cryptography;

using idunno.AtProto.Benchmarks;

namespace idunno.AtProto.Test;

public class WebSocketExtensionsTests
{
    [Theory]
    [InlineData(0, 4096)]
    [InlineData(1, 4096)]
    [InlineData(4095, 4096)]
    [InlineData(4096, 4096)]
    [InlineData(4097, 4096)]
    [InlineData(100_000, 4096)]
    [InlineData(ReplayWebSocket.FragmentSize * 3, 16 * 1024)]
    [InlineData(1_000_003, 1024)]
    public async Task ReceiveNextMessageAsyncReturnsTheWholeMessage(int messageLength, int bufferSize)
    {
        byte[] message = RandomNumberGenerator.GetBytes(messageLength);
        ReplayWebSocket socket = new(WebSocketMessageType.Binary);
        socket.Load(message);

        (WebSocketReceiveResult result, byte[] received) = await socket.ReceiveNextMessageAsync(bufferSize, maxMessageSize: 2 * 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.EndOfMessage);
        Assert.Equal(message, received);
    }

    [Fact]
    public async Task ReceiveNextMessageAsyncReturnsIndependentMessages()
    {
        ReplayWebSocket socket = new(WebSocketMessageType.Binary);

        socket.Load([1, 2, 3]);
        (_, byte[] first) = await socket.ReceiveNextMessageAsync(1024, cancellationToken: TestContext.Current.CancellationToken);

        socket.Load([4, 5]);
        (_, byte[] second) = await socket.ReceiveNextMessageAsync(1024, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], first);
        Assert.Equal([4, 5], second);
    }

    [Theory]
    [InlineData(1025, 4096)]
    [InlineData(10_000, 1024)]
    public async Task ReceiveNextMessageAsyncThrowsWhenTheMessageIsTooLarge(int messageLength, int bufferSize)
    {
        ReplayWebSocket socket = new(WebSocketMessageType.Binary);
        socket.Load(new byte[messageLength]);

        await Assert.ThrowsAsync<WebSocketMessageAbandonedException>(
            () => socket.ReceiveNextMessageAsync(bufferSize, maxMessageSize: 1024, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ReceiveNextMessageAsyncThrowsWhenTheBufferSizeIsNotPositive(int bufferSize)
    {
        ReplayWebSocket socket = new(WebSocketMessageType.Binary);
        socket.Load([1]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => socket.ReceiveNextMessageAsync(bufferSize, cancellationToken: TestContext.Current.CancellationToken));
    }
}
