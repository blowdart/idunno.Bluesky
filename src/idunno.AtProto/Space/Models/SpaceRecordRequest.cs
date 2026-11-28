// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Space.Models;

/// <summary>
/// Contains the common information required to write a record in a space.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed record SpaceRecordRequest
{
    /// <summary>
    /// Creates a request to write a record in a space.
    /// </summary>
    /// <param name="space">The space to write to.</param>
    /// <param name="repo">The author repository.</param>
    /// <param name="collection">The record collection.</param>
    /// <param name="record">The record value.</param>
    /// <param name="rkey">The record key, if specified.</param>
    /// <param name="validate">The validation setting, if specified.</param>
    public SpaceRecordRequest(
        SpaceAtUri space,
        Did repo,
        Nsid collection,
        JsonNode record,
        RecordKey? rkey,
        bool? validate)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(record);

        Space = space;
        Repo = repo;
        Collection = collection;
        Record = record;
        RecordKey = rkey;
        Validate = validate;
    }

    /// <summary>
    /// Gets the space reference.
    /// </summary>
    [JsonRequired]
    public SpaceAtUri Space { get; init; }

    /// <summary>
    /// Gets the author repository.
    /// </summary>
    [JsonRequired]
    public Did Repo { get; init; }

    /// <summary>
    /// Gets the record collection.
    /// </summary>
    [JsonRequired]
    public Nsid Collection { get; init; }

    /// <summary>
    /// Gets the record key, if specified.
    /// </summary>
    [JsonPropertyName("rkey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RecordKey? RecordKey { get; init; }

    /// <summary>
    /// Gets the validation setting, if specified.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Validate { get; init; }

    /// <summary>
    /// Gets the record value.
    /// </summary>
    [JsonRequired]
    public JsonNode Record { get; init; }
}
