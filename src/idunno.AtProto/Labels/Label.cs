// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Labels
{
    /// <summary>
    /// Metadata tag on an atproto resource (eg, repo or record).
    /// </summary>
    /// <remarks>
    /// <para>See https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/label/defs.json</para>
    /// <para>See https://atproto.blue/en/latest/atproto/atproto_client.models.com.atproto.label.defs.html#atproto_client.models.com.atproto.label.defs.Label</para>
    /// </remarks>
    [SuppressMessage("Usage", "CA1054:Uri parameters should not be strings", Justification = "The label uri can be either an at uri or a did.")]
    [SuppressMessage("Usage", "CA1056:Uri properties should not be strings", Justification = "The label uri can be either an at uri or a did.")]
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed record Label : AtProtoObject
    {
        /// <summary>
        /// Creates a new instance of <see cref="Label"/>.
        /// </summary>
        /// <param name="version">The AT Protocol version of the label.</param>
        /// <param name="source"><see cref="Did"/> of the actor who created the <see cref="Label"/>.</param>
        /// <param name="uri"><see cref="AtUri"/> of the record, repository (account), or other resource that the <see cref="Label"/> applies to.</param>
        /// <param name="cid">Optional <see cref="Cid">Content Identifier</see> specifying the specific version of <paramref name="uri"/> resource the <see cref="Label"/> applies to.</param>
        /// <param name="value">The short string name of the value or type of the <see cref="Label"/>.</param>
        /// <param name="isNegationLabel">Flag indicating whether the <see cref="Label"/> is a negation label, overwriting a previous label.</param>
        /// <param name="creationTimestamp">Timestamp when the label was created.</param>
        /// <param name="signature">Signature of dag-cbor encoded label.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/>, <paramref name="uri"/> or <paramref name="value"/> are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="value"/> has a length &gt; 128 characters.</exception>
        public Label(
            int? version,
            Did source,
            string uri,
            Cid? cid,
            string value,
            bool isNegationLabel,
            DateTimeOffset creationTimestamp,
            IEnumerable<byte> signature)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNullOrEmpty(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, 128, nameof(value));

            Version = version;
            Source = source;
            Uri = uri;
            Cid = cid;
            Value = value;
            IsNegationLabel = isNegationLabel;
            CreationTimestamp = creationTimestamp;

            Signature = signature;
        }

        /// <summary>
        /// The AT Protocol version of this <see cref="Label"/>.
        /// </summary>
        [JsonPropertyName("ver")]
        public int? Version { get; init; }

        /// <summary>
        /// <see cref="Did"/> of the actor who created this <see cref="Label"/>.
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("src")]
        public Did Source { get; init; }

        /// <summary>
        /// A string which can either be a <see cref="Did"/>, to refer to an account level label, or an <see cref="AtUri"/> to reference to a record level label.
        /// </summary>
        /// <remarks>
        /// <para>Labels are a special case where uri can be a did to refer to an account level label, or an at uri to refer to a record</para>
        /// </remarks>
        [JsonRequired]
        public string Uri { get; init; }

        /// <summary>
        /// Optional <see cref="Cid">Content Identifier</see> specifying the specific version of <see cref="Uri"/> resource this <see cref="Label"/> applies to,
        /// if the Uri is an AT URI.
        /// </summary>
        public Cid? Cid { get; init; }

        /// <summary>
        /// The short string name of the value or type of this <see cref="Label"/>.
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("val")]
        public string Value { get; init; }

        /// <summary>
        /// Flag indicating whether this <see cref="Label"/> is a negation label, overwriting a previous label
        /// </summary>
        [JsonPropertyName("neg")]
        public bool IsNegationLabel { get; init; }

        /// <summary>
        /// Timestamp when this label was created.
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("cts")]
        public DateTimeOffset CreationTimestamp { get; init; }

        /// <summary>
        /// Signature of dag-cbor encoded label.
        /// </summary>
        [JsonPropertyName("sig")]
        public IEnumerable<byte> Signature { get; init; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => '{' + Value + '}';
    }
}
