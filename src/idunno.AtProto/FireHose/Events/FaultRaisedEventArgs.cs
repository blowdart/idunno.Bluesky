// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.FireHose.Events;

/// <summary>
/// Encapsulates information given when a fault occurs in firehose processing.
/// </summary>
/// <param name="fault">The fault details.</param>
/// <remarks>
/// <para>Creates a new instance of <see cref="FaultRaisedEventArgs"/>.</para>
/// </remarks>
public class FaultRaisedEventArgs(string fault) : EventArgs
{
    /// <summary>
    /// The message received from the jetstream.
    /// </summary>
    public string Fault { get; } = fault;
}
