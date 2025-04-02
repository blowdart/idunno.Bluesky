// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Labels
{
    /// <summary>
    /// Metadata tags on an atproto record, published by the author within the record.
    /// </summary>
    public sealed class SelfLabels
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly object _syncLock = new();

        /// <summary>
        /// Creates a new instance of <see cref="SelfLabels"/>.
        /// </summary>
        public SelfLabels()
        {
            Values = ReadOnlyCollection<SelfLabel>.Empty;
        }

        /// <summary>
        /// Creates a new instance of <see cref="SelfLabels"/>.
        /// </summary>
        /// <param name="values">The collection of labels applied to the record.</param>
        [JsonConstructor]
        public SelfLabels(ReadOnlyCollection<SelfLabel> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(values.Count, 10);

            Values = values;
        }

        /// <summary>
        /// The type discriminator for the class.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("$type")]
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Needs to an instance property for json serialization.")]
        public string Type => "com.atproto.label.defs#selfLabels";

        /// <summary>
        /// The collection of self labels applied to the record.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ReadOnlyCollection<SelfLabel> Values { get; internal set; }

        /// <summary>
        /// Returns a flag indicating whether the specified <paramref name="label"/> is present.
        /// </summary>
        /// <param name="label">The label to search for.</param>
        /// <returns></returns>
        public bool Contains(string label)
        {
            lock (_syncLock)
            {
                SelfLabel? value = (from existingLabel in Values where existingLabel.Value.Equals(label, StringComparison.Ordinal) select existingLabel).FirstOrDefault();
                return value is not null;
            }
        }

        /// <summary>
        /// Adds a <see cref="SelfLabel"/> with the specified <paramref name="name"/> if one does not already exist.
        /// </summary>
        /// <param name="name">The name of the <see cref="SelfLabel"/> to add.</param>
        public void AddLabel(string name)
        {
            lock (_syncLock)
            {
                if (!Contains(name))
                {
                    List<string> values = [.. from existingLabel in Values select existingLabel.Value, name];

                    List<SelfLabel> updatedLabels = [];
                    foreach (string value in values)
                    {
                        updatedLabels.Add(new SelfLabel(value));
                    }

                    Values = updatedLabels.AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Removes a <see cref="SelfLabel"/> with the specified <paramref name="name"/> if it exists.
        /// </summary>
        /// <param name="name">The name of the <see cref="SelfLabel"/> to remove.</param>
        public void RemoveLabel(string name)
        {
            lock (_syncLock)
            {
                if (Contains(name))
                {
                    List<string> values = [.. from existingLabel in Values select existingLabel.Value];
                    values.Remove(name);

                    List<SelfLabel> updatedLabels = [];
                    foreach (string value in values)
                    {
                        updatedLabels.Add(new SelfLabel(value));
                    }

                    Values = updatedLabels.AsReadOnly();
                }
            }
        }
    }
}
