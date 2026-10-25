// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>Encodes durable service receipts without reflection or reconstruction of signed fields.</summary>
/// <remarks>
/// <para>This is a versioned provider-owned binary envelope, not a public wire format. Signed JSON is
/// embedded verbatim. Keeping the whole original outcome makes recovery independent of later labels,
/// policy changes and publication advances. All reads are bounded by the actual stored buffer.</para>
/// </remarks>
internal static class DecisionReceiptEncoding
{
    private static readonly UTF8Encoding s_utf8 = new(false, true);

    /// <summary>Captures the original outcome, including exact signed bytes and historical state boundaries.</summary>
    internal static byte[] Encode(LabelOperationReceipt receipt)
    {
        using MemoryStream buffer = new();
        using BinaryWriter writer = new(buffer, s_utf8, leaveOpen: true);
        writer.Write(1);
        LabelOperationRequest request = receipt.Request;
        writer.Write(request.OperationId);
        writer.Write((int)request.Kind);
        WriteTarget(writer, request.Target);
        writer.Write(request.Value);
        WriteNullable(writer, request.ExpiresAt?.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        writer.Write(request.Context.Actor.Value);
        writer.Write(request.Context.Reason);
        WriteNullable(writer, request.Context.CorrelationId);
        WriteTarget(writer, receipt.ResolvedTarget);
        writer.Write(receipt.Changed);
        writer.Write(receipt.Label is not null);
        if (receipt.Label is { } label)
        {
            writer.Write(label.Sequence);
            byte[] json = label.SignedLabel.GetJsonBytes();
            writer.Write(json.Length);
            writer.Write(json);
        }

        LabelStoreState state = receipt.StateAtCommit;
        writer.Write(state.StreamId.ToString("N"));
        writer.Write(state.Issuer);
        writer.Write(state.Revision);
        writer.Write(state.IssuedThrough);
        writer.Write(state.PublishedThrough);
        writer.Write(state.ReplayFloor);
        writer.Write((int)state.Phase);
        writer.Write(state.RetirementUpperBound);
        writer.Write(state.RetirementThrough);
        WriteNullable(writer, state.GraceDeadline?.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        writer.Write(receipt.Audit.EvaluatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        writer.Write(receipt.Audit.AuthorizationVersion);
        WriteNullable(writer, receipt.Audit.PolicyVersion);
        WriteNullable(writer, receipt.Audit.SigningKeyMultibase);
        writer.Flush();
        return buffer.ToArray();
    }

    /// <summary>Restores a known receipt version, rejecting malformed or trailing data rather than losing fields.</summary>
    internal static LabelOperationReceipt Decode(byte[] bytes)
    {
        using MemoryStream buffer = new(bytes, writable: false);
        using BinaryReader reader = new(buffer, s_utf8);
        try
        {
            if (reader.ReadInt32() != 1)
            {
                throw new InvalidDataException("The service receipt format is unsupported.");
            }

            string id = reader.ReadString();
            LabelOperationKind kind = (LabelOperationKind)reader.ReadInt32();
            LabelTarget target = ReadTarget(reader);
            string value = reader.ReadString();
            DateTimeOffset? expiry = Timestamp(ReadNullable(reader));
            LabelOperationContext context = new(new Did(reader.ReadString()), reader.ReadString(), ReadNullable(reader));
            LabelOperationRequest request = kind switch
            {
                LabelOperationKind.Issue => LabelOperationRequest.Issue(
                    operationId: id,
                    target: target,
                    value: value,
                    context: context,
                    expiresAt: expiry),
                LabelOperationKind.Negate when expiry is null => LabelOperationRequest.Negate(
                    operationId: id,
                    target: target,
                    value: value,
                    context: context),
                _ => throw new InvalidDataException("The service receipt operation is invalid.")
            };
            LabelTarget resolved = ReadTarget(reader);
            bool changed = reader.ReadBoolean();
            StoredLabel? label = null;
            if (reader.ReadBoolean())
            {
                long sequence = reader.ReadInt64();
                int length = reader.ReadInt32();
                if (length <= 0 || length > buffer.Length - buffer.Position)
                {
                    throw new InvalidDataException("The service receipt label length is invalid.");
                }

                label = new(sequence, new SignedLabelSnapshot(reader.ReadBytes(length)));
            }

            LabelStoreState state = new(
                StreamId: Guid.ParseExact(reader.ReadString(), "N"),
                Issuer: reader.ReadString(),
                Revision: reader.ReadInt64(),
                IssuedThrough: reader.ReadInt64(),
                PublishedThrough: reader.ReadInt64(),
                ReplayFloor: reader.ReadInt64(),
                Phase: (LabelerLifecyclePhase)reader.ReadInt32(),
                RetirementUpperBound: reader.ReadInt64(),
                RetirementThrough: reader.ReadInt64(),
                GraceDeadline: Timestamp(ReadNullable(reader)));
            LabelAuditRecord audit = new(
                evaluatedAt: Timestamp(reader.ReadString())!.Value,
                authorizationVersion: reader.ReadString(),
                policyVersion: ReadNullable(reader),
                signingKeyMultibase: ReadNullable(reader));
            if (buffer.Position != buffer.Length)
            {
                throw new InvalidDataException("The service receipt contains trailing data.");
            }

            return new LabelOperationReceipt(
                request: request,
                resolvedTarget: resolved,
                changed: changed,
                label: label,
                stateAtCommit: state,
                audit: audit);
        }
        catch (Exception exception) when (exception is EndOfStreamException or FormatException or ArgumentException)
        {
            throw new InvalidDataException("The stored service receipt is malformed.", exception);
        }
    }

    /// <summary>Records target kind, original subject and optional CID without conflating account and record identities.</summary>
    private static void WriteTarget(BinaryWriter writer, LabelTarget target)
    {
        writer.Write((int)target.Kind);
        writer.Write(target.RecordUri?.ToString() ?? target.Repository.Value);
        WriteNullable(writer, target.Cid?.ToString());
    }

    /// <summary>Recreates a validated target and rejects unsupported kind or account-CID combinations.</summary>
    private static LabelTarget ReadTarget(BinaryReader reader)
    {
        LabelTargetKind kind = (LabelTargetKind)reader.ReadInt32();
        string subject = reader.ReadString();
        string? cid = ReadNullable(reader);
        return kind switch
        {
            LabelTargetKind.Account when cid is null => LabelTarget.Account(AtIdentifier.Create(subject)),
            LabelTargetKind.Record => LabelTarget.Record(new AtUri(subject), cid is null ? null : new Cid(cid)),
            _ => throw new InvalidDataException("The stored service target is invalid.")
        };
    }

    /// <summary>Writes an explicit presence flag so absent text stays distinct from a present empty value.</summary>
    private static void WriteNullable(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
        {
            writer.Write(value);
        }
    }

    /// <summary>Restores the presence flag and its associated text in the receipt's fixed field order.</summary>
    private static string? ReadNullable(BinaryReader reader) => reader.ReadBoolean() ? reader.ReadString() : null;

    /// <summary>Parses the invariant round-trip timestamp format used by service receipt metadata.</summary>
    private static DateTimeOffset? Timestamp(string? value) => value is null ? null :
        DateTimeOffset.ParseExact(value, "O", System.Globalization.CultureInfo.InvariantCulture);
}
