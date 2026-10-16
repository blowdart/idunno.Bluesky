// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Benchmarks;

namespace idunno.AtProto.Test;

public class CaptureContentTests
{
    [Theory]
    [InlineData(15, true)]
    [InlineData(16, true)]
    [InlineData(17, true)]
    [InlineData(15, false)]
    [InlineData(16, false)]
    [InlineData(17, false)]
    public async Task CaptureBodiesAreBounded(int length, bool declaredLength)
    {
        using HttpContent content = declaredLength
            ? new ByteArrayContent(new byte[length])
            : new UnknownLengthContent(new byte[length]);

        if (length > 16)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => CaptureContent.ReadAsync(content, 16, TestContext.Current.CancellationToken));
        }
        else
        {
            byte[] body = await CaptureContent.ReadAsync(content, 16, TestContext.Current.CancellationToken);
            Assert.Equal(length, body.Length);
        }

    }

    private sealed class UnknownLengthContent(byte[] body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            stream.WriteAsync(body).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
