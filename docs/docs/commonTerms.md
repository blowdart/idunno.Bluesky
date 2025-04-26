# Concepts & Common Terms 

This documentation contains some common terms that you will see when discussing AT Protocol and Bluesky APIs.

## <a name="atUris">at:// uri, cid, and Strong References</a>

If you looked at the return value from `agent.Post()` you would have seen that the API returns some information, including a `StrongReference`, a `Uri` and a `Cid`.

### <a name="uri">at:// uris</a>
This is not an HTTPS URI, it is an [`at://` uri](https://atproto.com/specs/at-uri-scheme). An `at://` uri is a reference to a record on the network.

It is made up of three parts, an authority, a collection and a record key:

`"at://" AUTHORITY [ "/" COLLECTION [ "/" RKEY ] ]`

The authority is a [DID](#dids), which is who or what owns the content, the collection is used as a grouping of record types, and the rKey is the key to the individual record.

### <a name="cid">CID</a>
This is a [content identifier](https://github.com/multiformats/cid), a way to reference a revision of a record pointed to by an `at://` uri.
It is implemented as a hash of the record.

### <a name="strongReference">StrongReference</a>

A `StrongReference` is a record contain both the [at:// uri](#uri) of a record, and its [CID](#cid).

## <a name="repositories">Repositories</a>

Bluesky stores all your stuff in a repository on a Personal Data Server (PDS).
A repository contains various collections, and these collections contain records.
For example, when you write a post you are cerating a record in your repository in the `app.bsky.feed.post` collection.

A simple post record would, in JSON, look something like this

```json
{
  "text": "Hello World!",
  "$type": "app.bsky.feed.post",
  "createdAt": "2023-08-07T05:31:12.156888Z"
}
```

The shape of a record is defined by the lexicon, for example you can find the definition for a post record in the
[Bluesky Feed lexicon](https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/feed/post.json).

## <a name="records">Records, Collections and NSIDs</a>

Collections are identified by namespace identifiers ([NSID](https://atproto.com/specs/nsid)s), which make up part of the [`at://` uri](commonTerms.md#atUris).

A record can be referred to by its (strong reference)[#strongReference], or by an `at://` uri.

If we examine an `at://` uri, for example, `at://sinclairinat0r.com/app.bsky.feed.post/3l5ptjwzotx2h`, we can break it down into the following parts

* An authority of `sinclairinat0r.com`, which can also be read as the repository name.
* A collection of `app.bsky.feed.post`, the NSID of the collection the record is in. In this case it's in the post repository, which contains all the users posts.
* A record key of `3jwdwj2ctlk26`, a key to the latest version an individual post in the record collection.

Records can, according to the AT Protocol, have multiple versions. A (strong reference)[#strongReference] contains not only an [at:// uri](#uri)
but also a [CID](#cid) which refers to a individual revision of a record.

## <a name="actorsHandlesDids">Actors, handles and DIDs</a>

The Bluesky API documentation refers to a user or bot account as an`Actor`. Actors can be identified in one of two ways, a handle and a Distributed Identifier (`DID`)

### <a name="handles">Handles</a>

A `handle` is what you see in human readable terms, for example "blowdart.me". It's how you view a profile in the browser, for example https://bsky.app/profile/blowdart.me and it's how you @ people in posts.

However as you may have discovered handles can change. When registering on Bluesky you register with a *something*.bsky.social handle, which you can then change to be DNS "verified". If you change your handle your posts still remain, and people that follow you keep following you through the change. This is because underneath handles aren't often used by the APIs, an account is referenced by its `DID`.

### <a name="dids">Distributed Identifier (DID)</a>

A `DID` is your unique key on the atproto network. Under the covers Bluesky uses `DID`s, not handles for pretty much everything, sometimes accepting both and doing the work behind the scenes to resolve a handle into a `DID``.

A `DID` looks something like this: `did:plc:hfgp6pj3akhqxntgqwramlbg`. The first part of the `DID` is the prefix declaring it's a `DID`, the second part is an identifier for the issuer (`plc` is a `DID` issued by Bluesky, `web` is another common identifier indicating an independently issued `DID`), and the final part is a unique reference issued by the issuer.

For example, the api to [get an actor profile](https://docs.bsky.app/docs/api/app-bsky-actor-get-profile) takes a `DID`or a handle (this either/or combination is represented as an At-Identifier), but things like [updateAccountPassword](https://docs.bsky.app/docs/api/com-atproto-admin-update-subject-status) take just a `DID`.

When you login via an agent the authenticated user's `DID` is available via the `Did` property on the agent instance.

Bluesky's `DID` directory is available at https://web.plc.directory/. It servers up a `DidDoc` for a plc `DID` which allows discovery of things like a `DID`'s personal data server where authenticated API calls should go. The `BlueskyAgent` class take care of PDS discovery automatically, but you can retrieve a `DidDoc` yourself using the `DirectoryAgent` `ResolveDidDocument` method.

### <a name="resolvingHandles">Resolving a Handle to a DID</a>

The `AtProto` and `Bluesky` agents provide a method to resolve a `DID` for a `Handle`, `ResolveHandle()`.

## <a name="views">Views</a>

As you explore the APIs you while you write records you don't get records, instead you get views of records. You create a `Post` but what you see in your timeline is a `PostView`.

A view is how Bluesky aggregates information from multiple records into a single entity. For example a `PostView` takes information from not only the `Post` record, but also information about the post author via a `ProfileView`, and things like reply and like counts. If you've used a SQL database before you can think of a view like a select over multiple joined tables.

Views are generated by applications, the overall view over data that Bluesky presents is commonly referred to as the `AppView`.

