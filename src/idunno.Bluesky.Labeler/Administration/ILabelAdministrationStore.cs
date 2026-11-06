// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;

namespace idunno.Bluesky.Labeler.Administration;

/// <summary>Provides issuer-bound team persistence and transaction-valid authorization.</summary>
/// <remarks><para>These are trusted infrastructure APIs, not endpoints. Only authenticated transport
/// adapters may call authentication methods; never populate their inputs from a request-body identity.
/// All providers must serialize team changes with label-decision fences.</para></remarks>
public interface ILabelAdministrationStore
{
    /// <summary>Checks live maintenance permission and records a coordinator's intent or known result.</summary>
    /// <param name="caller">The authenticated invocation.</param>
    /// <param name="operationId">The coordinator's stable operation identity.</param>
    /// <param name="reason">The bounded audit reason.</param>
    /// <param name="phase">Whether this records intent, completion or failure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The durable audit event or an explicit authorization/persistence failure.</returns>
    /// <remarks><para>This does not execute maintenance, acquire a coordination lease or replace commit-time authorization.</para></remarks>
    Task<LabelServiceResult<LabelAdministrationReceipt>> AuditMaintenanceAsync(
        LabelAdministrationCaller caller,
        string operationId,
        string reason,
        LabelMaintenanceAuditPhase phase = LabelMaintenanceAuditPhase.Intent,
        CancellationToken cancellationToken = default);

