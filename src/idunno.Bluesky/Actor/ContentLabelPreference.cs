// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// User preferences for a content label.
    /// </summary>
    public sealed record ContentLabelPreference : Preference
    {
        /// <summary>
        /// Creates a new instance of <see cref="ContentLabelPreference"/>.
        /// </summary>
        /// <param name="label">The label name.</param>
        /// <param name="labelerDid">The <see cref="Did"/> of the labeler. If null this preference applies globally.</param>
        /// <param name="visibility">How the label should be treated in a UI.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="label"/> is null or white space.</exception>
        [JsonConstructor]
        public ContentLabelPreference(string label, Did? labelerDid, LabelVisibility visibility)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(label);

            Label = label;
            LabelerDid = labelerDid;
            Visibility = visibility;
        }

        /// <summary>
        /// The label the preference applies to.
        /// </summary>
        /// <remarks>
        /// <para>Labels are not globally unique, i.e. the same label name can be used by multiple labelers.</para>
        /// </remarks>
        [JsonRequired]
        public string Label { get; init; }

        /// <summary>
        /// The labeler this preference applies.
        /// If null the preference applies globally.
        /// </summary>
        public Did? LabelerDid { get; init; }

        /// <summary>
        /// The desired visibility for the label.
        /// </summary>
        [JsonRequired]
        public LabelVisibility Visibility { get; init; }
    }

    /// <summary>
    /// Label effect values.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LabelVisibility
    {
        /// <summary>
        /// The label should be ignored and not shown to the user
        /// </summary>
        Ignore,

        /// <summary>
        /// The label should be shown to the user, but it should not affect the labelled content.
        /// </summary>
        Show,

        /// <summary>
        /// The user should be warned about the labelled content, and presented a way to display the content.
        /// </summary>

        Warn,
        /// <summary>
        /// The content should be hidden from the user.
        /// </summary>
        Hide
    }
}
