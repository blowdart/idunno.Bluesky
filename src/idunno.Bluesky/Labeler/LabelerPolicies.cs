// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Labels;

namespace idunno.Bluesky.Labeler
{
    /// <summary>
    /// The policies for a labeler
    /// </summary>
    public sealed record LabelerPolicies
    {
        /// <summary>
        /// Gets the description of the labeler, if any.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Gets the label values which this labeler publishes. May include global or custom labels.
        /// </summary>
        public required ICollection<string> LabelValues { get; init; }

        /// <summary>
        /// Gets the label values created by this labeler and scoped exclusively to it. Labels defined here will override global label definitions for this labeler.
        /// </summary>
        public ICollection<LabelValueDefinition> LabelValueDefinitions { get; init; } = [];
    }
}
