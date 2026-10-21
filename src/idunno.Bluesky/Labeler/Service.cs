// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Labeler;

/// <summary>
/// Encapsulates a declaration of the existence of a labeler service.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true,
                 UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(Service), typeDiscriminator: RecordType.LabelerDeclaration)]
public record Service : BlueskyRecord
{
    // See https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/labeler/service.json

    /// <summary>
    /// Gets the policies the labeler publishes.
    /// </summary>
    public required LabelerPolicies Policies { get; init; }

    /// <summary>
    /// Gets the labels the labeler declares for itself, if any.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SelfLabels? Labels { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> the labeler declaration was created at.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the set of subject types (account, record, etc) this service accepts reports on.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ICollection<string>? SubjectTypes { get; init; }

    /// <summary>
    /// Gets the report reasons this service accepts.
    /// </summary>
    /// <remarks>
    /// <para>A value of <see langword="null"/> omits the field and accepts all reasons, including future additions.
    /// An empty collection accepts no reports. Values may include legacy reasons, granular
    /// <c>tools.ozone.report.defs</c> reasons, and future reason identifiers.</para>
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ICollection<string>? ReasonTypes { get; init; }

    /// <summary>
    /// Gets the set of record types (collection <see cref="Nsid"/>s) which can be reported to this service.
    /// If the value is <see langword="null"/>, as distinct from an empty collection the labeler accepts reports on any record type.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ICollection<Nsid>? SubjectCollections { get; init; }

}
