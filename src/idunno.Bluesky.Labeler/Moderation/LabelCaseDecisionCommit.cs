// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Supplies previously prepared actions for one atomic case, label, receipt and audit transaction.</summary>
/// <remarks><para>This is trusted application infrastructure. The provider rechecks authority,
/// exact case subject, case and policy revisions, current labels and shared issuance fences.
/// Preparation must not commit any individual action.</para></remarks>
public sealed class LabelCaseDecisionCommit
{
    /// <summary>Initializes a new instance of the <see cref="LabelCaseDecisionCommit"/> class.</summary>
    /// <param name="caller">The provider-issued moderator.</param>
    /// <param name="request">The complete immutable decision intent.</param>
    /// <param name="expectedPolicyRevision">The policy revision observed before preparation.</param>
    /// <param name="preparedActions">The ordered actions prepared by the shared issuance service.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Prepared actions are <see langword="null"/> or differ in count from the intent.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expectedPolicyRevision"/> is not positive.</exception>
    public LabelCaseDecisionCommit(
        LabelAdministrationCaller caller,
        LabelCaseDecision request,
        long expectedPolicyRevision,
        IReadOnlyCollection<LabelDecisionCommit> preparedActions)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(preparedActions);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedPolicyRevision);
        LabelDecisionCommit[] copy = [.. preparedActions];
        if (copy.Length != request.Actions.Count || copy.Any(action => action is null))
        {
            throw new ArgumentException("Every requested action requires exactly one prepared action.", nameof(preparedActions));
        }

        Caller = caller;
        Request = request;
        ExpectedPolicyRevision = expectedPolicyRevision;
        PreparedActions = Array.AsReadOnly(copy);
    }

    /// <summary>Gets the provider-issued moderator checked at commit.</summary>
    /// <value>The authenticated caller evidence.</value>
    public LabelAdministrationCaller Caller { get; }
    /// <summary>Gets the complete immutable case decision.</summary>
    /// <value>The same-intent request.</value>
    public LabelCaseDecision Request { get; }
    /// <summary>Gets the policy revision observed before network or signing work.</summary>
    /// <value>The positive optimistic policy fence.</value>
    public long ExpectedPolicyRevision { get; }
    /// <summary>Gets the exact prepared actions in request order.</summary>
    /// <value>The immutable signed or no-op preparations.</value>
    public IReadOnlyList<LabelDecisionCommit> PreparedActions { get; }
}
