// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Retains an atomic case outcome and its exact signed-label receipts for recovery.</summary>
public sealed class LabelCaseDecisionReceipt
{
    /// <summary>Initializes a historical receipt from validated provider data.</summary>
    internal LabelCaseDecisionReceipt(
        LabelCaseDecision request,
        Did actor,
        LabelCaseReceipt caseEvent,
        IReadOnlyCollection<LabelOperationReceipt> labels,
        LabelStoreState stateAtCommit)
    {
        Request = request;
        Actor = actor;
        CaseEvent = caseEvent;
        Labels = Array.AsReadOnly(labels.ToArray());
        StateAtCommit = stateAtCommit;
    }

    /// <summary>Gets the original immutable decision intent.</summary>
    /// <value>The request retained for same-intent recovery.</value>
    public LabelCaseDecision Request { get; }
    /// <summary>Gets the original authenticated decision-maker.</summary>
    /// <value>The moderator DID, independent of current team state.</value>
    public Did Actor { get; }
    /// <summary>Gets the immutable case event and historical resolved snapshot.</summary>
    /// <value>The event committed with all labels.</value>
    public LabelCaseReceipt CaseEvent { get; }
    /// <summary>Gets the ordered exact action receipts.</summary>
    /// <value>The immutable signed or no-op outcomes.</value>
    public IReadOnlyList<LabelOperationReceipt> Labels { get; }
    /// <summary>Gets the final stream state of the atomic transaction.</summary>
    /// <value>The historical final state, not a current publication acknowledgement.</value>
    public LabelStoreState StateAtCommit { get; }
}