    /// <summary>Durably records a rejected authentication or malformed invocation at a trusted adapter boundary.</summary>
    /// <param name="reason">A bounded failure category, never raw credentials or request content.</param>
    /// <param name="caller">Provider-issued caller evidence, if authentication had already succeeded.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The denied audit event, or an explicit persistence failure.</returns>
    /// <remarks><para>This trusted infrastructure seam must not itself be exposed as an administrative endpoint.</para></remarks>
    Task<LabelServiceResult<LabelAdministrationReceipt>> RecordAdministrationDenialAsync(
        string reason,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically enrolls the first administrator and local binding exactly once.</summary>
    /// <param name="administrator">The independently verified administrator DID.</param>
    /// <param name="localIdentity">The OS-authorized setup identity.</param>
    /// <param name="operationId">The stable setup retry identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original or newly committed bootstrap receipt.</returns>
    Task<LabelServiceResult<LabelAdministrationReceipt>> BootstrapAsync(
        Did administrator,
        string localIdentity,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>Authenticates a kernel-established local peer against its live binding.</summary>
    /// <param name="localIdentity">The peer UID or SID established by the protected transport.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Invocation evidence or an explicit denial.</returns>
    Task<LabelServiceResult<LabelAdministrationCaller>> AuthenticateLocalAsync(
        string localIdentity,
        CancellationToken cancellationToken = default);

    /// <summary>Creates short-lived caller evidence for an authenticated browser identity.</summary>
    /// <param name="actor">The DID established by a trusted browser authentication handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Invocation evidence for an active member or an explicit denial.</returns>
    /// <remarks><para>Call only after validating the browser's authenticated principal. This method does not
    /// authenticate an identity itself. It rechecks live membership and issues expiring provider-backed
    /// evidence that is revalidated for every operation and mutation commit.</para></remarks>
    Task<LabelServiceResult<LabelAdministrationCaller>> AuthenticateBrowserAsync(
        Did actor,
        CancellationToken cancellationToken = default);

    /// <summary>Consumes a verified service token's replay identity across all hosts.</summary>
    /// <param name="authentication">The protocol verifier's successful result.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Invocation evidence or an explicit denial.</returns>
    Task<LabelServiceResult<LabelAdministrationCaller>> AuthenticateServiceAsync(
        ServiceAuthenticationResult authentication,
        CancellationToken cancellationToken = default);

    /// <summary>Checks a live permission and durably audits the decision.</summary>
    /// <param name="caller">The provider-issued invocation evidence.</param>
    /// <param name="permission">The required permission.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorization grant or a classified denial.</returns>
    Task<LabelServiceResult<LabelAuthorizationGrant>> AuthorizeAsync(
        LabelAdministrationCaller caller,
        LabelAdministrationPermission permission,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Applies a team change and audit atomically after checking live administrator authority.</summary>
    /// <param name="caller">The authenticated invocation evidence.</param>
    /// <param name="change">The verified team change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original or newly committed receipt, or a classified denial.</returns>
    /// <remarks><para>The current owner cannot be removed, suspended or demoted. Only that owner
    /// may transfer ownership to another active administrator. Ownership, protection and remote-administration
    /// control move atomically, without changing either account's role or local bindings.</para></remarks>
    Task<LabelServiceResult<LabelAdministrationReceipt>> ChangeTeamAsync(
        LabelAdministrationCaller caller,
        LabelTeamChange change,
        CancellationToken cancellationToken = default);

    /// <summary>Registers or clears an active administrator's policy-alert email under live team authority.</summary>
    /// <param name="caller">The authenticated invocation evidence.</param>
    /// <param name="change">The validated address change and audit reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original or newly committed receipt, or a classified denial.</returns>
    /// <remarks><para>The address is returned only to callers with current team-management permission and is
    /// never written to audit text. Only active administrators receive policy-verification notifications.</para></remarks>
    Task<LabelServiceResult<LabelAdministrationReceipt>> SetTeamNotificationEmailAsync(
        LabelAdministrationCaller caller,
        LabelTeamNotificationEmailChange change,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the bounded team after checking live inspection authority.</summary>
    /// <param name="caller">The authenticated invocation evidence.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The members, including tombstones, or a denial.</returns>
    Task<LabelServiceResult<IReadOnlyList<LabelTeamMember>>> GetTeamAsync(
        LabelAdministrationCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a bounded page of administrative audit events.</summary>
    /// <param name="caller">The authenticated invocation evidence.</param>
    /// <param name="afterSequence">The exclusive audit position.</param>
    /// <param name="limit">The maximum count, between one and one thousand.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The audit page or a denial.</returns>
    /// <remarks><para>Successful reads do not append events to the feed being read. Authorization denials are
    /// still audited. Pages are live rather than a fixed snapshot; concurrent administrative activity can add events.</para></remarks>
    Task<LabelServiceResult<IReadOnlyList<LabelAdministrationReceipt>>> GetAdministrationAuditAsync(
        LabelAdministrationCaller caller,
        long afterSequence = 0,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the durable remote-administration switch after checking current inspection authority.</summary>
    /// <param name="caller">The provider-issued invocation evidence.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted switch state or a classified authorization/persistence failure.</returns>
    Task<LabelServiceResult<LabelRemoteAdministrationStatus>> GetRemoteAdministrationStatusAsync(
        LabelAdministrationCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>Changes the durable remote-administration switch under the current owner's authority.</summary>
    /// <param name="caller">The provider-issued invocation evidence.</param>
    /// <param name="operationId">The stable operation identity for retries.</param>
    /// <param name="enabled">A value that indicates whether remote administration should be enabled.</param>
    /// <param name="reason">The bounded audit reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original or newly committed audit receipt, or a classified denial.</returns>
    /// <remarks><para>Only the current owner, initially the DID that completed the one-time local bootstrap, may change this setting.
    /// The update and its audit receipt commit atomically. An existing installation's bootstrap audit
    /// is used to identify its owner when its persisted team predates this setting.</para></remarks>
    Task<LabelServiceResult<LabelAdministrationReceipt>> SetRemoteAdministrationEnabledAsync(
        LabelAdministrationCaller caller,
        string operationId,
        bool enabled,
        string reason,
        CancellationToken cancellationToken = default);
}

/// <summary>Describes the persisted remote-administration switch and the caller's owner status.</summary>
public sealed class LabelRemoteAdministrationStatus
{
    /// <summary>Initializes a status projection for a validated administration caller.</summary>
    /// <param name="enabled">A value that indicates whether the persisted remote-administration switch is enabled.</param>
    /// <param name="isOwner">A value that indicates whether the caller is the current labeler owner.</param>
    internal LabelRemoteAdministrationStatus(bool enabled, bool isOwner)
    {
        Enabled = enabled;
        IsOwner = isOwner;
    }

    /// <summary>Gets a value that indicates whether the persisted remote-administration switch is enabled.</summary>
    /// <value><see langword="true"/> if the persisted switch is enabled; otherwise, <see langword="false"/>.</value>
    public bool Enabled { get; }

    /// <summary>Gets a value that indicates whether the caller is the current labeler owner.</summary>
    /// <value><see langword="true"/> if the caller is the current labeler owner; otherwise, <see langword="false"/>.</value>
    public bool IsOwner { get; }
}
