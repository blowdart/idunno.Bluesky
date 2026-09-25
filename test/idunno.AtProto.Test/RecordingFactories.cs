// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace idunno.AtProto.Test;

/// <summary>
/// An <see cref="IHttpClientFactory"/> which records the names it was asked for.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class RecordingHttpClientFactory : IHttpClientFactory
{
    internal List<string> RequestedNames { get; } = [];

    public HttpClient CreateClient(string name)
    {
        RequestedNames.Add(name);

        return new HttpClient();
    }
}

/// <summary>
/// An <see cref="IMeterFactory"/> which records the meter names it was asked to create.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class RecordingMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    internal List<string> CreatedMeterNames { get; } = [];

    public Meter Create(MeterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        CreatedMeterNames.Add(options.Name);

        Meter meter = new(options.Name, options.Version, options.Tags, scope: this);
        _meters.Add(meter);

        return meter;
    }

    public void Dispose()
    {
        foreach (Meter meter in _meters)
        {
            meter.Dispose();
        }

        _meters.Clear();
    }
}
