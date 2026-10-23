// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Encodes XRPC binary messages, retaining signed field text and representing signatures as CBOR bytes.</summary>
internal static class LabelFrames
{
    private static readonly string[] s_fields = ["cid", "cts", "exp", "neg", "sig", "src", "uri", "val", "ver"];
    private static readonly JsonTypeInfo<Label> s_typeInfo = (JsonTypeInfo<Label>)AtProtoJsonSerializerOptions.Default.GetTypeInfo(typeof(Label));

    /// <summary>Builds one sequenced label event with canonical field names but unchanged signed values and presence.</summary>
    internal static byte[] Labels(StoredLabel stored, int maximumBytes)
    {
        // The converter canonicalizes accepted property casing and numeric strings, not signed field values.
        Label label = stored.SignedLabel.Label;
        JsonElement root = JsonSerializer.SerializeToElement(label, s_typeInfo);
        CborWriter writer = new(CborConformanceMode.Canonical, allowMultipleRootLevelValues: true);
        Header(writer, "#labels");
        writer.WriteStartMap(2);
        writer.WriteTextString("seq");
        writer.WriteInt64(stored.Sequence);
        writer.WriteTextString("labels");
        writer.WriteStartArray(1);
        writer.WriteStartMap(s_fields.Count(field => root.TryGetProperty(field, out _)));
        foreach (string field in s_fields)
        {
            if (!root.TryGetProperty(field, out JsonElement value))
            {
                continue;
            }

            writer.WriteTextString(field);
            switch (field)
            {
                case "sig":
                    writer.WriteByteString(label.Signature!.ToBytes());
                    break;
                case "ver":
                    writer.WriteInt32(value.GetInt32());
                    break;
                case "neg":
                    writer.WriteBoolean(value.GetBoolean());
                    break;
                default:
                    writer.WriteTextString(value.GetString()!);
                    break;
            }
        }

        writer.WriteEndMap();
        writer.WriteEndArray();
        writer.WriteEndMap();
        byte[] frame = writer.Encode();
        if (frame.Length > maximumBytes)
        {
            throw new InvalidDataException("A label frame exceeds the hosting message limit.");
        }

        return frame;
    }

    /// <summary>Builds an unsequenced informational or terminal error message.</summary>
    internal static byte[] Notice(string name, string message, bool error)
    {
        CborWriter writer = new(CborConformanceMode.Canonical, allowMultipleRootLevelValues: true);
        if (error)
        {
            writer.WriteStartMap(1);
            writer.WriteTextString("op");
            writer.WriteInt32(-1);
            writer.WriteEndMap();
        }
        else
        {
            Header(writer, "#info");
        }

        writer.WriteStartMap(2);
        writer.WriteTextString(error ? "error" : "name");
        writer.WriteTextString(name);
        writer.WriteTextString("message");
        writer.WriteTextString(message);
        writer.WriteEndMap();
        return writer.Encode();
    }

    /// <summary>Writes the canonical XRPC message header as the first of two CBOR objects.</summary>
    private static void Header(CborWriter writer, string type)
    {
        writer.WriteStartMap(2);
        writer.WriteTextString("t");
        writer.WriteTextString(type);
        writer.WriteTextString("op");
        writer.WriteInt32(1);
        writer.WriteEndMap();
    }
}
