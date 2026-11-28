// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space.Models;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>Receives the signed user delegation token.</summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed record SpaceDelegationResponse([property: JsonRequired] string Token);

/// <summary>Sends the public proof key and optional authorization assertions to the space authority.</summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed record AuthorizeSpaceSyncRequest(
    SpaceAtUri Space,
    Did CredentialKey,
    string? DelegationToken,
    string? ClientAttestation,
    SpaceNotificationRegistration? Notify);

/// <summary>Receives a newly issued space credential and optional space revision.</summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed record AuthorizeSpaceSyncResponse(
    [property: JsonRequired] string Credential,
    TimestampIdentifier? SpaceRev);

/// <summary>Receives a record whose original JSON must be checked before typed interpretation.</summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed record SpaceRecordResponse(
    [property: JsonRequired] SpaceAtUri Uri,
    [property: JsonRequired] Cid Cid,
    [property: JsonRequired] JsonElement Value);

/// <summary>Receives a paginated record listing, which has no MAC profile in the alpha protocol.</summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed record ListSpaceRecordsResponse(
    [property: JsonRequired] List<SpaceRecordListEntry> Records,
    string? Cursor);

/// <summary>Preserves each listing's record identity and optional original value.</summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed record SpaceRecordListEntry(
    [property: JsonRequired] Nsid Collection,
    [property: JsonRequired] RecordKey Rkey,
    [property: JsonRequired] Cid Cid,
    JsonElement? Value);
