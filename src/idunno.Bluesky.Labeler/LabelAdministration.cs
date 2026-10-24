// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler;

/// <summary>Identifies a labeler team member's authority.</summary>
public enum LabelTeamRole
{
    /// <summary>Permits administrative inspection.</summary>
    Viewer,
    /// <summary>Also permits issuing and withdrawing labels.</summary>
    Moderator,
    /// <summary>Also permits team, credential and maintenance administration.</summary>
    Administrator
}

/// <summary>Identifies an administrative permission independently of its transport.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "These are application permissions, not legacy code-access-security permission objects.")]
public enum LabelAdministrationPermission
{
    /// <summary>Permits administrative inspection.</summary>
    Inspect,
    /// <summary>Permits issuing and withdrawing labels.</summary>
    Moderate,
    /// <summary>Permits team and credential management.</summary>
    ManageTeam,
    /// <summary>Permits protected maintenance, but does not execute maintenance.</summary>
    Maintain
}

/// <summary>Identifies a durable team or credential change.</summary>
public enum LabelTeamAction
{
    /// <summary>Enrolls a verified account.</summary>
    Enroll,
    /// <summary>Changes an active or suspended member's role.</summary>
    SetRole,
    /// <summary>Suspends a member.</summary>
    Suspend,
    /// <summary>Reactivates a suspended member without restoring old bindings.</summary>
    Reinstate,
    /// <summary>Removes a member while retaining a generation tombstone.</summary>
    Remove,
    /// <summary>Binds an OS identity to an active member.</summary>
    BindLocal,
    /// <summary>Revokes an OS identity binding.</summary>
    RevokeLocal
}

/// <summary>Represents provider-issued authentication evidence for one administrative invocation.</summary>
/// <remarks><para>Only an authentication adapter may establish this evidence. Actor fields in requests
/// and <see cref="LabelOperationContext"/> do not establish authentication. Membership and credentials
/// are checked again for every operation and at mutation commit.</para></remarks>
public sealed class LabelAdministrationCaller
{
    /// <summary>Initializes immutable evidence after provider authentication.</summary>
    internal LabelAdministrationCaller(
        Did issuer,
        Did actor,
        long generation,
        string credential,
        string? localIdentity,
        DateTimeOffset expiresAt)
    {
        Issuer = issuer;
        Actor = actor;
        Generation = generation;
        Credential = credential;
        LocalIdentity = localIdentity;
        ExpiresAt = expiresAt;
    }

    /// <summary>Gets the labeler issuer.</summary>
    public Did Issuer { get; }
    /// <summary>Gets the authenticated or explicitly delegated team DID.</summary>
    public Did Actor { get; }
    /// <summary>Gets the authenticated OS identity, or <see langword="null"/> for service authentication.</summary>
    public string? LocalIdentity { get; }
    internal long Generation { get; }
    internal string Credential { get; }
    internal DateTimeOffset ExpiresAt { get; }
}

/// <summary>Describes an immutable, verified account enrollment or team change.</summary>
/// <remarks><para>For enrollment, the application service verifies the target before submitting it
/// to storage. A handle is never an authorization identifier.</para></remarks>
public sealed class LabelTeamChange
{
    /// <summary>Initializes a team change.</summary>
    /// <param name="operationId">The stable retry identity.</param>
    /// <param name="action">The requested change.</param>
    /// <param name="target">The target member DID.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="role">The role for enrollment or role changes.</param>
    /// <param name="credential">The OS identity for a local-binding change, when applicable.</param>
    /// <exception cref="ArgumentException">A text argument is invalid.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum value or text length is invalid.</exception>
    public LabelTeamChange(
        string operationId,
        LabelTeamAction action,
        Did target,
        string reason,
        LabelTeamRole role = LabelTeamRole.Viewer,
        string? credential = null)
    {
        if (!Enum.IsDefined(action) || !Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        ArgumentNullException.ThrowIfNull(target);
        OperationId = LabelInput.ValidateText(operationId, nameof(operationId), 256);
        Reason = LabelInput.ValidateText(reason, nameof(reason), 4096);
        Target = target;
        Action = action;
        Role = role;
        Credential = credential is null ? null : LabelInput.ValidateText(credential, nameof(credential), 256);
        if (action is LabelTeamAction.BindLocal or LabelTeamAction.RevokeLocal)
        {
            ValidateLocalIdentity(credential);
        }
    }

    /// <summary>Gets the stable retry identity.</summary>
    public string OperationId { get; }
    /// <summary>Gets the requested change.</summary>
    public LabelTeamAction Action { get; }
    /// <summary>Gets the target member DID.</summary>
    public Did Target { get; }
    /// <summary>Gets the audit reason.</summary>
    public string Reason { get; }
    /// <summary>Gets the requested role.</summary>
    public LabelTeamRole Role { get; }
    /// <summary>Gets the OS identity, when applicable.</summary>
    public string? Credential { get; }

    /// <summary>Requires a stable numeric Unix UID or Windows SID rather than a mutable account name.</summary>
    internal static void ValidateLocalIdentity(string? identity)
    {
        if (identity is null || identity.Length > 256 ||
            !(identity.StartsWith("uid:", StringComparison.Ordinal) &&
                uint.TryParse(identity.AsSpan(4), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out uint uid) &&
                identity == $"uid:{uid}" ||
              identity.StartsWith("sid:S-1-", StringComparison.Ordinal) &&
                identity.Skip(6).All(character => char.IsAsciiDigit(character) || character == '-')))
        {
            throw new ArgumentException("Use uid:<decimal UID> or sid:<Windows SID>.", nameof(identity));
        }
    }
}

/// <summary>Describes a retained team member.</summary>
/// <param name="Did">The stable member DID.</param>
/// <param name="Role">The assigned role.</param>
/// <param name="Active">Whether the member can currently act.</param>
/// <param name="Removed">Whether this entry is a removal tombstone.</param>
public sealed record LabelTeamMember(Did Did, LabelTeamRole Role, bool Active, bool Removed);

/// <summary>Describes a durable administration receipt or audit event.</summary>
/// <param name="Sequence">The issuer-local audit sequence.</param>
/// <param name="OperationId">The operation identity, or an empty value for an authorization event.</param>
/// <param name="Actor">The authenticated or delegated actor DID, or <see langword="null"/> for an unbound OS identity.</param>
/// <param name="LocalIdentity">The authenticated OS identity, when applicable.</param>
/// <param name="Action">The operation or permission.</param>
/// <param name="Reason">The supplied reason.</param>
/// <param name="Succeeded">Whether the operation was authorized and committed.</param>
public sealed record LabelAdministrationReceipt(
    long Sequence,
    string OperationId,
    Did? Actor,
    string? LocalIdentity,
    string Action,
    string Reason,
    bool Succeeded);

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
    Task<LabelServiceResult<LabelAdministrationReceipt>> ChangeTeamAsync(
        LabelAdministrationCaller caller,
        LabelTeamChange change,
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
    Task<LabelServiceResult<IReadOnlyList<LabelAdministrationReceipt>>> GetAdministrationAuditAsync(
        LabelAdministrationCaller caller,
        long afterSequence = 0,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>Distinguishes a maintenance coordinator's authorization intent from its known execution outcome.</summary>
public enum LabelMaintenanceAuditPhase
{
    /// <summary>The authorized coordinator intends to perform maintenance.</summary>
    Intent,
    /// <summary>The coordinator established successful completion.</summary>
    Completed,
    /// <summary>The coordinator established failure.</summary>
    Failed
}
