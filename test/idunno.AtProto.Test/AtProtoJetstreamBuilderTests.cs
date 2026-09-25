// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto.Jetstream;

namespace idunno.AtProto.Test;

/// <summary>
/// Covers what <see cref="AtProtoJetstreamBuilder.Build()"/> carries through to the jetstream it creates.
/// </summary>
/// <remarks>
/// <para>
///   Anything the builder collects and then drops is silently ignored, because every one of these is optional and a
///   jetstream falls back to a default rather than failing.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class AtProtoJetstreamBuilderTests
{
    [Fact]
    public void BuildPassesTheMeterFactoryToTheJetstream()
    {
        using RecordingMeterFactory meterFactory = new();

        using AtProtoJetstream jetstream = AtProtoJetstreamBuilder.Create()
            .WithMeterFactory(meterFactory)
            .Build();

        Assert.Equal(JetstreamMetrics.MeterName, Assert.Single(meterFactory.CreatedMeterNames));
    }

    [Fact]
    public void BuildPassesTheHttpClientFactoryToTheJetstream()
    {
        RecordingHttpClientFactory httpClientFactory = new();

        using AtProtoJetstream jetstream = AtProtoJetstreamBuilder.Create()
            .WithHttpClientFactory(httpClientFactory)
            .Build();

        Assert.Equal(Agent.HttpClientName, Assert.Single(httpClientFactory.RequestedNames));
    }

    [Fact]
    public void BuildPassesTheConfiguredHttpClientOptionsToTheJetstream()
    {
        using AtProtoJetstream jetstream = AtProtoJetstreamBuilder.Create()
            .ConfigureHttpClientOptions(options => options.HttpUserAgent = "test/3.0")
            .Build();

        Assert.Equal("test/3.0", jetstream._httpClientOptions?.HttpUserAgent);
    }

    [Fact]
    public void BuildIgnoresHttpClientOptionsWhenAnHttpClientFactoryIsSupplied()
    {
        RecordingHttpClientFactory httpClientFactory = new();

        using AtProtoJetstream jetstream = AtProtoJetstreamBuilder.Create()
            .ConfigureHttpClientOptions(options => options.HttpUserAgent = "test/3.0")
            .WithHttpClientFactory(httpClientFactory)
            .Build();

        // The factory owns how its clients are configured, so carrying the options over would suggest they applied.
        Assert.Null(jetstream._httpClientOptions);
    }

    [Fact]
    public void ABuiltJetstreamUsesCompressionUnlessItIsTurnedOff()
    {
        // The builder used to leave compression off by default while JetstreamOptions turned it on, so building a
        // jetstream rather than constructing one silently gave up compression.
        using AtProtoJetstream jetstream = AtProtoJetstreamBuilder.Create().Build();

        Assert.True(jetstream.Options.UseCompression);
        Assert.Equal(new JetstreamOptions().UseCompression, jetstream.Options.UseCompression);
    }

    [Fact]
    public void BuildPassesTheRemainingOptionsToTheJetstream()
    {
        TaskFactory taskFactory = new(TaskScheduler.Default);
        byte[] dictionary = [1, 2, 3];

        using AtProtoJetstream jetstream = AtProtoJetstreamBuilder.Create()
            .UseCompression(false)
            .WithCompressionDictionary(dictionary)
            .WithTaskFactory(taskFactory)
            .SetReadBufferSize(4096)
            .SetMaximumTotalMessageSize(65536)
            .Build();

        Assert.False(jetstream.Options.UseCompression);
        Assert.Equal(dictionary, jetstream.Options.Dictionary);
        Assert.Same(taskFactory, jetstream.Options.TaskFactory);
        Assert.Equal(4096, jetstream.Options.BufferSize);
        Assert.Equal(65536, jetstream.Options.MaxMessageSize);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildPassesTheWebSocketOptionsToTheJetstream(bool withHttpClientFactory)
    {
        WebProxy proxy = new("http://localhost:8866");
        WebSocketOptions webSocketOptions = new()
        {
            Proxy = proxy,
            KeepAliveInterval = TimeSpan.FromSeconds(42),
        };

        AtProtoJetstreamBuilder builder = AtProtoJetstreamBuilder.Create().WithWebSocketOptions(webSocketOptions);

        if (withHttpClientFactory)
        {
            builder.WithHttpClientFactory(new RecordingHttpClientFactory());
        }

        using AtProtoJetstream jetstream = builder.Build();

        Assert.Same(proxy, jetstream.WebSocketOptions.Proxy);
        Assert.Equal(TimeSpan.FromSeconds(42), jetstream.WebSocketOptions.KeepAliveInterval);
    }

    [Fact]
    public void WithWebSocketOptionsRejectsNull() =>
        Assert.Throws<ArgumentNullException>(
            "webSocketOptions",
            () => AtProtoJetstreamBuilder.Create().WithWebSocketOptions(null!));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetReadBufferSizeRejectsSizesWhichAreNotPositive(int readBufferSize) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            "readBufferSize",
            () => AtProtoJetstreamBuilder.Create().SetReadBufferSize(readBufferSize));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetMaximumTotalMessageSizeRejectsSizesWhichAreNotPositive(int maximumTotalMessageSize) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            "maximumTotalMessageSize",
            () => AtProtoJetstreamBuilder.Create().SetMaximumTotalMessageSize(maximumTotalMessageSize));

    [Fact]
    public void WithHttpClientFactoryRejectsNull() =>
        Assert.Throws<ArgumentNullException>(
            "httpClientFactory",
            () => AtProtoJetstreamBuilder.Create().WithHttpClientFactory(null!));

    [Fact]
    public void ConfigureHttpClientOptionsRejectsNull() =>
        Assert.Throws<ArgumentNullException>(
            "configure",
            () => AtProtoJetstreamBuilder.Create().ConfigureHttpClientOptions(null!));

    [Theory]
    [MemberData(nameof(NullRejectingProperties))]
    public void SettingAPropertyToNullThrows(Action<AtProtoJetstreamBuilder> set) =>
        Assert.Throws<ArgumentNullException>(() => set(AtProtoJetstreamBuilder.Create()));

    [Theory]
    [MemberData(nameof(OutOfRangeProperties))]
    public void SettingAPropertyOutOfRangeThrows(Action<AtProtoJetstreamBuilder> set) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => set(AtProtoJetstreamBuilder.Create()));

    /// <summary>
    /// The properties whose fluent counterparts reject <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// <para>Setting one of these directly used to be accepted where the fluent method rejected it, so which of the two
    /// ways of configuring a builder was used decided whether a mistake was caught.</para>
    /// </remarks>
    public static TheoryData<Action<AtProtoJetstreamBuilder>> NullRejectingProperties()
    {
        TheoryData<Action<AtProtoJetstreamBuilder>> data = [];

        data.Add(builder => builder.Service = null!);
        data.Add(builder => builder.LoggerFactory = null!);
        data.Add(builder => builder.CompressionDictionary = null!);
        data.Add(builder => builder.TaskFactory = null!);

        return data;
    }

    /// <summary>
    /// The properties whose fluent counterparts reject values which are equal to, or less than, zero.
    /// </summary>
    public static TheoryData<Action<AtProtoJetstreamBuilder>> OutOfRangeProperties()
    {
        TheoryData<Action<AtProtoJetstreamBuilder>> data = [];

        data.Add(builder => builder.ReadBufferSize = 0);
        data.Add(builder => builder.ReadBufferSize = -1);
        data.Add(builder => builder.MaximumTotalMessageSize = 0);
        data.Add(builder => builder.MaximumTotalMessageSize = -1);
        data.Add(builder => builder.MaximumConcurrentMessageParsers = 0);
        data.Add(builder => builder.MaximumConcurrentMessageParsers = -1);
        data.Add(builder => builder.CloseTimeout = TimeSpan.Zero);
        data.Add(builder => builder.CloseTimeout = TimeSpan.FromSeconds(-1));

        return data;
    }

    [Fact]
    public void BuildPassesTheMaximumConcurrentMessageParsersToTheJetstream()
    {
        using AtProtoJetstream jetstream = AtProtoJetstreamBuilder.Create()
            .SetMaximumConcurrentMessageParsers(7)
            .Build();

        Assert.Equal(7, jetstream.Options.MaximumConcurrentMessageParsers);
    }
}
