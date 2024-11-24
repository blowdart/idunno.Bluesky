// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Labels
{
    /// <summary>
    /// Metadata tags on an atproto record, published by the author within the record.
    /// </summary>
    public sealed record SelfLabels
    {
        /// <summary>
        /// Creates a new instance of <see cref="SelfLabels"/>.
        /// </summary>
        /// <param name="labels">The collection of labels applied to the record.</param>
        [JsonConstructor]
        internal SelfLabels(IReadOnlyCollection<SelfLabel> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(values.Count, 10);

            Values = new List<SelfLabel>().AsReadOnly();
        }

        /// <summary>
        /// The collection of self labels applied to the record.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyCollection<SelfLabel> Values { get; init; }
    }
}
