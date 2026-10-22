// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>Preserves the commit-submission boundary when a database acknowledgement is lost.</summary>
/// <remarks><para>This exception is consumed only by typed service commits. Legacy append behavior
/// continues to propagate the original database exception, preserving its existing contract.</para></remarks>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Private transport marker always requires the original commit exception.")]
[SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "Consumed within the provider; the public boundary returns OutcomeUnknown.")]
[SuppressMessage("Major Code Smell", "S3871:Exception types should be public", Justification = "Consumed within the provider; the public boundary returns OutcomeUnknown.")]
internal sealed class DecisionCommitUncertainException(Exception innerException)
    : Exception("The decision commit outcome must be recovered by operation identity.", innerException);
