// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Jetstream;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class JetstreamBuilderTests
{
    [Fact]
    public void WithCompressionDictionaryThrowsOnNull()
    {
        AtProtoJetstreamBuilder builder = AtProtoJetstream.CreateBuilder();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => builder.WithCompressionDictionary(null!));

        Assert.Equal("compressionDictionary", exception.ParamName);
    }

    [Fact]
    public void WithTaskFactoryThrowsOnNull()
    {
        AtProtoJetstreamBuilder builder = AtProtoJetstream.CreateBuilder();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => builder.WithTaskFactory(null!));

        Assert.Equal("taskFactory", exception.ParamName);
    }

    [Fact]
    public void FilterToThrowsOnANullDidArray()
    {
        AtProtoJetstreamBuilder builder = AtProtoJetstream.CreateBuilder();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => builder.FilterTo((Did[])null!));

        Assert.Equal("dids", exception.ParamName);
    }

    [Fact]
    public void FilterToThrowsOnANullNsidArray()
    {
        AtProtoJetstreamBuilder builder = AtProtoJetstream.CreateBuilder();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => builder.FilterTo((Nsid[])null!));

        Assert.Equal("collections", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetCloseTimeoutRejectsATimeoutWhichIsNotPositive(int seconds)
    {
        AtProtoJetstreamBuilder builder = AtProtoJetstream.CreateBuilder();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => builder.SetCloseTimeout(TimeSpan.FromSeconds(seconds)));

        Assert.Equal("closeTimeout", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void JetstreamOptionsRejectsACloseTimeoutWhichIsNotPositive(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JetstreamOptions { CloseTimeout = TimeSpan.FromSeconds(seconds) });
    }

    [Fact]
    public void TheCloseTimeoutSetOnTheBuilderReachesTheOptions()
    {
        TimeSpan closeTimeout = TimeSpan.FromSeconds(7);

        using AtProtoJetstream jetstream = AtProtoJetstream.CreateBuilder()
            .SetCloseTimeout(closeTimeout)
            .Build();

        Assert.Equal(closeTimeout, jetstream.Options.CloseTimeout);
    }

    [Fact]
    public void TheMessageSizesSetOnTheBuilderReachTheOptionsTheyName()
    {
        const int readBlockSize = 4096;
        const int maximumTotalMessageSize = 64 * 1024;

        using AtProtoJetstream jetstream = AtProtoJetstream.CreateBuilder()
            .SetMaximumMessageSize(readBlockSize)
            .SetMaximumTotalMessageSize(maximumTotalMessageSize)
            .Build();

        // SetMaximumMessageSize configures the size of each block read from the socket, and it is
        // SetMaximumTotalMessageSize which bounds how large a message may be.
        Assert.Equal(readBlockSize, jetstream.Options.BufferSize);
        Assert.Equal(maximumTotalMessageSize, jetstream.Options.MaxMessageSize);
    }
}
