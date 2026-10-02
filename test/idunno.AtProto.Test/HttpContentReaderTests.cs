// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace idunno.AtProto.Test;

public class HttpContentReaderTests
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, 21L)]
    public async Task ReadAsPooledBytesReturnsTheContentWithoutAByteOrderMark(bool withByteOrderMark, long? contentLength)
    {
        const string json = """{"text":"h\u00e9llo"}""";
        byte[] body = withByteOrderMark ? [.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(json)] : Encoding.UTF8.GetBytes(json);

        using ByteArrayContent content = new(body);
        content.Headers.ContentLength = contentLength ?? body.Length;

        using PooledContent? pooled = await HttpContentReader.ReadAsPooledBytes(content, 1024, TestContext.Current.CancellationToken);

        Assert.NotNull(pooled);
        Assert.Equal(Encoding.UTF8.GetBytes(json), pooled.Span.ToArray());
        Assert.Equal(json, pooled.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadAsPooledBytesReturnsNullWhenTheContentIsTooLarge(bool declareLength)
    {
        using ByteArrayContent content = new(new byte[1025]);

        if (!declareLength)
        {
            content.Headers.ContentLength = null;
        }

        PooledContent? pooled = await HttpContentReader.ReadAsPooledBytes(content, 1024, TestContext.Current.CancellationToken);

        Assert.Null(pooled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadersAcceptSmallBodiesWithIntMaxValueLimit(bool pooled)
    {
        using ByteArrayContent content = new("small"u8.ToArray());

        if (pooled)
        {
            using PooledContent? result = await HttpContentReader.ReadAsPooledBytes(content, int.MaxValue, TestContext.Current.CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("small"u8, result.Span);
        }
        else
        {
            string? result = await HttpContentReader.ReadAsString(content, int.MaxValue, TestContext.Current.CancellationToken);
            Assert.Equal("small", result);
        }
    }

    [Fact]
    public async Task PooledContentRejectsInvalidUtf8()
    {
        using ByteArrayContent content = new([0xC3, 0x28]);
        using PooledContent? pooled = await HttpContentReader.ReadAsPooledBytes(content, 1024, TestContext.Current.CancellationToken);

        Assert.NotNull(pooled);
        Assert.Throws<DecoderFallbackException>(pooled.ToString);
    }

    [Fact]
    public void PooledContentThrowsAfterItIsDisposed()
    {
        byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(4);
        buffer.AsSpan(0, 4).Fill(0xA5);
        PooledContent pooled = new(buffer, 4);

        pooled.Dispose();
        pooled.Dispose();

        Assert.All(buffer.AsSpan(0, 4).ToArray(), value => Assert.Equal(0, value));
        Assert.Throws<ObjectDisposedException>(() => pooled.Span.Length);
        Assert.Throws<ObjectDisposedException>(pooled.ToString);
    }
}
