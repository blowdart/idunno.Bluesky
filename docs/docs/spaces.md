# AT Protocol Spaces (experimental)

AT Protocol Spaces are an alpha feature for permissioned data. The APIs in this library are experimental and may change or be removed. Using them emits the `BSKYSpaces` diagnostic; see the linked page for current guidance.

The current implementation supports creating, inspecting, listing, updating and deleting spaces, managing members, checking managed-space access, authorizing space credentials, and creating, replacing, deleting, reading or listing records within a space. Archive synchronization and subscriptions are not yet exposed. Space reads are separate from ordinary public repository reads.

## Authentication

Space methods called with the account's own session reject app passwords, including privileged app passwords, with `InvalidToken` (`Bad token scope`). Use OAuth with the appropriate `space:` scopes, or a full account-password session for scripts and tests. The SDK returns the server error in `AtErrorDetail`; it does not convert an app-password session into a full session. Requests authenticated with a space credential, delegation token, or service authentication are unaffected by this restriction.

## Space credentials and authenticated reads

`GetSpaceDelegationToken` obtains a short-lived user delegation from the account's PDS. `AuthorizeSpaceSync` resolves the space authority's PDS and exchanges that delegation, an optional supplied client attestation, or the policy's other authorization mechanism for a `SpaceCredential`. Despite its name, authorization alone does not synchronize records.

The SDK verifies delegation and credential JWT signatures against the issuer's DID signing key, their exact space and issuer, their lifetime, and the credential's binding to a freshly generated P-256 proof key. Agents use their configured PLC directory for host discovery and issuer/author-key verification. Delegation tokens are single-use: authorization consumes one even if the HTTP request fails, because the authority might have received it. A canceled operation that never starts authorization does not consume it.

Handle unsuccessful results through `StatusCode` and `AtErrorDetail`, as with other agent methods. For example, after obtaining a space reference and full record URI:

```csharp
AtProtoHttpResult<SpaceDelegationToken> delegation = await accountAgent.GetSpaceDelegationToken(
    space,
    cancellationToken: cancellationToken);

if (delegation.Succeeded)
{
    AtProtoHttpResult<SpaceCredential> authorization = await readerAgent.AuthorizeSpaceSync(
        space: space,
        delegationToken: delegation.Result,
        cancellationToken: cancellationToken);

    if (authorization.Succeeded)
    {
        using SpaceCredential credential = authorization.Result;
        AtProtoHttpResult<AtProtoRepositoryRecord> record = await readerAgent.GetSpaceRecord(
            uri: recordUri,
            credential: credential,
            cancellationToken: cancellationToken);
    }
}
```

These types are in `idunno.AtProto`, `idunno.AtProto.Space`, and `idunno.AtProto.Repo`. The reading agent does not need an account session. Authorization and reads do not forward the account's access token, even when the destination is the account's PDS.

Each authorization returns a separately owned disposable credential. Dispose it when access is no longer needed; expiry also releases its token and destroys its private key. Renewal is explicit and generates a new proof key. The SDK does not expose raw tokens for persistence or support credential import. A credential is scoped to one space but can be used with different record authors' PDSs in that space. Revocation can invalidate it before its advertised expiry, so handle authorization failures rather than retrying indefinitely.

`GetSpaceRecord` signs the credential and exact repository audience using HTTP Message Signatures. It requests a response MAC using a fresh ephemeral P-256 key, derives the export-only HPKE secret using platform ECDH and HKDF, and checks the response's MAC, requested URI and canonical DAG-CBOR CID before returning the original record value. No additional cryptography package is required. Missing or invalid authentication raises `InvalidDataException`; ordinary endpoint errors still return `AtErrorDetail`. The original JSON is hashed without normalizing embedded alpha-era URI strings.

`ListSpaceRecords` supports an optional collection, limit (1–1000, default 50), cursor and `excludeValues`. It sends the same credential proof and checks supplied values against their CIDs, but **the alpha protocol defines no response MAC for listings**. The listing's CIDs, authorship and completeness are not authenticated. Use `GetSpaceRecord` to authenticate individual returned identities; doing so still cannot prove listing completeness.

The credential proof deliberately covers only the credential and repository audience, not the request method or URL. It is reusable until expiry. Response MACs are not transferable proof of authorship, and neither mechanism encrypts records. Custom HTTP clients must protect resolved services against SSRF and disable automatic redirects.

`SpaceNotificationRegistration` optionally supplies a base HTTPS callback endpoint and opaque secret to authorization. The application must implement registration confirmation and update callbacks itself and retain the same registration when renewing. This SDK does not sign client-attestation JWTs or host those notification callbacks.

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

Pass the space type and a typed policy to `AtProtoAgent.CreateSpace`. The SDK serializes the policy's Lexicon `$type` discriminator:

```csharp
PersonalSpacePolicy policy = new();

AtProtoHttpResult<SpaceAtUri> result = await agent.CreateSpace(
    spaceType: "app.bsky.bookmark",
    policy: policy,
    cancellationToken: cancellationToken);

if (result.Succeeded)
{
    SpaceAtUri space = result.Result;
}
```

