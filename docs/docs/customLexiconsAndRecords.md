# Custom Lexicons and Records

One of the features of [AT Protocol](https://atproto.com) is the ability to define custom [lexicons](https://atproto.com/guides/lexicon)
and APIs. This allows developers to use the protocol with their own data types and structures, enabling a wide range of applications and use cases.

To work with custom lexicons and records in `idunno.AtProto`, you can use the `AtProtoHttpClient` class to send raw requests to the
AT Protocol PDS but this becomes burdensome if you need to work with custom types frequently. To make it easier to work with custom
lexicons you can define your own records and use the generic `AtProtoHttpClient<TRecord>` class.

>[!Note]
> This document assumes you are familiar with the concepts of lexicons and records in AT Protocol.
> If you are not, please refer to the [AT Protocol Lexicon Guide](https://atproto.com/guides/lexicon) and [Data Model](https://atproto.com/specs/data-model)
> for more information.

## Defining a custom lexicon

Say, for example, you want to write records for what a user is currently listening to (check out (team.fm)[https://teal.fm/] who are doing
this in reality). A listening record might need the following information.

* Track Name
* Artist
* Album Name
* Date and Time listening started

This could be represented as lexicon like this:

```json
{
  "lexicon": 1,
  "id": "blue.idunno.listeningto.track",

   "defs": {
     "main" : {
      "type": "record",
      "description": "Record containing a listening to track.",
      "key": "tid",
      "record": {
        "type": "object",
        "required": ["trackName", "artist", "listeningStartedAt"],
        "properties": {
          "trackName": {
            "type": "string",
            "maxLength": 3000,
            "maxGraphemes": 300,
            "description": "The name of track listened to."
          },
          "artist": {
            "type": "string",
            "maxLength": 3000,
            "maxGraphemes": 300,
            "description": "The track artist."
          },
          "albumName": {
            "type": "string",
            "maxLength": 3000,
            "maxGraphemes": 300,
            "description": "The album name."
          },
          "listeningStartedAt":
          {
            "type": "string",
            "format": "datetime",
            "description": "Client-declared timestamp listening started at"
          }
        }
      }
    }
  }
}
```

## Representing a lexicon in C#

To represent this in C#, you could define a suitable record

```csharp
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace Samples.CustomRecords
{
    internal sealed record Track : AtProtoRecord
    {
        [JsonConstructor]
        public Track(string name, string artistName, string? albumName, DateTimeOffset? listeningStartedAt = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentException.ThrowIfNullOrEmpty(artistName);

            listeningStartedAt ??= DateTimeOffset.UtcNow;

            if (name.Length > 3000 || name.GetGraphemeLength() > 300)
            {
                 // Arbitrary limits for demonstration purposes.
                throw new ArgumentException("Track name is too long.", nameof(name));
            }

            if (artistName.Length > 3000 || artistName.GetGraphemeLength() > 300)
            {
                // Arbitrary limits for demonstration purposes.
                throw new ArgumentException("Artist name is too long.", nameof(artistName));
            }

            if (albumName is not null && (albumName.Length > 3000 || albumName.GetGraphemeLength() > 300))
            {
                // Arbitrary limits for demonstration purposes.
                throw new ArgumentException("Album name is too long.", nameof(albumName));
            }

            Name = name;
            Artist = artistName;
            AlbumName = albumName;
            ListeningStartedAt = listeningStartedAt.Value;
        }

        [JsonInclude]
        [JsonRequired]
        public string Name { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public string Artist { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public string? AlbumName { get; internal set; }

        [JsonInclude]
        public DateTimeOffset ListeningStartedAt { get; internal set; } = DateTimeOffset.UtcNow;
    }
}
```

Depending on how mutable you want your record implementation to be, you may prefer to move validation to the individual property setters.

All AT Protocol records must inherit from `AtProtoRecord`. The properties of the record should match the properties defined in the lexicon.

You can use the standard [JSON serialization attributes](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties) to customize the serialization if needed.

> [!Tip]
> Don't forget about [JSON code generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) if you are using AOT.

## Using your custom lexicon records with AtProtoAgent

Then, to create a record, you need the collection to store it in, an instance of the record and then call `agent.CreateRecord()`

```c#
const string collection = "blue.idunno.listening.track";

var track = new Track("Shake it off", "Taylor Swift", "1989");

var createResult = await agent.CreateRecord(
    record: track,
    collection: collection,
    rKey: TimestampIdentifier.Next(),
    validate: false);
```

You can see here that `TimestampIdentifier.Next()` is used to generate the optional record key (rKey), which
generates a [Timestamp Identifier (TID)](https://atproto.com/specs/tid) that allows the records to be sortable by time in the collection.

`CreateRecord` creates a new record in the repository of the authenticated user, in the collection specified.

If you're using JSON source generation you will also want to build your `JsonSerializerOptions` chained from the AtProto serialization options.

```c#
const string collection = "blue.idunno.listening.track";
JsonSerializerOptions jsonSerializerOptions = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(SourceGenerationContext.Default);

var track = new Track("Shake it off", "Taylor Swift", "1989");

var createResult = await agent.CreateRecord(
    record: track,
    jsonSerializerOptions: jsonSerializerOptions,
    collection: collection,
    rKey: TimestampIdentifier.Next(),
    validate: false,
    cancellationToken: cancellationToken);
```

> [!TIP]
> You can use [atp.tools](https://atp.tools) to browse your own (and other's) collections for debugging.

The result from `CreateRecord`, an `AtProtoHttpResult<CreateRecordResult>` allows you to check for success with the `Succeeded` property,
and the `Result` property includes a `StrongReference` to the newly created record.

AtProto provides [createRecord](https://docs.bsky.app/docs/api/com-atproto-repo-create-record),
[getRecord](https://docs.bsky.app/docs/api/com-atproto-repo-get-record), [putRecord](https://docs.bsky.app/docs/api/com-atproto-repo-put-record)
and [deleteRecord](https://docs.bsky.app/docs/api/com-atproto-repo-delete-record) endpoints to manipulate records in a repository, as well
as [listRecords](https://docs.bsky.app/docs/api/com-atproto-repo-list-records) to enumerate records in a collection, and
[applyWrites](https://docs.bsky.app/docs/api/com-atproto-repo-apply-writes) to allow for batched transactional operations on a repository.
Each of these endpoints has the equivalent method on `AtProtoAgent` that you can use with your custom records.
