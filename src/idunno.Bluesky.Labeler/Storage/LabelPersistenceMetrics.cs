// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>Identifies the persistence meter used by all labeler providers.</summary>
public static class LabelPersistenceMetrics
{
    /// <summary>Gets the meter name to subscribe to with a metrics listener or exporter.</summary>
    public static string MeterName => "idunno.Bluesky.Labeler.Persistence";

    /// <summary>Gets the instrument contract version.</summary>
    public static string MeterVersion => "1.0.0";
}
