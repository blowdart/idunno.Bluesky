// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.BotSdk;

/// <summary>Indicates that a source event contains data the policy cannot process.</summary>
/// <remarks>
/// <para>A bot policy can throw this exception when deserialization or source-data validation fails. The reader logs
/// and audits the event, skips it, and advances replay progress. Do not use it for policy configuration or programming errors.</para>
/// </remarks>
public sealed class MalformedEventException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MalformedEventException"/> class.</summary>
    public MalformedEventException() : base("The source event contains data the policy cannot process.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MalformedEventException"/> class.</summary>
    /// <param name="message">The message that describes why the source event is malformed.</param>
    public MalformedEventException(string message) : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MalformedEventException"/> class.</summary>
    /// <param name="message">The message that describes why the source event is malformed.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public MalformedEventException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
