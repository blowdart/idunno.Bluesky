// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Labels;

/// <summary>Validates declared label values and custom definitions at an explicit publication boundary.</summary>
/// <remarks><para>This validator does not restrict tolerant deserialization of remote label definitions.</para></remarks>
public static class LabelPolicyValidator
{
    /// <summary>Validates unique label values, custom identifiers, behavior and localized definitions.</summary>
    /// <param name="labelValues">The declared global and custom label values.</param>
    /// <param name="definitions">The custom definitions belonging to these values.</param>
    /// <exception cref="ArgumentNullException">A required collection, definition or localized field is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Values, identifiers, behavior or localized text violate publication constraints.</exception>
    public static void Validate(IEnumerable<string> labelValues, IEnumerable<LabelValueDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(labelValues);
        ArgumentNullException.ThrowIfNull(definitions);
        HashSet<string> values = new(StringComparer.Ordinal);
        foreach (string value in labelValues)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (value.GetUtf8Length() > 128 || !values.Add(value))
            {
                throw new ArgumentException("Label values must be unique and at most 128 UTF-8 bytes.", nameof(labelValues));
            }
        }

        HashSet<string> identifiers = new(StringComparer.Ordinal);
        foreach (LabelValueDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            string identifier = definition.Identifier;
            ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
            if (identifier.Length > 100 || identifier.Any(character => character is not (>= 'a' and <= 'z') and not '-') ||
                !identifiers.Add(identifier) || !values.Contains(identifier))
            {
                throw new ArgumentException(
                    "Custom identifiers must use lowercase ASCII letters or hyphens, be at most 100 characters, be unique, and appear in labelValues. Reserved ! labels cannot be redefined.",
                    nameof(definitions));
            }

            if (definition.Severity is not ("inform" or "alert" or "none") ||
                definition.Blurs is not ("content" or "media" or "none") ||
                definition.DefaultSetting is not ("ignore" or "warn" or "hide"))
            {
                throw new ArgumentException("The label definition has an unsupported severity, blur behavior, or default setting.", nameof(definitions));
            }

            ArgumentNullException.ThrowIfNull(definition.Locales);
            if (definition.Locales.Count == 0)
            {
                throw new ArgumentException("Custom labels must have at least one localized definition.", nameof(definitions));
            }

            HashSet<string> languages = new(StringComparer.OrdinalIgnoreCase);
            foreach (LabelValueDefinitionStrings locale in definition.Locales)
            {
                ArgumentNullException.ThrowIfNull(locale);
                ArgumentException.ThrowIfNullOrWhiteSpace(locale.Lang);
                ArgumentNullException.ThrowIfNull(locale.Name);
                ArgumentNullException.ThrowIfNull(locale.Description);
                if (!languages.Add(locale.Lang) ||
                    locale.Name.GetUtf8Length() > 640 || locale.Name.GetGraphemeLength() > 64 ||
                    locale.Description.GetUtf8Length() > 100000 || locale.Description.GetGraphemeLength() > 10000)
                {
                    throw new ArgumentException("Localized definitions must have unique languages and text within the lexicon length limits.", nameof(definitions));
                }
            }
        }
    }
}
