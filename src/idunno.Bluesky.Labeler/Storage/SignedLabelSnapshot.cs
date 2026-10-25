// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.AtProto.Labels;

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>Captures an immutable snapshot of an already signed label for exact persistence, publication and idempotent retries.</summary>
/// <remarks>
/// <para>
/// <see cref="Label"/> represents protocol data and can also describe an unsigned label. This class provides
/// the persistence boundary: it requires a signed version 1 representation and captures the complete signed
/// UTF-8 JSON together with the canonical unsigned DAG-CBOR payload used for signing. Keeping that snapshot
/// avoids reconstructing signed fields from database projections, which could change timestamp or CID text
/// and invalidate the signature.
/// </para>
/// <para>
/// Create a snapshot after signing and retain it when retrying the same append operation. Re-signing a decision
/// can produce different signature bytes, so it is not a substitute for resubmitting the original snapshot.
/// The JSON constructor preserves the supplied JSON bytes; the label constructor captures the library's
/// serialization of the supplied label. JSON whitespace and property order are not themselves signed.
/// </para>
/// <para>
/// Input buffers are copied, byte accessors return copies, and <see cref="Label"/> returns a fresh deserialized
/// object. Callers therefore cannot change the captured representation through a returned buffer or label.
/// This class neither signs labels nor verifies their cryptographic authenticity: it validates the representation
/// and signature length only. Verify untrusted labels against a trusted public key before accepting them.
/// </para>
/// </remarks>
/// <seealso href="https://atproto.com/specs/label">AT Protocol Labels specification (version 1)</seealso>
public sealed class SignedLabelSnapshot
{
    private static readonly JsonTypeInfo<Label> s_typeInfo = (JsonTypeInfo<Label>)AtProtoJsonSerializerOptions.Default.GetTypeInfo(typeof(Label));
    private readonly byte[] _json;
    private readonly byte[] _payload;

    /// <summary>Initializes a new instance of the <see cref="SignedLabelSnapshot"/> class.</summary>
    /// <param name="label">The signed version 1 label.</param>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The label has invalid fields or no compact signature.</exception>
    /// <remarks><para>This validates representation, not cryptographic trust. Verify against a trusted public key before submitting untrusted labels.</para></remarks>
    public SignedLabelSnapshot(Label label)
    {
        ArgumentNullException.ThrowIfNull(label);
        _payload = label.GetCanonicalUnsignedBytes();
        if (label.Signature?.ToBytes().Length != 64)
        {
            throw new InvalidDataException("A compact signed version 1 label is required.");
        }

        _json = JsonSerializer.SerializeToUtf8Bytes(label, s_typeInfo);
    }

    /// <summary>Initializes a new instance of the <see cref="SignedLabelSnapshot"/> class from exact signed JSON.</summary>
    /// <param name="signedJson">The complete signed UTF-8 JSON representation.</param>
    /// <exception cref="JsonException">The JSON cannot be read as a label.</exception>
    /// <exception cref="InvalidDataException">The label is missing, invalid or unsigned.</exception>
    public SignedLabelSnapshot(ReadOnlySpan<byte> signedJson)
    {
        _json = signedJson.ToArray();
        Label label = JsonSerializer.Deserialize(_json, s_typeInfo)
            ?? throw new InvalidDataException("A signed label is required.");
        _payload = label.GetCanonicalUnsignedBytes();
        if (label.Signature?.ToBytes().Length != 64)
        {
            throw new InvalidDataException("A compact signed version 1 label is required.");
        }
    }

    /// <summary>Gets a fresh deserialized label preserving its signed representations.</summary>
    public Label Label => JsonSerializer.Deserialize(_json, s_typeInfo)!;

    /// <summary>Gets a copy of the complete signed UTF-8 JSON.</summary>
    /// <returns>A new signed JSON buffer.</returns>
    public byte[] GetJsonBytes() => (byte[])_json.Clone();

    /// <summary>Gets a copy of the canonical unsigned DAG-CBOR signing payload.</summary>
    /// <returns>A new canonical payload buffer.</returns>
    public byte[] GetCanonicalUnsignedBytes() => (byte[])_payload.Clone();
}
