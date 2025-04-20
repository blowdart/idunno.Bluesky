// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Labels
{
    /// <summary>
    /// Encapsulates a label value and its expected interpretations and behaviors.
    /// </summary>
    public sealed record LabelValueDefinition
    {
        /// <summary>
        /// Gets the identifer of the label
        /// </summary>
        public required string Identifier { get; init; }

        /// <summary>
        /// Gets an indication of how a client visually convey this label.
        ///
        /// 'inform' means neutral and informational;
        /// 'alert' means negative and warning;
        /// 'none' means show nothing.
        ///
        /// Other values may occur.
        /// </summary>
        public required string Severity { get; init; }

        /// <summary>
        /// Gets an indication of how a client should hide entities who have the the label applied.
        ///
        /// 'inform' means neutral and informational;
        ///
        /// 'alert' means negative and warning;
        ///
        /// 'none' means show nothing.
        ///
        /// Other values may occur.
        /// </summary>
        public required string Blurs { get; init; }

        /// <summary>
        /// Gets the default setting for the label, if any.
        /// </summary>
        public string DefaultSetting { get; init; } = "warn";

        /// <summary>
        /// Gets a flag indicating if the user needs to have adult content enabled to configure the label.
        /// </summary>
        public bool AdultOnly { get; init; }

        /// <summary>
        /// A collection of strings which describe the label in the UI, localized into a specific language.
        /// </summary>
        public ICollection<LabelValueDefinitionStrings> Locales { get; init; } = [];
    }


    /// <summary>
    /// Strings which describe the label in the UI, localized into a specific language.
    /// </summary>
    public sealed record LabelValueDefinitionStrings
    {
        /// <summary>
        /// Gets the code of the language these strings are written in.
        /// </summary>
        public required string Lang { get; init; }

        /// <summary>
        /// Gets a short human-readable name for the label.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets a longer description of what the label means and why it might be applied.
        /// </summary>
        public required string Description { get; init; }
    }
}
