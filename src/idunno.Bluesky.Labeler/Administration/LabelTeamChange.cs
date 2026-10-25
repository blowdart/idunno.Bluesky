// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Administration;

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
