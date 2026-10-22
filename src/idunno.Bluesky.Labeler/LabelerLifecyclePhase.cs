// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>Identifies a labeler's lifecycle phase through operation, retirement and optional local cleanup.</summary>
/// <remarks>
/// <para>
/// A labeller lifecycle has the following phases:
/// </para>
/// <list type="number">
/// <item><description><see cref="Active"/>: apply labels, review reports and appeals, and publish corrections or negations.</description></item>
/// <item><description><see cref="Retiring"/>: stop ordinary issuance and work through existing history, saving retirement negations and progress.</description></item>
/// <item><description><see cref="Distributing"/>: finish making all issued labels and retirement negations available to readers.</description></item>
/// <item><description><see cref="Grace"/>: keep distribution running for a chosen period so consumers have an opportunity to catch up.</description></item>
/// <item><description><see cref="Retired"/>: complete retirement explicitly after the grace deadline; keep the database for as long as required.</description></item>
/// </list>
/// <para>
/// Only if the operator separately confirms local deletion does the store continue to <see cref="Purging"/>
/// and then <see cref="Purged"/>. These are optional cleanup phases after retirement, not prerequisites for it.
/// An empty history can skip <see cref="Retiring"/>.
/// </para>
/// <para>
/// A consumer of a labeller is software that reads and uses its published labels. For example, a Bluesky
/// AppView can subscribe to the labeller's label stream and include those labels in responses to a Bluesky
/// client. The client uses the person's subscriptions and moderation preferences to decide whether to show
/// a warning, hide content or display a label. Other applications can query labels or subscribe directly.
/// These are downstream consumers of the labeller's output, not the Jetstream or repository Firehose:
/// those streams supply account and record changes as input to the labeller's decision logic.
/// The label subscription stream is separate from that repository Firehose.
/// </para>
/// <para>
/// A labeller starts with a policy: decide which accounts or records to label, what each label means,
/// what evidence justifies applying it, and how reports, corrections and appeals will be handled.
/// Choose who can make those decisions and how their actions will be authorized and audited.
/// These decisions belong to the labeller's application and operators, not its persistence provider.
/// </para>
/// <para>
/// Before opening the service, set up its identity, generate a dedicated label-signing key and keep the private
/// key safe, separately from the label database. Publish the public verification key and label endpoint in
/// the identity's DID document, and publish a declaration describing the service and its labels.
/// For a hosted PDS account using a PLC identity, DID-document changes use the account-authorized PLC-operation
/// workflow through the PDS; the operator does not simply edit a document on the PDS.
/// Configure storage, distribution endpoints, backups and monitoring, then check that consumers can find
/// the service, verify its labels, query its history and subscribe to updates.
/// </para>
/// <para>
/// During <see cref="Active"/> operation, the application's decision logic examines records from Jetstream
/// or the Firehose, or acts on reviewed reports and moderator decisions. An approved decision becomes a signed
/// label identifying its issuer, subject and value, with a timestamp and any optional scope or expiry.
/// The store saves the exact signed representation and assigns a sequence; publication then makes it available
/// in order for queries and subscription replay. Consumers choose which labellers to follow and how to present
/// their labels. Publishing a label neither deletes the subject nor proves that every consumer has received it.
/// </para>
/// <para>
/// Corrections and appeals are part of normal operation. Review the evidence and record the authorized outcome
/// in the appropriate case and audit systems. To withdraw a label, issue, sign, save and publish a negation
/// with the matching subject, value and scope rather than editing or deleting the original statement.
/// A negation lets consumers learn that the decision has changed; deleting a local row does not.
/// Expiry is a separate way for a label to stop applying. The store retains history but does not decide
/// which labels currently apply or resolve reports and appeals.
/// </para>
/// <para>
/// To close the service, announce the shutdown and enter <see cref="Retiring"/>. This permanently stops
/// ordinary issuance and freezes the boundary of the existing history to examine. The caller works through
/// that history, decides which labels still need withdrawing, and appends signed retirement negations with
/// durable checkpoints so an interruption does not lose its place. Existing negations, expiry and later
/// decisions must be considered; retirement is not an instruction to negate every historical entry blindly.
/// </para>
/// <para>
/// Once enumeration is complete, <see cref="Distributing"/> allows the remaining issued labels, including
/// retirement negations, to be published. After all have been published, the caller begins <see cref="Grace"/>
/// and keeps queries and subscriptions running for a chosen grace period so disconnected consumers have an
/// opportunity to catch up. The deadline uses the database clock, survives restarts and establishes the
/// earliest time retirement can complete; it does not acknowledge downstream delivery.
/// </para>
/// <para>
/// After the deadline, the caller explicitly completes retirement and the store reaches <see cref="Retired"/>,
/// the end of its normal operating lifecycle. The operator can then take the service offline and update its
/// discovery information and public notices. Consider historical signature verification before removing public
/// keys; private-key destruction is a separate operational decision, not an effect of retiring the store.
/// </para>
/// <para>
/// A store may remain <see cref="Retired"/> indefinitely with its history intact. Deleting that history is a
/// separate, explicitly confirmed operation that progresses through <see cref="Purging"/> to <see cref="Purged"/>;
/// deletion is not required to complete retirement. Cleanup removes labels and operation receipts in resumable
/// batches while retaining the stream identity and sequence boundaries so old positions cannot be reused.
/// Follow the applicable retention and backup policies: local cleanup does not remove backups, destroy private
/// keys or erase copies held by downstream consumers.
/// </para>
/// <para>
/// This enumeration records the durable storage phases of that wider lifecycle, not every setup or moderation
/// activity described above.
/// The caller drives the work and transitions; the store does not run a host, schedule retirement or delete
/// data automatically. Retirement cannot be reversed to resume ordinary issuance.
/// </para>
/// </remarks>
public enum LabelerLifecyclePhase
{
    /// <summary>Allows ordinary signed-label issuance and publication before retirement begins.</summary>
    Active,
    /// <summary>Stops ordinary issuance while the caller works through existing history and appends retirement negations with resumable checkpoints.</summary>
    Retiring,
    /// <summary>Marks retirement enumeration as complete while waiting for all issued labels to be published and the caller to begin the grace period.</summary>
    Distributing,
    /// <summary>Retains published history for distribution until the grace deadline has passed and the caller explicitly completes retirement.</summary>
    Grace,
    /// <summary>Marks retirement as complete, retaining local history indefinitely unless the caller separately confirms its deletion.</summary>
    Retired,
    /// <summary>Records optional local cleanup after retirement while the caller removes labels and operation receipts in resumable batches.</summary>
    Purging,
    /// <summary>Marks optional local cleanup as complete, retaining stream metadata and sequence boundaries without reopening the store for issuance.</summary>
    Purged
}
