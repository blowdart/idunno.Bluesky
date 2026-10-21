// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Labels;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Firehose;

/// <summary>
/// Decodes <c>com.atproto.label.subscribeLabels</c> payloads.
/// </summary>
/// <param name="options">The options which set the decoding limits.</param>
/// <param name="verifier">The signature verifier, or <see langword="null"/> to skip signature verification.</param>
internal sealed class LabelEventDecoder(FirehoseOptions options, FirehoseSignatureVerifier? verifier) : IFirehosePayloadDecoder
{
    /// <summary>
    /// The NSID of the endpoint.
    /// </summary>
    internal const string EndpointNsid = "com.atproto.label.subscribeLabels";

    private const string LabelsType = "#labels";

    private const string SignatureField = "sig";

    private static readonly CborFieldNames s_payloadFields = new("labels");

    /// <summary>
    /// The label fields read when decoding a label.
    /// </summary>
    internal static CborFieldNames LabelFields { get; } = new("cid", "cts", "exp", "neg", SignatureField, "src", "uri", "val", "ver");

    /// <inheritdoc/>
    public string Nsid => EndpointNsid;

    /// <inheritdoc/>
    public CborFieldNames PayloadFields => s_payloadFields;

    /// <inheritdoc/>
    public bool IsSequenced(string type) => type == LabelsType;

    /// <inheritdoc/>
    public Did? GetSubject(string type, CborFields fields) => null;

    /// <inheritdoc/>
    public async Task<FirehoseEvent> DecodeAsync(string type, long sequence, CborFields fields, CancellationToken cancellationToken)
    {
        if (type != LabelsType)
        {
            throw new InvalidDataException($"The message type '{type}' is not supported.");
        }

        IReadOnlyList<ReadOnlyMemory<byte>> encodedLabels = fields.GetArray("labels", options.MaximumLabelsPerMessage);
        List<Label> labels = new(encodedLabels.Count);

        foreach (ReadOnlyMemory<byte> encodedLabel in encodedLabels)
        {
            labels.Add(DecodeLabel(FirehoseCbor.ReadFields(encodedLabel, LabelFields)));
        }

        if (verifier is not null)
        {
            // Every source is counted before any is resolved, so a message claiming many sources costs no resolutions.
            if (HasMoreSourcesThan(labels, options.MaximumLabelSourcesPerMessage))
            {
                throw new InvalidDataException($"The labels message has labels from more than {options.MaximumLabelSourcesPerMessage} sources.");
            }

            Dictionary<Did, DidDocument?> documents = [];

            for (int i = 0; i < labels.Count; i++)
            {
                Label label = labels[i];

                if (label.Signature is null)
                {
                    throw new InvalidDataException("A label is not signed.");
                }

                await verifier.VerifyAsync(
                    label.Source,
                    SigningKeyVerifier.LabelSigningKeyFragment,
                    LabelSignatureVerifier.GetUnsignedLabel(label),
                    label.Signature.ToBytes(),
                    "label",
                    documents,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return new FirehoseLabelsEvent(sequence, labels.AsReadOnly());
    }

    private static bool HasMoreSourcesThan(List<Label> labels, int maximum)
    {
        if (labels.Count <= maximum)
        {
            return false;
        }

        HashSet<Did> sources = [];
        foreach (Label label in labels)
        {
            if (sources.Add(label.Source) && sources.Count > maximum)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Decodes a DAG-CBOR encoded <c>com.atproto.label.defs#label</c>.
    /// </summary>
    /// <param name="fields">The fields of the label.</param>
    /// <returns>The decoded label.</returns>
    /// <exception cref="InvalidDataException">The label is missing required fields or has invalid values.</exception>
    /// <remarks>
    /// <para>Over DAG-CBOR the signature is raw bytes and the CID is a string, rather than the JSON encodings.</para>
    /// </remarks>
    internal static Label DecodeLabel(CborFields fields)
    {
        int? version = null;
        if (!fields.IsAbsentOrNull("ver"))
        {
            long value = fields.GetInteger("ver");
            version = value is >= int.MinValue and <= int.MaxValue ? (int)value : throw new InvalidDataException("The label version is out of range.");
        }

        Cid? cid = null;
        string? cidValue = fields.GetOptionalString("cid");
        if (cidValue is not null && !Cid.TryParse(cidValue, out cid))
        {
            throw new InvalidDataException("The label CID is invalid.");
        }

        byte[]? signature = fields.IsAbsentOrNull(SignatureField) ? null : fields.GetBytes(SignatureField);
        bool negation = !fields.IsAbsentOrNull("neg") && fields.GetBoolean("neg");

        try
        {
            return new Label(
                version,
                fields.GetDid("src"),
                fields.GetString("uri"),
                cid,
                fields.GetString("val"),
                negation,
                fields.GetDateTime("cts"),
                signature: null)
            {
                ExpiresAt = fields.GetOptionalDateTime("exp"),
                CreationTimestampText = fields.GetString("cts"),
                CidText = cidValue,
                ExpiresAtText = fields.GetOptionalString("exp"),
                Negation = fields.IsAbsentOrNull("neg") ? null : negation,
                Signature = signature is null ? null : new Bytes(signature)
            };
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The label is invalid.", exception);
        }
    }
}
