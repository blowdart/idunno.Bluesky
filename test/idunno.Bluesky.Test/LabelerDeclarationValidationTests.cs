// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class LabelerDeclarationValidationTests
{
    private static LabelValueDefinitionStrings CreateLocale() => new() { Lang = "en", Name = "Spiders", Description = "Contains spiders." };

    private static LabelValueDefinition CreateDefinition() => new()
    {
        Identifier = "spider-warning",
        Severity = "inform",
        Blurs = "media",
        Locales = [CreateLocale()]
    };

    private static Service CreateDeclaration(LabelValueDefinition definition) => new()
    {
        CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture),
        Policies = new LabelerPolicies { LabelValues = [definition.Identifier, "!warn"], LabelValueDefinitions = [definition] }
    };

    [Theory]
    [InlineData("content", "alert")]
    [InlineData("content", "inform")]
    [InlineData("content", "none")]
    [InlineData("media", "alert")]
    [InlineData("media", "inform")]
    [InlineData("media", "none")]
    [InlineData("none", "alert")]
    [InlineData("none", "inform")]
    [InlineData("none", "none")]
    public void PublishingAcceptsAllNinePresentationCombinations(string blurs, string severity)
    {
        foreach (string setting in new[] { "hide", "warn", "ignore" })
        {
            Service declaration = CreateDeclaration(CreateDefinition() with { Blurs = blurs, Severity = severity, DefaultSetting = setting });
            LabelerDeclarationValidator.Validate(declaration);
        }
    }

    [Theory]
    [InlineData("severity")]
    [InlineData("blurs")]
    [InlineData("defaultSetting")]
    [InlineData("locales")]
    [InlineData("duplicateLocale")]
    [InlineData("language")]
    [InlineData("nameBytes")]
    [InlineData("nameGraphemes")]
    [InlineData("descriptionBytes")]
    [InlineData("descriptionGraphemes")]
    [InlineData("identifierLength")]
    public void PublishingRejectsInvalidDefinitions(string invalidField)
    {
        LabelValueDefinition definition = CreateDefinition();
        LabelValueDefinitionStrings locale = CreateLocale();
        definition = invalidField switch
        {
            "severity" => definition with { Severity = "unsupported" },
            "blurs" => definition with { Blurs = "unsupported" },
            "defaultSetting" => definition with { DefaultSetting = "unsupported" },
            "locales" => definition with { Locales = [] },
            "duplicateLocale" => definition with { Locales = [locale, locale with { Lang = "EN" }] },
            "language" => definition with { Locales = [locale with { Lang = " " }] },
            "nameBytes" => definition with { Locales = [locale with { Name = string.Concat(Enumerable.Repeat("a" + new string('\u0301', 320), 2)) }] },
            "nameGraphemes" => definition with { Locales = [locale with { Name = new string('a', 65) }] },
            "descriptionBytes" => definition with { Locales = [locale with { Description = "a" + new string('\u0301', 50000) }] },
            "descriptionGraphemes" => definition with { Locales = [locale with { Description = new string('a', 10001) }] },
            "identifierLength" => definition with { Identifier = new string('a', 101) },
            _ => throw new InvalidOperationException()
        };

        Assert.ThrowsAny<ArgumentException>(() => LabelerDeclarationValidator.Validate(CreateDeclaration(definition)));
    }

    [Fact]
    public void PublishingAcceptsLexiconTextLengthBoundariesAndMultipleLocales()
    {
        LabelValueDefinitionStrings locale = CreateLocale() with
        {
            Name = new string('a', 64),
            Description = new string('a', 10000)
        };
        LabelerDeclarationValidator.Validate(CreateDeclaration(CreateDefinition() with
        {
            Identifier = new string('a', 100),
            Locales = [locale, locale with { Lang = "fr" }]
        }));
    }

    [Fact]
    public void PublishingAcceptsUtf8ByteBoundariesIndependentlyOfGraphemeLimits()
    {
        LabelValueDefinitionStrings locale = CreateLocale() with
        {
            Name = "aa" + new string('\u0301', 319),
            Description = "aa" + new string('\u0301', 49999)
        };
        LabelerDeclarationValidator.Validate(CreateDeclaration(CreateDefinition() with { Locales = [locale] }));
    }

    [Theory]
    [InlineData("duplicateValue")]
    [InlineData("longValue")]
    [InlineData("duplicateDefinition")]
    [InlineData("missingValue")]
    [InlineData("emptyReason")]
    [InlineData("emptySubject")]
    public void PublishingRejectsInconsistentPoliciesAndEmptyScopes(string invalidField)
    {
        LabelValueDefinition definition = CreateDefinition();
        Service declaration = CreateDeclaration(definition);
        declaration = invalidField switch
        {
            "duplicateValue" => declaration with { Policies = declaration.Policies with { LabelValues = ["spam", "spam"] } },
            "longValue" => declaration with { Policies = declaration.Policies with { LabelValues = [new string('\u00e9', 65)] } },
            "duplicateDefinition" => declaration with { Policies = declaration.Policies with { LabelValueDefinitions = [definition, definition] } },
            "missingValue" => declaration with { Policies = declaration.Policies with { LabelValues = ["spam"] } },
            "emptyReason" => declaration with { ReasonTypes = [""] },
            "emptySubject" => declaration with { SubjectTypes = [""] },
            _ => throw new InvalidOperationException()
        };

        Assert.ThrowsAny<ArgumentException>(() => LabelerDeclarationValidator.Validate(declaration));
    }

    [Theory]
    [InlineData("com.atproto.moderation.defs#reasonSpam")]
    [InlineData("tools.ozone.report.defs#reasonOther")]
    [InlineData("example.future.report#reasonNew")]
    public void PublishingDoesNotCloseReportReasonOrSubjectTypeVocabularies(string reason)
    {
        Service declaration = CreateDeclaration(CreateDefinition()) with { ReasonTypes = [reason], SubjectTypes = ["future-subject"] };
        LabelerDeclarationValidator.Validate(declaration);
    }
}
