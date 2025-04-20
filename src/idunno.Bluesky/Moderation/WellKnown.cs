// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Moderation
{
    /// <summary>
    /// Provides a list of known moderation report reason types.
    /// </summary>
    public static class WellKnown
    {
        // Taken from https://github.com/bluesky-social/social-app/blob/main/src/lib/moderation/useReportOptions.ts

        private static readonly ReportOption s_otherReportOption = new()
        {
            Type = "com.atproto.moderation.defs#reasonOther",
            Title = "Other",
            Description = "Other: reports not falling under another report category"
        };

        private static readonly IReadOnlyCollection<ReportOption> s_commonReportOptions = [
            new(){ Type = "com.atproto.moderation.defs#reasonRude", Title = "Anti-social behavior", Description = "Rude, harassing, explicit, or otherwise unwelcoming behavior" },
            new(){ Type = "com.atproto.moderation.defs#reasonViolation", Title = "Illegal and Urgent", Description = "Direct violation of server rules, laws, terms of service" },
            s_otherReportOption];

        static WellKnown()
        {
            ReportType = new Dictionary<ReportType, string>()
            {
                { Moderation.ReportType.Other, "com.atproto.moderation.defs#reasonOther" },
                { Moderation.ReportType.Rude, "com.atproto.moderation.defs#reasonRude" },
                { Moderation.ReportType.Violation, "com.atproto.moderation.defs#reasonViolation" },
                { Moderation.ReportType.Misleading, "com.atproto.moderation.defs#reasonMisleading" },
                { Moderation.ReportType.Spam, "com.atproto.moderation.defs#reasonSpam" },
                { Moderation.ReportType.Sexual, "com.atproto.moderation.defs#reasonSexual" }
            }.AsReadOnly();

            Dictionary<string, List<ReportOption>> reportOptionsByTarget = [];

            reportOptionsByTarget.Add(
                "account",
                [
                    new(){ Type = "com.atproto.moderation.defs#reasonMisleading", Title = "Misleading Account", Description = "Impersonation or false claims about identity or affiliation" },
                    new(){ Type = "com.atproto.moderation.defs#reasonSpam", Title = "Frequently Posts Unwanted Content", Description = "Spam; excessive mentions or replies" },
                    new(){ Type = "com.atproto.moderation.defs#reasonViolation", Title = "Name or Description Violates Community Standards", Description = "Terms used violate community standards" },
                    s_otherReportOption
                ]);

            reportOptionsByTarget.Add(
                "post",
                [
                    new(){ Type = "com.atproto.moderation.defs#reasonMisleading", Title = "Misleading Post", Description = "Impersonation, misinformation, or false claim" },
                    new(){ Type = "com.atproto.moderation.defs#reasonSpam", Title = "Spam", Description = "Excessive mentions or replies" },
                    new(){ Type = "com.atproto.moderation.defs#reasonSexual", Title = "Unwanted Sexual Content", Description = "Nudity or adult content not labeled as such" },
                ]);
            reportOptionsByTarget["post"].AddRange(s_commonReportOptions);

            reportOptionsByTarget.Add(
                "convoMessage",
                [
                    new() { Type = "com.atproto.moderation.defs#reasonSpam", Title = "Spam", Description = "Excessive mentions or replies" },
                    new() { Type = "com.atproto.moderation.defs#reasonSexual", Title = "Unwanted Sexual Content", Description = "Nudity or adult content not labeled as such" }
                ]);
            reportOptionsByTarget["convoMessage"].AddRange(s_commonReportOptions);

            reportOptionsByTarget.Add(
                "list",
                [
                    new(){ Type = "com.atproto.moderation.defs#reasonViolation", Title = "Name or Description Violates Community Standards", Description = "Terms used violate community standards" },
                ]);
            reportOptionsByTarget["list"].AddRange(s_commonReportOptions);

            reportOptionsByTarget.Add(
                "starterpack",
                [
                    new(){ Type = "com.atproto.moderation.defs#reasonViolation", Title = "Name or Description Violates Community Standards", Description = "Terms used violate community standards" },
                ]);
            reportOptionsByTarget["starterpack"].AddRange(s_commonReportOptions);

            reportOptionsByTarget.Add(
                "feedgen",
                [
                    new(){ Type = "com.atproto.moderation.defs#reasonViolation", Title = "Name or Description Violates Community Standards", Description = "Terms used violate community standards" },
                ]);
            reportOptionsByTarget["feedgen"].AddRange(s_commonReportOptions);

            reportOptionsByTarget.Add("other", [.. s_commonReportOptions]);

            ReportTargets = reportOptionsByTarget.Keys;

            ReportOptions = reportOptionsByTarget.AsReadOnly();
        }

        /// <summary>
        /// Gets a dictionary of report options, where the key is the report target.
        /// </summary>
        public static IReadOnlyDictionary<string, List<ReportOption>> ReportOptions { get; }

        /// <summary>
        /// Gets a read-only collection of well known report targets.
        /// </summary>
        public static IReadOnlyCollection<string> ReportTargets { get; }

        /// <summary>
        /// Gets a dictionary mapping report reason to report type as string.
        /// </summary>
        public static IReadOnlyDictionary<ReportType, string> ReportType { get; }
    }

    /// <summary>
    /// Encapsulates the information needed to display moderation reporting options.
    /// </summary>
    public sealed record ReportOption
    {
        /// <summary>
        /// Gets the type to use when submitting a report
        /// </summary>
        public required string Type { get; init; }

        /// <summary>
        /// Gets the report reason title.
        /// </summary>
        public required string Title { get; init; }

        /// <summary>
        /// Gets the description of the report reason.
        /// </summary>
        public required string Description { get; init; }
    }

    /// <summary>
    /// Known report reasons
    /// </summary>
    public enum ReportType
    {
        /// <summary>
        /// Any other reason.
        /// </summary>
        Other = 0,

        /// <summary>
        /// Anti-Social Behavior.
        /// </summary>
        Rude,

        /// <summary>
        /// Illegal and Urgent
        /// </summary>
        Violation,

        /// <summary>
        /// Impersonation, misinformation or false claims.
        /// </summary>
        Misleading,

        /// <summary>
        /// `Excessive mentions, replies or unwanted messages.
        /// </summary>
        Spam,

        /// <summary>
        /// Unwanted sexual content.
        /// </summary>
        Sexual
    }
}
