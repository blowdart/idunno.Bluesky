// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;

namespace idunno.AtProto.Labels;

/// <summary>
/// Encodes the signed fields of supported label versions.
/// </summary>
internal static class LabelEncoding
{
    internal static byte[] Encode(Label label)
    {
        if (label.Version != 1)
        {
            throw new InvalidDataException("The label must have version 1.");
        }

        if (label.Source is null || label.Uri is null || label.Value is null ||
            (!AtUri.TryParse(label.Uri, out _) && !System.Uri.TryCreate(label.Uri, UriKind.Absolute, out _)) ||
            label.Value.GetUtf8Length() > 128 ||
            !ProtocolTimestamp.TryParse(label.CreationTimestampText, out _) ||
            (label.ExpiresAtText is not null && !ProtocolTimestamp.TryParse(label.ExpiresAtText, out _)))
        {
            throw new InvalidDataException("The label has invalid signed fields.");
        }

        return EncodeVersionOne(label);
    }

    private static byte[] EncodeVersionOne(Label label)
    {
        CborWriter writer = new(CborConformanceMode.Canonical);
        writer.WriteStartMap(5 + (label.CidText is null ? 0 : 1) + (label.ExpiresAtText is null ? 0 : 1) + (label.Negation.HasValue ? 1 : 0));
        if (label.CidText is not null)
        {
            writer.WriteTextString("cid");
            writer.WriteTextString(label.CidText);
        }

        writer.WriteTextString("cts");
        writer.WriteTextString(label.CreationTimestampText);
        if (label.ExpiresAtText is not null)
        {
            writer.WriteTextString("exp");
            writer.WriteTextString(label.ExpiresAtText);
        }

        if (label.Negation.HasValue)
        {
            writer.WriteTextString("neg");
            writer.WriteBoolean(label.Negation.Value);
        }

        writer.WriteTextString("src");
        writer.WriteTextString(label.Source.ToString());
        writer.WriteTextString("uri");
        writer.WriteTextString(label.Uri);
        writer.WriteTextString("val");
        writer.WriteTextString(label.Value);
        writer.WriteTextString("ver");
        writer.WriteInt32(1);
        writer.WriteEndMap();
        return writer.Encode();
    }
}
