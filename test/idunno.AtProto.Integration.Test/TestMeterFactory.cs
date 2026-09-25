// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace idunno.AtProto.Integration.Test;

/// <summary>
/// An <see cref="IMeterFactory"/> whose meters are scoped to the factory, so a test only sees its own measurements.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class TestMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(options.Name, options.Version, options.Tags, scope: this);

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
