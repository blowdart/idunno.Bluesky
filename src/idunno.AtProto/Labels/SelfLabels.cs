// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Labels
{
    /// <summary>
    /// Metadata tags on an atproto record, published by the author within the record.
    /// </summary>
    [JsonPolymorphic]
    [JsonDerivedType(typeof(SelfLabels), typeDiscriminator: "com.atproto.label.defs#selfLabels")]
    public class SelfLabels
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
#if NET9_0_OR_GREATER
        private readonly Lock _syncLock = new();
#else
        private readonly object _syncLock = new();
#endif

        List<SelfLabel> _values;

        /// <summary>
        /// Creates a new instance of <see cref="SelfLabels"/>.
        /// </summary>
        public SelfLabels()
        {
            _values = [];
        }

        /// <summary>
        /// Creates a new instance of <see cref="SelfLabels"/>.
        /// </summary>
        /// <param name="values">The collection of labels applied to the record.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="values"/> contains more than 10 items.</exception>
        [JsonConstructor]
        public SelfLabels(IReadOnlyList<SelfLabel> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(values.Count, 10);

            _values = [.. values];
        }

        /// <summary>
        /// The collection of self labels applied to the record.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when setting to <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when setting to a collection with more than 10 items.</exception>
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyList<SelfLabel> Values
        {
            get => _values.AsReadOnly();

            set
            {
                ArgumentNullException.ThrowIfNull(value);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Count, 10);
                lock (_syncLock)
                {
                    _values = [.. value];
                }
            }
        }

        /// <summary>
        /// Returns a flag indicating whether the specified <paramref name="label"/> is present.
        /// </summary>
        /// <param name="label">The label to search for.</param>
        /// <returns>A flag indicating whether the label is present.</returns>
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
                    List<string> values = [.. from existingLabel in _values select existingLabel.Value, name];

                    List<SelfLabel> updatedLabels = [];
                    foreach (string value in values)
                    {
                        updatedLabels.Add(new SelfLabel(value));
                    }

                    _values = updatedLabels;
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

                    _values = updatedLabels;
                }
            }
        }
    }
}
