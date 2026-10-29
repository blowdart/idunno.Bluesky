// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Adapts bounded protocol reports without accepting caller identity or administrative wire envelopes.</summary>
internal static class ModerationWire
{
    internal const int MaximumReportBytes = 32768;

    /// <summary>Reads exact account/strong references and preserves submitted record CIDs without fetching targets.</summary>
    internal static LabelReportSubmission ParseReport(byte[] bytes)
    {
        try
        {
            return ParseReportCore(bytes);
        }
        catch (InvalidOperationException exception)
        {
            throw new JsonException("The report contains invalid Unicode.", exception);
        }
    }

    /// <summary>Decodes the bounded report while leaving Unicode failures to the parser boundary.</summary>
    private static LabelReportSubmission ParseReportCore(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        JsonElement root = document.RootElement;
        ValidateObject(root, ["reasonType", "reason", "subject", "modTool"]);
        RejectDuplicateProperties(root);
        string reasonType = RequiredString(root, "reasonType");
        if (!root.TryGetProperty("subject", out JsonElement subject))
        {
            throw new JsonException("A report subject is required.");
        }

        LabelReportSubject reference = ParseSubject(subject);
        string? reason = root.TryGetProperty("reason", out _) ? RequiredString(root, "reason") : null;
        string? toolName = null;
        JsonElement? metadata = null;
        if (root.TryGetProperty("modTool", out JsonElement tool))
        {
            ValidateObject(tool, ["name", "meta"]);
            toolName = RequiredString(tool, "name");
            metadata = tool.TryGetProperty("meta", out JsonElement meta) ? meta : null;
        }

        return new(
            subject: reference, reasonType: reasonType, reason: reason,
            modToolName: toolName, modToolMetadata: metadata);
    }

    /// <summary>Parses only supported explicit report union discriminators.</summary>
    internal static LabelReportSubject ParseSubject(JsonElement subject)
    {
        if (subject.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The subject must be an explicit reference.");
        }

        string type = RequiredString(subject, "$type");
        if (type == "com.atproto.admin.defs#repoRef")
        {
            ValidateObject(subject, ["$type", "did"]);
            return LabelReportSubject.Account(new(RequiredString(subject, "did")));
        }

        if (type == "com.atproto.repo.strongRef")
        {
            ValidateObject(subject, ["$type", "uri", "cid"]);
            return LabelReportSubject.Record(new(RequiredString(subject, "uri")), new(RequiredString(subject, "cid")));
        }

        throw new JsonException("Only account and record reports are supported.");
    }

    /// <summary>Projects the protocol response without exposing case IDs, policy state or private authentication evidence.</summary>
    internal static ReportOutput ReportOutput(LabelModerationReport report) => new(
        Id: report.Id,
        ReasonType: report.Submission.ReasonType,
        Subject: SubjectView(report.Submission.Subject),
        ReportedBy: report.ReportedBy.Value,
        CreatedAt: report.CreatedAt,
        Reason: report.Submission.Reason);

    /// <summary>Projects the standard protocol receipt without disclosing whether policy verification delayed review.</summary>
    internal static ReportOutput ReportOutput(
        LabelReportIntakeReceipt receipt,
        LabelReportSubmission submission,
        Did reporter) => new(
            Id: receipt.Id,
            ReasonType: submission.ReasonType,
            Subject: SubjectView(submission.Subject),
            ReportedBy: reporter.Value,
            CreatedAt: receipt.CreatedAt,
            Reason: submission.Reason);

    /// <summary>Projects a retained pending report using the standard protocol report fields.</summary>
    internal static ReportOutput ReportOutput(
        long id,
        LabelReportSubmission submission,
        Did reporter,
        DateTimeOffset createdAt) => new(
            Id: id,
            ReasonType: submission.ReasonType,
            Subject: SubjectView(submission.Subject),
            ReportedBy: reporter.Value,
            CreatedAt: createdAt,
            Reason: submission.Reason);

    /// <summary>Projects a reference without converting an account into its profile record.</summary>
    internal static ReportSubjectView SubjectView(LabelReportSubject subject) => subject.RecordUri is null
        ? new("com.atproto.admin.defs#repoRef", Did: subject.Repository.Value)
        : new("com.atproto.repo.strongRef", Uri: subject.RecordUri.ToString(), Cid: subject.Cid!.ToString());

    /// <summary>Rejects properties that could otherwise be mistaken for trusted identity or workflow instructions.</summary>
    private static void ValidateObject(JsonElement value, string[] properties)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            value.EnumerateObject().Any(property => !properties.Contains(property.Name, StringComparer.Ordinal)))
        {
            throw new JsonException("The report object contains unsupported fields.");
        }
    }

    /// <summary>Requires a string while retaining valid empty optional report context.</summary>
    private static string RequiredString(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()! : throw new JsonException("A required report string is absent or invalid.");

    /// <summary>Rejects ambiguous JSON throughout the report, including arbitrary tool metadata.</summary>
    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException("Duplicate report properties are not accepted.");
                }

                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item);
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            _ = element.GetString();
        }
    }
}

/// <summary>Defines the actual standard createReport output, not a private success envelope.</summary>
internal sealed record ReportOutput(long Id, string ReasonType, ReportSubjectView Subject, string ReportedBy, DateTimeOffset CreatedAt, string? Reason);

/// <summary>Defines the supported explicit report subject union.</summary>
internal sealed record ReportSubjectView(
    [property: JsonPropertyName("$type")] string Type, string? Did = null, string? Uri = null, string? Cid = null);

/// <summary>Defines a standard XRPC error without private diagnostic details.</summary>
internal sealed record ModerationXrpcError(string Error, string Message);

/// <summary>Supplies trimming-safe report and moderation transport metadata.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ReportOutput))]
[JsonSerializable(typeof(ModerationXrpcError))]
internal sealed partial class LabelerModerationJsonContext : JsonSerializerContext;