The optional key (`skey`) lets the caller choose a stable space key; when omitted, the service generates one. The SDK models personal, member and managed-space policies with `PersonalSpacePolicy`, `MemberSpacePolicy` and `ManagedSpacePolicy`. Other policy types are preserved as `UnknownSpacePolicy`, which can also be constructed from a JSON object containing its `$type`. Policy fields are defined by the corresponding Lexicons: [personal-space policy](https://github.com/bluesky-social/atproto/blob/047d6032a21be01df8447489f0d6b59d2cc60af8/lexicons/com/atproto/personalspace/policy.json), [member-space policy](https://github.com/bluesky-social/atproto/blob/047d6032a21be01df8447489f0d6b59d2cc60af8/lexicons/com/atproto/memberspace/policy.json), and [managed-space policy](https://github.com/bluesky-social/atproto/blob/047d6032a21be01df8447489f0d6b59d2cc60af8/lexicons/com/atproto/managedspace/policy.json).

`GetSpace` retrieves a space's policy, and `ListSpaces` returns an authenticated, paginated list filtered optionally by owner DID or space-type NSID. The list limit defaults to 50 and may be set from 1 to 100. `UpdateSpace` replaces the policy; changing its type discards the previous policy's state. `DeleteSpace` removes the space and its authority repository. These methods do not provide access to the records inside a space.

## Policy and membership

`PutSpaceMembers` adds members or updates their read and write access, and `RemoveSpaceMembers` removes them; each accepts at most 100 entries. `ListSpaceMembers` returns a page of members with an optional cursor. Its limit is 1–1000, with a server default of 100. These methods require authentication as the space authority and a member-space policy; nonexistent members are ignored on removal.

```csharp
SpaceMember member = new() { Did = "did:plc:example", Read = true, Write = false };
AtProtoHttpResult<EmptyResponse> update = await agent.PutSpaceMembers(
    space,
    [member],
    cancellationToken: cancellationToken);
```

`SpacePolicyRecord` represents the published `com.atproto.space.policy` record at key `self`. `SpaceMemberRecord` represents a published `com.atproto.memberspace.member` record keyed by the member DID; unlike `SpaceMember`, it does not contain the DID in its value. These records are authoritative only in the space authority's repository. Unknown policy union values are retained.

For hosts implementing managed spaces, `AtProtoServer.CheckManagedSpaceAccess` calls the managing application, not the authority's PDS. It requires a service-auth credential intended for that application, obtained by the host; an ordinary account session is not a substitute. Its optional `repo` and attested OAuth `clientId` identify the requester.

The managing-app endpoint URL comes from the DID document, not from `publishPolicy`. The managed-space policy's `managingApp` field contains a DID service reference in the form `did#serviceId`. The authority's PDS resolves that DID, selects the matching DID-document service entry by its fragment (for example, `#managed-space`), and takes the entry's `serviceEndpoint` as the base URL. It then appends `/xrpc/com.atproto.managedspace.checkAccess` and supplies the `space`, optional `repo`, optional `clientId`, and a service token whose audience is the `managingApp` reference and whose method is `com.atproto.managedspace.checkAccess`. Configure the managing application's DID document with an HTTPS `serviceEndpoint` that is reachable by the authority PDS.

The framework-neutral `ManagedSpaceAccessHandler` in `idunno.AtProto` verifies that token and passes an authenticated check to your `IManagedSpaceAccessDecider`. The `idunno.AtProto.AspNet.Spaces` package builds on it with dependency-injection and endpoint helpers:

```csharp
builder.Services.AddManagedSpaceAccess<MyAccessDecider>(options =>
{
    options.ManagingApp = new AtProtoServiceReference(
        new Did("did:plc:your-managing-app"),
        "managed-space");
});

app.MapManagedSpaceCheckAccess();
```

The `managed-space` fragment must match the service entry ID in the managing application's DID document. The helper requires HTTPS, rejects repeated request values, and returns the standard XRPC success or error shape. Its replay guard is in-memory by default; multi-instance managing apps should provide a shared implementation of `IServiceTokenReplayGuard`.

## Writing records

`CreateSpaceRecord`, `PutSpaceRecord`, and `DeleteSpaceRecord` operate on a space reference and the authenticated account's repository. The `space` argument must address the space itself, not a record URI. Record values must follow their Lexicon and include the appropriate `$type` discriminator.

These are separate from the public-repository `CreateRecord`, `PutRecord`, and `DeleteRecord` methods. Space records are permissioned; do not pass space URIs to general repository operations or assume that public repository reads can access them.

The [endpoint inventory](endpointStatus.md) lists the implemented Spaces methods and their pinned alpha Lexicons.

The feature is based on the [AT Protocol Spaces alpha announcement](https://atproto.com/blog/atproto-spaces-alpha), the [November 24 alpha migration guidance](https://github.com/bluesky-social/atproto/blob/permissioned-data/alpha-migrations/prompt-11-24-26.md), and the [permissioned data proposal](https://github.com/bluesky-social/proposals/tree/main/0016-permissioned-data).
