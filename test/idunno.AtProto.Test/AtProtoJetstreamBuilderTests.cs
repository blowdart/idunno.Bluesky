// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

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
    public void BuildPassesTheRemainingOptionsToTheJetstream()
    {
        TaskFactory taskFactory = new(TaskScheduler.Default);
        byte[] dictionary = [1, 2, 3];

        using AtProtoJetstream jetstream = AtProtoJetstreamBuilder.Create()
            .UseCompression(false)
            .WithCompressionDictionary(dictionary)
            .WithTaskFactory(taskFactory)
            .SetMaximumMessageSize(4096)
            .Build();

        Assert.False(jetstream.Options.UseCompression);
        Assert.Equal(dictionary, jetstream.Options.Dictionary);
        Assert.Same(taskFactory, jetstream.Options.TaskFactory);
        Assert.Equal(4096, jetstream.Options.BufferSize);
    }

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
}
