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

    /// <summary>Parses the Ozone label appeal shape, including clients that encode record URIs in repoRef.did.</summary>
    internal static OzoneAppealRequest ParseOzoneAppeal(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        JsonElement root = document.RootElement;
        ValidateObject(root, ["action", "subject", "reason", "modTool"]);
        RejectDuplicateProperties(root);
        if (!root.TryGetProperty("subject", out JsonElement subject))
        {
            throw new JsonException("An appeal subject is required.");
        }
        if (subject.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("An appeal subject must be an explicit reference.");
        }

        Did repository;
        AtUri? recordUri = null;
        Cid? cid = null;
        string subjectType = RequiredString(subject, "$type");
        if (subjectType == "com.atproto.admin.defs#repoRef")
        {
            ValidateObject(subject, ["$type", "did"]);
            string didOrUri = RequiredString(subject, "did");
            if (didOrUri.StartsWith("at://", StringComparison.Ordinal))
            {
                recordUri = new AtUri(didOrUri);
                repository = recordUri.Repo as Did ?? throw new JsonException("A record appeal requires a DID repository.");
                if (recordUri.Collection is null || recordUri.RecordKey is null)
                {
                    throw new JsonException("A record appeal requires a complete AT URI.");
                }
            }
            else
            {
                repository = new Did(didOrUri);
            }
        }
        else if (subjectType == "com.atproto.repo.strongRef")
        {
            ValidateObject(subject, ["$type", "uri", "cid"]);
            recordUri = new AtUri(RequiredString(subject, "uri"));
            repository = recordUri.Repo as Did ?? throw new JsonException("A record appeal requires a DID repository.");
            if (recordUri.Collection is null || recordUri.RecordKey is null)
            {
                throw new JsonException("A record appeal requires a complete AT URI.");
            }

            cid = new Cid(RequiredString(subject, "cid"));
        }
        else
        {
            throw new JsonException("Only account and record appeal subjects are supported.");
        }

        if (!root.TryGetProperty("action", out JsonElement action) ||
            action.ValueKind != JsonValueKind.Object)
        {
            throw new NotSupportedException("A labelRef action is required to appeal a label.");
        }

        ValidateObject(action, ["$type", "val"]);
        if (RequiredString(action, "$type") != "tools.ozone.inbox.appealActionedSubject#labelRef")
        {
            throw new NotSupportedException("Only label appeals are supported.");
        }

        string labelValue = RequiredString(action, "val");
        string? reason = root.TryGetProperty("reason", out _) ? RequiredString(root, "reason") : null;
        string? toolName = null;
        JsonElement? toolMetadata = null;
        if (root.TryGetProperty("modTool", out JsonElement tool))
        {
            ValidateObject(tool, ["name", "meta"]);
            toolName = RequiredString(tool, "name");
            toolMetadata = tool.TryGetProperty("meta", out JsonElement meta) ? meta : null;
        }

        return new(
            Repository: repository,
            RecordUri: recordUri,
            Cid: cid,
            LabelValue: labelValue,
            Reason: reason,
            ModToolName: toolName,
            ModToolMetadata: toolMetadata);
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
    internal static LabelReportSubject ParseSubject(JsonElement subject, bool administrationView = false)
    {
        if (subject.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The subject must be an explicit reference.");
        }

        string type = RequiredString(subject, "$type");
        if (type == "com.atproto.admin.defs#repoRef")
        {
            ValidateObject(subject, ["$type", "did"]);
            string identity = RequiredString(subject, "did");
            return identity.StartsWith("at://", StringComparison.Ordinal)
                ? LabelReportSubject.Record(new(identity))
                : LabelReportSubject.Account(new(identity));
        }

        if (administrationView && type == "record")
        {
            ValidateObject(subject, ["$type", "uri"]);
            return LabelReportSubject.Record(new(RequiredString(subject, "uri")));
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
            Subject: ProtocolSubjectView(submission.Subject),
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

    /// <summary>Projects an administrative subject, using an internal record discriminator when no strong reference was submitted.</summary>
    internal static ReportSubjectView SubjectView(LabelReportSubject subject)
    {
        if (subject.RecordUri is null)
        {
            return new("com.atproto.admin.defs#repoRef", Did: subject.Repository.Value);
        }

        string type = subject.Cid is null ? "record" : "com.atproto.repo.strongRef";
        return new(type, Uri: subject.RecordUri.ToString(), Cid: subject.Cid?.ToString());
    }

    /// <summary>Returns the submitted protocol reference without inventing a strong reference for a URI-only subject.</summary>
    internal static ReportSubjectView ProtocolSubjectView(LabelReportSubject subject) => subject.RecordUri is not null && subject.Cid is null
        ? new("com.atproto.admin.defs#repoRef", Did: subject.Subject)
        : SubjectView(subject);

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
            HashSet<string> names = [with(StringComparer.Ordinal)];
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

/// <summary>Retains the exact label and subject supplied by an Ozone appeal client.</summary>
internal sealed record OzoneAppealRequest(
    Did Repository,
    AtUri? RecordUri,
    Cid? Cid,
    string LabelValue,
    string? Reason,
    string? ModToolName,
    JsonElement? ModToolMetadata)
{
    /// <summary>Builds a durable appeal submission using only the subject identity supplied by the client.</summary>
    internal LabelReportSubmission ToSubmission() => new(
        subject: RecordUri is null
            ? LabelReportSubject.Account(Repository)
            : LabelReportSubject.Record(RecordUri, Cid),
        reasonType: "tools.ozone.report.defs#reasonAppeal",
        reason: Reason,
        modToolName: ModToolName,
        modToolMetadata: ModToolMetadata,
        appealLabelValue: LabelValue);
}

/// <summary>Defines the actual standard createReport output, not a private success envelope.</summary>
internal sealed record ReportOutput(long Id, string ReasonType, ReportSubjectView Subject, string ReportedBy, DateTimeOffset CreatedAt, string? Reason);

/// <summary>Defines the supported explicit report subject union.</summary>
internal sealed record ReportSubjectView(
    [property: JsonPropertyName("$type")] string Type, string? Did = null, string? Uri = null, string? Cid = null);

/// <summary>Defines the Ozone appeal response for the actioned subject.</summary>
internal sealed record AppealActionedSubjectOutput(
    string Src,
    ReportSubjectView Subject,
    AppealEnforcementView Enforcement,
    bool IsRead,
    AppealStateView Appeal,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Describes the active label enforcement without implying network or account suspension.</summary>
internal sealed record AppealEnforcementView(string State, string Scope, string[] Labels);

/// <summary>Describes the accepted appeal state.</summary>
internal sealed record AppealStateView(string State, DateTimeOffset AppealedAt);

/// <summary>Defines a standard XRPC error without private diagnostic details.</summary>
internal sealed record ModerationXrpcError(string Error, string Message);

/// <summary>Supplies trimming-safe report and moderation transport metadata.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ReportOutput))]
[JsonSerializable(typeof(AppealActionedSubjectOutput))]
[JsonSerializable(typeof(ModerationXrpcError))]
internal sealed partial class LabelerModerationJsonContext : JsonSerializerContext;
