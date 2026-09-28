// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable CS0618 // Old serialized event contracts remain registered for compatibility.

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

using idunno.AtProto.Admin;
using idunno.AtProto.Authentication;
using idunno.AtProto.Authentication.Models;
using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;
using idunno.AtProto.Jetstream.Models;
using idunno.AtProto.Labels;
using idunno.AtProto.Labels.Models;
using idunno.AtProto.Moderation;
using idunno.AtProto.Moderation.Model;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;

namespace idunno.AtProto;

/// <exclude />
[JsonSourceGenerationOptions(
    AllowOutOfOrderMetadataProperties = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    IgnoreReadOnlyProperties = false,
    GenerationMode = JsonSourceGenerationMode.Default,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = false)]

[JsonSerializable(typeof(AtErrorDetail))]

[JsonSerializable(typeof(EmptyResponse))]

[JsonSerializable(typeof(AtIdentifier))]
[JsonSerializable(typeof(AtUri))]
[JsonSerializable(typeof(Blob))]
[JsonSerializable(typeof(Cid))]
[JsonSerializable(typeof(CidLink))]
[JsonSerializable(typeof(Did))]
[JsonSerializable(typeof(DidDocument))]
[JsonSerializable(typeof(Handle))]
[JsonSerializable(typeof(Nsid))]
[JsonSerializable(typeof(RecordKey))]
[JsonSerializable(typeof(TimestampIdentifier))]

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

[JsonSerializable(typeof(Server.ServerDescription))]
[JsonSerializable(typeof(Server.Links))]
[JsonSerializable(typeof(Server.Contact))]

[JsonSerializable(typeof(Sync.HostDescription))]
[JsonSerializable(typeof(Sync.HostStatus))]
[JsonSerializable(typeof(Sync.RepoHostingStatus))]
[JsonSerializable(typeof(Sync.RepoStatus))]
[JsonSerializable(typeof(Sync.Model.ListBlobsResponse))]
[JsonSerializable(typeof(Sync.Model.ListHostsResponse))]
[JsonSerializable(typeof(Sync.HostedRepository))]
[JsonSerializable(typeof(Sync.Model.ListReposResponse))]
[JsonSerializable(typeof(Sync.Model.ListReposByCollectionResponse))]
[JsonSerializable(typeof(Sync.Model.RequestCrawlRequest))]

[JsonSerializable(typeof(RepoDescription))]

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

[JsonSerializable(typeof(AtJetstreamEvent))]
[JsonSerializable(typeof(JetstreamEvent))]
[JsonSerializable(typeof(JetstreamCommitEvent))]
[JsonSerializable(typeof(JetstreamAccountEvent))]
[JsonSerializable(typeof(JetstreamIdentityEvent))]
[JsonSerializable(typeof(JetstreamSyncEvent))]
[JsonSerializable(typeof(JetstreamCommit))]
[JsonSerializable(typeof(JetstreamAccount))]
[JsonSerializable(typeof(JetstreamIdentity))]
[JsonSerializable(typeof(JetstreamSync))]
[JsonSerializable(typeof(JetStreamEventKind))]
[JsonSerializable(typeof(JetstreamCommitOperation))]
[JsonSerializable(typeof(AtJetstreamAccountEvent))]
[JsonSerializable(typeof(AtJetstreamCommitEvent))]
[JsonSerializable(typeof(AtJetstreamIdentityEvent))]
[JsonSerializable(typeof(AtJetstreamSyncEvent))]
[JsonSerializable(typeof(AtJetstreamSync))]
[JsonSerializable(typeof(JetstreamV2EventPayload))]
[JsonSerializable(typeof(OptionsUpdateMessage))]
[JsonSerializable(typeof(SnapshotPlan))]
[JsonSerializable(typeof(PlannedSegment))]
[JsonSerializable(typeof(BlockRange))]
[JsonSerializable(typeof(SnapshotPlanStats))]
[JsonSerializable(typeof(SegmentList))]
[JsonSerializable(typeof(SegmentInfo))]
[JsonSerializable(typeof(SnapshotRequest))]
[JsonSerializable(typeof(SnapshotCheckpoint))]

[JsonSerializable(typeof(OAuthLoginState))]

[JsonSerializable(typeof(AtProtoRepositoryRecord))]

[JsonSerializable(typeof(OAuthResponseError))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

#pragma warning restore CS0618