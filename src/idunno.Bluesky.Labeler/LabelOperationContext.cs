// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler;

/// <summary>Captures the trusted caller identity and explanatory context for a label operation.</summary>
/// <param name="actor">The trusted caller's actor DID.</param>
/// <param name="reason">The nonblank reason for the operation.</param>
/// <param name="correlationId">An optional nonblank diagnostic correlation identifier.</param>
/// <exception cref="ArgumentNullException"><paramref name="actor"/> is <see langword="null"/>.</exception>
/// <exception cref="ArgumentException"><paramref name="reason"/> or <paramref name="correlationId"/> is blank, contains U+0000, or is not valid UTF-8.</exception>
/// <exception cref="ArgumentOutOfRangeException">The reason exceeds 4,096 UTF-8 bytes or the correlation identifier exceeds 256 UTF-8 bytes.</exception>
/// <remarks>
/// <para>The embedding host is responsible for deriving <see cref="Actor"/> from an authenticated context. A context instance does not itself authenticate or authorize its actor.</para>
/// <para>The reason and optional correlation identifier are bounded text, not evidence or proof of authorization.</para>
/// </remarks>
public sealed class LabelOperationContext(Did actor, string reason, string? correlationId = null)
{
    /// <summary>Gets the trusted caller's actor DID.</summary>
    /// <value>The actor DID.</value>
    /// <exception cref="ArgumentNullException">The actor supplied during construction is <see langword="null"/>.</exception>
    public Did Actor { get; } = actor ?? throw new ArgumentNullException(nameof(actor));

    /// <summary>Gets the reason supplied for the operation.</summary>
    /// <value>The nonblank reason.</value>
    public string Reason { get; } = LabelInput.ValidateText(reason, nameof(reason), 4096);

    /// <summary>Gets the optional diagnostic correlation identifier.</summary>
    /// <value>The correlation identifier, or <see langword="null"/> when none was supplied.</value>
    public string? CorrelationId { get; } = correlationId is null ? null : LabelInput.ValidateText(correlationId, nameof(correlationId), 256);
}
