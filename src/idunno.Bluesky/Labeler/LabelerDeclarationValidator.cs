// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;

namespace idunno.Bluesky.Labeler;

/// <summary>
/// Validates declarations at the publishing boundary without restricting deserialization of server data.
/// </summary>
internal static class LabelerDeclarationValidator
{
    internal static void Validate(Service declaration)
    {
        const string ParameterName = nameof(declaration);
        LabelerPolicies policies = declaration.Policies;
        ArgumentNullException.ThrowIfNull(policies);

        HashSet<string> values = new(StringComparer.Ordinal);
        foreach (string value in policies.LabelValues)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (value.GetUtf8Length() > 128 || !values.Add(value))
            {
                throw new ArgumentException("Label values must be unique and at most 128 UTF-8 bytes.", ParameterName);
            }
        }

        HashSet<string> definitions = new(StringComparer.Ordinal);
        foreach (LabelValueDefinition definition in policies.LabelValueDefinitions)
        {
            string identifier = definition.Identifier;
            ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
            if (identifier.Length > 100 || identifier.Any(c => c is not (>= 'a' and <= 'z') and not '-') ||
                !definitions.Add(identifier) || !values.Contains(identifier))
            {
                throw new ArgumentException(
                    "Custom identifiers must use lowercase ASCII letters or hyphens, be at most 100 characters, be unique, and appear in labelValues. Reserved ! labels cannot be redefined.",
                    ParameterName);
            }

            if (definition.Severity is not ("inform" or "alert" or "none") ||
                definition.Blurs is not ("content" or "media" or "none") ||
                definition.DefaultSetting is not ("ignore" or "warn" or "hide"))
            {
                throw new ArgumentException("The label definition has an unsupported severity, blur behavior, or default setting.", ParameterName);
            }

            ArgumentNullException.ThrowIfNull(definition.Locales);
            if (definition.Locales.Count == 0)
            {
                throw new ArgumentException("Custom labels must have at least one localized definition.", ParameterName);
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
                    throw new ArgumentException("Localized definitions must have unique languages and text within the lexicon length limits.", ParameterName);
                }
            }
        }

        if (declaration.ReasonTypes is not null)
        {
            foreach (string reason in declaration.ReasonTypes)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            }
        }

        if (declaration.SubjectTypes is not null)
        {
            foreach (string subject in declaration.SubjectTypes)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(subject);
            }
        }

        if (declaration.SubjectCollections is not null)
        {
            foreach (Nsid collection in declaration.SubjectCollections)
            {
                ArgumentNullException.ThrowIfNull(collection);
            }
        }
    }
}
