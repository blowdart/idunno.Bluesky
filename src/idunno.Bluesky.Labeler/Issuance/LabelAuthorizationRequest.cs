// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Issuance;

/// <summary>Describes a service-created authorization check for an issuer operation.</summary>
/// <remarks>
/// <para>The service creates this immutable request so an embedding authorizer can make a decision using the current caller and available target context.</para>
/// <para>An admission check occurs before target resolution or operation lookup. A later check can include the resolved or stored target before a decision is committed or a receipt is returned.</para>
/// </remarks>
public sealed class LabelAuthorizationRequest
{
    /// <summary>Initializes a service authorization request.</summary>
    /// <param name="issuer">The label issuer.</param>
    /// <param name="action">The operation to authorize.</param>
    /// <param name="context">The caller context.</param>
    /// <param name="operationId">The optional service-operation identifier.</param>
    /// <param name="requestedTarget">The optional target supplied by the caller.</param>
    /// <param name="resolvedTarget">The optional resolved or stored target.</param>
    /// <param name="value">The optional label value.</param>
    /// <param name="isAdmissionCheck"><see langword="true"/> to identify the coarse preflight check; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="issuer"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A supplied operation identifier or value is blank, contains U+0000, or is not valid UTF-8.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A supplied operation identifier exceeds 256 UTF-8 bytes or a value exceeds 128 UTF-8 bytes.</exception>
    /// <remarks>
    /// <para>The constructor is internal: host code receives requests through <see cref="ILabelIssuanceAuthorizer"/> and cannot manufacture service checks.</para>
    /// </remarks>
    internal LabelAuthorizationRequest(
        Did issuer,
        LabelAuthorizationAction action,
        LabelOperationContext context,
        string? operationId,
        LabelTarget? requestedTarget,
        LabelTarget? resolvedTarget,
        string? value,
        bool isAdmissionCheck)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "The authorization action is not defined.");
        }

        Issuer = issuer;
        Action = action;
        Context = context;
        OperationId = operationId is null ? null : LabelInput.ValidateText(operationId, nameof(operationId), 256);
        RequestedTarget = requestedTarget;
        ResolvedTarget = resolvedTarget;
        Value = value is null ? null : LabelInput.ValidateText(value, nameof(value), 128);
        IsAdmissionCheck = isAdmissionCheck;
    }

    /// <summary>Gets the label issuer.</summary>
    /// <value>The issuer DID.</value>
    public Did Issuer { get; }

    /// <summary>Gets the requested authorization action.</summary>
    /// <value>The action.</value>
    public LabelAuthorizationAction Action { get; }

    /// <summary>Gets the trusted caller context.</summary>
    /// <value>The immutable caller context.</value>
    public LabelOperationContext Context { get; }

    /// <summary>Gets the optional service-operation identifier.</summary>
    /// <value>The operation identifier, or <see langword="null"/> when the action has no operation key.</value>
    public string? OperationId { get; }

    /// <summary>Gets the optional target as supplied by the caller.</summary>
    /// <value>The requested target, or <see langword="null"/> for issuer-scoped operations.</value>
    public LabelTarget? RequestedTarget { get; }

    /// <summary>Gets the optional resolved or stored target.</summary>
    /// <value>The resolved target, or <see langword="null"/> when none applies.</value>
    public LabelTarget? ResolvedTarget { get; }

    /// <summary>Gets the optional label value associated with the action.</summary>
    /// <value>The label value, or <see langword="null"/> when the action is not value-scoped.</value>
    public string? Value { get; }

    /// <summary>Gets a value that indicates whether this is a coarse admission check.</summary>
    /// <value><see langword="true"/> for the preflight check; otherwise, <see langword="false"/>.</value>
    public bool IsAdmissionCheck { get; }
}
