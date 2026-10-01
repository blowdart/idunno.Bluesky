// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;
using System.Globalization;

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

    // The CBOR initial byte of a map, and the additional information value saying its length follows in one byte.
    private const byte MapMajorType = 0xA0;
    private const byte OneByteLength = 24;

    /// <summary>
    /// The most fields a label can have for its signature to be checked. Every field is covered by the signature, so none can be
    /// skipped, and a label defines nine.
    /// </summary>
    internal const int MaximumSignedLabelFields = 64;

    private static readonly CborFieldNames s_payloadFields = new("labels");

    // The canonical CBOR encoding of the "sig" key, a three byte text string.
    private static ReadOnlySpan<byte> EncodedSignatureKey => [0x63, (byte)'s', (byte)'i', (byte)'g'];

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
                    GetUnsignedLabel(encodedLabels[i]),
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
                Signature = signature is null ? null : new Bytes(signature)
            };
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The label is invalid.", exception);
        }
    }

    /// <summary>
    /// Re-encodes a label without its signature, which is the data the signature covers.
    /// </summary>
    /// <param name="encodedLabel">The DAG-CBOR encoded label.</param>
    /// <returns>The DAG-CBOR encoded label without its <c>sig</c> field.</returns>
    /// <exception cref="InvalidDataException">The label is not a DAG-CBOR map, or has more than <see cref="MaximumSignedLabelFields"/> fields.</exception>
    /// <remarks>
    /// <para>The canonical reader rejects unsorted or duplicate keys and non-canonical values, so removing the signature entry from the
    /// encoded label, and shortening the map's length, gives exactly the bytes that re-encoding the remaining fields would.</para>
    /// </remarks>
    internal static byte[] GetUnsignedLabel(ReadOnlyMemory<byte> encodedLabel) => FirehoseCbor.Wrap(() =>
    {
        CborReader reader = new(encodedLabel, CborConformanceMode.Canonical);
        int length = reader.ReadStartMap() ?? throw new InvalidDataException("The label is an indefinite length map.");

        if (length > MaximumSignedLabelFields)
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"The label has {length} fields, more than the maximum of {MaximumSignedLabelFields}."));
        }

        int entriesStart = encodedLabel.Length - reader.BytesRemaining;
        int signatureStart = -1;
        int signatureEnd = -1;

        for (int i = 0; i < length; i++)
        {
            int entryStart = encodedLabel.Length - reader.BytesRemaining;

            if (reader.PeekState() != CborReaderState.TextString)
            {
                throw new InvalidDataException("The label has a key which is not a string.");
            }

            bool isSignature = reader.ReadEncodedValue().Span.SequenceEqual(EncodedSignatureKey);
            reader.SkipValue();

            if (isSignature)
            {
                signatureStart = entryStart;
                signatureEnd = encodedLabel.Length - reader.BytesRemaining;
            }
        }

        reader.ReadEndMap();

        int entriesEnd = encodedLabel.Length - reader.BytesRemaining;
        ReadOnlySpan<byte> label = encodedLabel.Span;

        if (signatureStart < 0)
        {
            return label[..entriesEnd].ToArray();
        }

        int unsignedLength = length - 1;
        int headerLength = unsignedLength < OneByteLength ? 1 : 2;
        int beforeSignature = signatureStart - entriesStart;
        byte[] result = new byte[headerLength + beforeSignature + (entriesEnd - signatureEnd)];

        if (unsignedLength < OneByteLength)
        {
            result[0] = (byte)(MapMajorType | unsignedLength);
        }
        else
        {
            result[0] = MapMajorType | OneByteLength;
            result[1] = (byte)unsignedLength;
        }

        label[entriesStart..signatureStart].CopyTo(result.AsSpan(headerLength));
        label[signatureEnd..entriesEnd].CopyTo(result.AsSpan(headerLength + beforeSignature));

        return result;
    });
}
