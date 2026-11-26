# AT Protocol Spaces (experimental)

AT Protocol Spaces are an alpha feature for permissioned data. The APIs in this library are experimental and may change or be removed. Using them emits the `BSKYSpaces` diagnostic; see the linked page for current guidance.

The current implementation supports creating a space and creating, replacing, or deleting records within a space. It does not expose space reads, synchronization, or membership management. In particular, `com.atproto.space.getRecord` uses space credentials and authenticated response MACs; it must not be treated as an ordinary unauthenticated repository read.

## Authentication

Space methods called with the account's own session reject app passwords, including privileged app passwords, with `InvalidToken` (`Bad token scope`). Use OAuth with the appropriate `space:` scopes, or a full account-password session for scripts and tests. The SDK returns the server error in `AtErrorDetail`; it does not convert an app-password session into a full session. Requests authenticated with a space credential, delegation token, or service authentication are unaffected by this restriction.

## Reading alpha-era record values

During the alpha, some record values contain the older `at://{spaceDid}/space/...` form. Standard URI parsing and JSON deserialization remain strict. For a URI extracted from a record value, `SpaceAtUri.TryParseRecordValue` accepts either form and returns a normalized `at:space/...` URI. Check `IsRecord` when the Lexicon requires a space reference rather than a record URI.

For typed JSON, apply `SpaceRecordValueUriConverter` **only to the record value**, not the surrounding response or other protocol data. For example, given an untyped `AtProtoRepositoryRecord` whose `Value` is a `JsonObject`:

```csharp
JsonSerializerOptions recordValueOptions = BlueskyJsonSerializerOptions.Create(
    options => options.Converters.Add(new SpaceRecordValueUriConverter()));
Repost? value = record.Value?.Deserialize<Repost>(recordValueOptions);
```

This example uses `System.Text.Json`, `idunno.AtProto.Json`, `idunno.Bluesky`, and `idunno.Bluesky.Feed`. Use the corresponding AT Protocol options and registered record metadata for non-Bluesky types. Existing typed repository reads and record `Parse` methods use strict options; obtain the untyped value and deserialize it separately when this compatibility path is needed. The converter handles `AtUri` and `SpaceAtUri` properties, not arbitrary strings or `JsonNode` values. It does not validate the rest of the record against its Lexicon.

Normalize extracted references before comparison or indexing. Do **not** rewrite stored record JSON: changing an embedded URI changes the record's CID. Applications should migrate their own persisted URI keys and extracted backlinks to the new form. Archives exported before the update must be re-exported from an upgraded host. Legacy support is temporary and will be removed before beta.

Keep protocol parameters, token subjects, MAC headers, archives, and new record input strict. When writing records containing untyped JSON or string fields, callers must ensure Lexicon URI fields use the current form; the SDK cannot identify arbitrary string fields without the record's schema.

## Creating a space

Pass the space type and a policy object to `AtProtoAgent.CreateSpace`. The policy is a Lexicon union value and must include the policy's `$type` discriminator:

```csharp
JsonObject policy = new()
{
    ["$type"] = "com.atproto.personalspace.policy"
};

AtProtoHttpResult<SpaceAtUri> result = await agent.CreateSpace(
    spaceType: "app.bsky.bookmark",
    policy: policy,
    cancellationToken: cancellationToken);

if (result.Succeeded)
{
    SpaceAtUri space = result.Result;
}
```

The optional key (`skey`) lets the caller choose a stable space key; when omitted, the service generates one. Policy values are defined by the relevant Lexicons, such as [personal-space policy](https://github.com/bluesky-social/atproto/blob/047d6032a21be01df8447489f0d6b59d2cc60af8/lexicons/com/atproto/personalspace/policy.json), [member-space policy](https://github.com/bluesky-social/atproto/blob/047d6032a21be01df8447489f0d6b59d2cc60af8/lexicons/com/atproto/memberspace/policy.json), and [managed-space policy](https://github.com/bluesky-social/atproto/blob/047d6032a21be01df8447489f0d6b59d2cc60af8/lexicons/com/atproto/managedspace/policy.json).

## Writing records

`CreateSpaceRecord`, `PutSpaceRecord`, and `DeleteSpaceRecord` operate on a space reference and the authenticated account's repository. The `space` argument must address the space itself, not a record URI. Record values must follow their Lexicon and include the appropriate `$type` discriminator.

These are separate from the public-repository `CreateRecord`, `PutRecord`, and `DeleteRecord` methods. Space records are permissioned; do not pass space URIs to general repository operations or assume that public repository reads can access them.

The endpoints currently supported are [`createSpace`](https://github.com/bluesky-social/atproto/blob/047d6032a21be01df8447489f0d6b59d2cc60af8/lexicons/com/atproto/space/createSpace.json), [`createRecord`](https://github.com/bluesky-social/atproto/blob/047d6032a21be01df8447489f0d6b59d2cc60af8/lexicons/com/atproto/space/createRecord.json), [`putRecord`](https://github.com/bluesky-social/atproto/blob/047d6032a21be01df8447489f0d6b59d2cc60af8/lexicons/com/atproto/space/putRecord.json), and [`deleteRecord`](https://github.com/bluesky-social/atproto/blob/047d6032a21be01df8447489f0d6b59d2cc60af8/lexicons/com/atproto/space/deleteRecord.json).

The feature is based on the [AT Protocol Spaces alpha announcement](https://atproto.com/blog/atproto-spaces-alpha), the [November 24 alpha migration guidance](https://github.com/bluesky-social/atproto/blob/permissioned-data/alpha-migrations/prompt-11-24-26.md), and the [permissioned data proposal](https://github.com/bluesky-social/proposals/tree/main/0016-permissioned-data).
