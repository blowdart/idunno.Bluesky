// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler
{
    /// <summary>
    /// Encapsulates a detailed view over a labeller.
    /// </summary>
    public sealed record LabelerViewDetailed : LabelerView
    {
        /// <summary>
        /// Gets the policies for the labeler
        /// </summary>
        public required LabelerPolicies Policies { get; set; }

        /// <summary>
        /// The set of report reason 'codes' which are in-scope for this service to review and action.
        /// These usually align to policy categories.
        /// If null (distinct from empty array), all report reason types are allowed.
        /// </summary>
        public ICollection<string>? ReasonTypes { get; init; }

        /// <summary>
        /// Gets the set of subject types (account, record, etc) this service accepts reports on.
        /// </summary>
        public IEnumerable<string>? SubjectTypes { get; init; }

        /// <summary>
        /// Gets the set of record types (collection NSIDs) which can be reported to this service.
        /// If null (as distinct from empty array), the default is any collection.
        /// </summary>
        public ICollection<Nsid>? SubjectCollections { get; init; }
    }
}
