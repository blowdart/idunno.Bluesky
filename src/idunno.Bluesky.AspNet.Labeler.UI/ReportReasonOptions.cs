// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Names standard submission reasons without conflating them with moderator decision explanations.</summary>
internal static class ReportReasonOptions
{
    /// <summary>Gets the standard Ozone and legacy AT Protocol reasons grouped for policy selection.</summary>
    internal static readonly (string Group, string Id, string Title)[] All =
    [
        ("General", "tools.ozone.report.defs#reasonOther", "Other"),
        ("General", "tools.ozone.report.defs#reasonAppeal", "Appeal a moderation action"),
        ("Misleading content", "tools.ozone.report.defs#reasonMisleadingSpam", "Spam"),
        ("Misleading content", "tools.ozone.report.defs#reasonMisleadingScam", "Scams"),
        ("Misleading content", "tools.ozone.report.defs#reasonMisleadingBot", "Fake accounts or bots"),
        ("Misleading content", "tools.ozone.report.defs#reasonMisleadingImpersonation", "Impersonation"),
        ("Misleading content", "tools.ozone.report.defs#reasonMisleadingElections", "False election information"),
        ("Misleading content", "tools.ozone.report.defs#reasonMisleadingOther", "Other misleading content"),
        ("Harassment", "tools.ozone.report.defs#reasonHarassmentTroll", "Trolling"),
        ("Harassment", "tools.ozone.report.defs#reasonHarassmentTargeted", "Targeted harassment"),
        ("Harassment", "tools.ozone.report.defs#reasonHarassmentHateSpeech", "Hate speech"),
        ("Harassment", "tools.ozone.report.defs#reasonHarassmentDoxxing", "Doxxing"),
        ("Harassment", "tools.ozone.report.defs#reasonHarassmentOther", "Other harassing or hateful content"),
        ("Violence", "tools.ozone.report.defs#reasonViolenceAnimal", "Animal welfare violations"),
        ("Violence", "tools.ozone.report.defs#reasonViolenceThreats", "Threats or incitement"),
        ("Violence", "tools.ozone.report.defs#reasonViolenceGraphicContent", "Graphic violence"),
        ("Violence", "tools.ozone.report.defs#reasonViolenceGlorification", "Glorification of violence"),
        ("Violence", "tools.ozone.report.defs#reasonViolenceExtremistContent", "Extremist content"),
        ("Violence", "tools.ozone.report.defs#reasonViolenceTrafficking", "Human trafficking"),
        ("Violence", "tools.ozone.report.defs#reasonViolenceOther", "Other violent content"),
        ("Sexual content", "tools.ozone.report.defs#reasonSexualAbuseContent", "Adult sexual abuse content"),
        ("Sexual content", "tools.ozone.report.defs#reasonSexualNCII", "Non-consensual intimate imagery"),
        ("Sexual content", "tools.ozone.report.defs#reasonSexualDeepfake", "Deepfake adult content"),
        ("Sexual content", "tools.ozone.report.defs#reasonSexualAnimal", "Animal sexual abuse"),
        ("Sexual content", "tools.ozone.report.defs#reasonSexualUnlabeled", "Unlabeled adult content"),
        ("Sexual content", "tools.ozone.report.defs#reasonSexualOther", "Other sexual violence content"),
        ("Child safety", "tools.ozone.report.defs#reasonChildSafetyCSAM", "Child sexual abuse material"),
        ("Child safety", "tools.ozone.report.defs#reasonChildSafetyGroom", "Grooming or predatory behavior"),
        ("Child safety", "tools.ozone.report.defs#reasonChildSafetyPrivacy", "Privacy violations involving minors"),
        ("Child safety", "tools.ozone.report.defs#reasonChildSafetyHarassment", "Harassment or bullying of minors"),
        ("Child safety", "tools.ozone.report.defs#reasonChildSafetyOther", "Other child safety concerns"),
        ("Rules", "tools.ozone.report.defs#reasonRuleSiteSecurity", "Hacking or system attacks"),
        ("Rules", "tools.ozone.report.defs#reasonRuleProhibitedSales", "Prohibited sales"),
        ("Rules", "tools.ozone.report.defs#reasonRuleBanEvasion", "Ban evasion"),
        ("Rules", "tools.ozone.report.defs#reasonRuleOther", "Other rule violations"),
        ("Self-harm", "tools.ozone.report.defs#reasonSelfHarmContent", "Self-harm content"),
        ("Self-harm", "tools.ozone.report.defs#reasonSelfHarmED", "Eating disorders"),
        ("Self-harm", "tools.ozone.report.defs#reasonSelfHarmStunts", "Dangerous challenges or activities"),
        ("Self-harm", "tools.ozone.report.defs#reasonSelfHarmSubstances", "Dangerous substances or drug abuse"),
        ("Self-harm", "tools.ozone.report.defs#reasonSelfHarmOther", "Other dangerous content"),
        ("Legacy AT Protocol reasons", "com.atproto.moderation.defs#reasonOther", "Other (legacy)"),
        ("Legacy AT Protocol reasons", "com.atproto.moderation.defs#reasonAppeal", "Appeal (legacy)"),
        ("Legacy AT Protocol reasons", "com.atproto.moderation.defs#reasonSpam", "Spam (legacy)"),
        ("Legacy AT Protocol reasons", "com.atproto.moderation.defs#reasonViolation", "Rule violations (legacy)"),
        ("Legacy AT Protocol reasons", "com.atproto.moderation.defs#reasonMisleading", "Misleading content (legacy)"),
        ("Legacy AT Protocol reasons", "com.atproto.moderation.defs#reasonSexual", "Sexual content (legacy)"),
        ("Legacy AT Protocol reasons", "com.atproto.moderation.defs#reasonRude", "Harassment (legacy)")
    ];
}
