// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Jetstream.Archive;

/// <summary>
/// Specifies how snapshot or replay handles an invalid archive record.
/// </summary>
public enum JetstreamRecordErrorAction
{
    /// <summary>
    /// Stops enumeration by rethrowing the record decoding exception.
    /// </summary>
    Stop,

    /// <summary>
    /// Skips the invalid record and continues with the remaining records.
    /// </summary>
    Skip
}
