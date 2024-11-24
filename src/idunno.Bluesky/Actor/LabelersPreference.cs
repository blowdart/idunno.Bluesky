// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// A user's labelers preferences
    /// </summary>
    public sealed record LabelersPreference : Preference
    {
        /// <summary>
        /// Creates a new instance of <see cref="LabelersPreference"/> from the specified <paramref name="labelers"/>.
        /// </summary>
        /// <param name="labelers">A list of <see cref="LabelerPreference"/>s.</param>
        [JsonConstructor]
        public LabelersPreference(IReadOnlyList<LabelerPreference> labelers)
        {
            Labelers = labelers;
        }

        /// <summary>
        /// Gets the list of <see cref="LabelersPreference"/>.
        /// </summary>
        [JsonInclude]
        public IReadOnlyList<LabelerPreference> Labelers { get; init; }
    }

    /// <summary>
    /// A preference for an individual labeler.
    /// </summary>
    public record LabelerPreference
    {
        /// <summary>
        /// Creates a new instance of <see cref="LabelerPreference"/>.
        /// </summary>
        /// <param name="did">The <paramref name="did"/> of the labeler this preference applies to.</param>
        [JsonConstructor]
        public LabelerPreference(Did did)
        {
            ArgumentNullException.ThrowIfNull(did, nameof(did));
            Did = did;
        }

        /// <summary>
        /// The <see cref="Did"/> of the labeler.
        /// </summary>
        [JsonRequired]
        public Did Did { get; init; }
    }
}
