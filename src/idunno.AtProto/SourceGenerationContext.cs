// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using idunno.AtProto.Admin;
using idunno.AtProto.Authentication.Models;
using idunno.AtProto.Labels;
using idunno.AtProto.Labels.Models;
using idunno.AtProto.Moderation;
using idunno.AtProto.Moderation.Model;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;
using idunno.AtProto.Server.Models;

namespace idunno.AtProto
{
    /// <exclude />
    [JsonSourceGenerationOptions(
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = false,
        GenerationMode = JsonSourceGenerationMode.Metadata,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = false)]

    [JsonSerializable(typeof(AtErrorDetail))]

    [JsonSerializable(typeof(EmptyResponse))]

    [JsonSerializable(typeof(AtIdentifier))]
    [JsonSerializable(typeof(AtUri))]
    [JsonSerializable(typeof(Cid))]
    [JsonSerializable(typeof(Did))]
    [JsonSerializable(typeof(DidDocument))]
    [JsonSerializable(typeof(Handle))]
    [JsonSerializable(typeof(Nsid))]
    [JsonSerializable(typeof(RecordKey))]

    [JsonSerializable(typeof(SubjectType))]
    [JsonSerializable(typeof(StrongReference))]
    [JsonSerializable(typeof(RepoReference))]
    [JsonSerializable(typeof(Commit))]

    [JsonSerializable(typeof(Label))]
    [JsonSerializable(typeof(List<Label>))]
    [JsonSerializable(typeof(LabelValueDefinition))]
    [JsonSerializable(typeof(ReadOnlyCollection<SelfLabel>))]
    [JsonSerializable(typeof(SelfLabel))]
    [JsonSerializable(typeof(SelfLabels))]
    [JsonSerializable(typeof(QueryLabelsResponse))]

    [JsonSerializable(typeof(ServerDescription))]
    [JsonSerializable(typeof(Links))]
    [JsonSerializable(typeof(Contact))]

    [JsonSerializable(typeof(RepoDescription))]
    [JsonSerializable(typeof(Blob))]
    [JsonSerializable(typeof(BlobReference))]

    [JsonSerializable(typeof(BaseSessionResponse))]
    [JsonSerializable(typeof(CreateSessionRequest))]
    [JsonSerializable(typeof(CreateSessionResponse))]
    [JsonSerializable(typeof(GetSessionResponse))]
    [JsonSerializable(typeof(RefreshSessionResponse))]

    [JsonSerializable(typeof(CreateRecordRequest))]
    [JsonSerializable(typeof(CreateRecordResponse))]
    [JsonSerializable(typeof(DeleteRecordRequest))]
    [JsonSerializable(typeof(DeleteRecordResponse))]
    [JsonSerializable(typeof(PutRecordRequest))]
    [JsonSerializable(typeof(PutRecordResponse))]
    [JsonSerializable(typeof(ApplyWritesCreateRequest))]
    [JsonSerializable(typeof(ApplyWritesCreateResponse))]
    [JsonSerializable(typeof(ApplyWritesDeleteRequest))]
    [JsonSerializable(typeof(ApplyWritesDeleteResponse))]
    [JsonSerializable(typeof(ApplyWritesRequest))]
    [JsonSerializable(typeof(ApplyWritesResponseBase))]
    [JsonSerializable(typeof(ApplyWritesResponse))]
    [JsonSerializable(typeof(ApplyWritesUpdateRequest))]
    [JsonSerializable(typeof(ApplyWritesUpdateResponse))]
    [JsonSerializable(typeof(CreateBlobResponse))]
    [JsonSerializable(typeof(ListRecordsResponse))]

    [JsonSerializable(typeof(CreateReportRequest))]
    [JsonSerializable(typeof(ModerationReport))]

    [JsonSerializable(typeof(ServiceToken))]

    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
