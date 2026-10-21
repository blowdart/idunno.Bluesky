# <a name="labels">Labels</a>
Labelers and [labels](https://docs.bsky.app/docs/advanced-guides/moderation) are how Bluesky allows for composable moderation.
A label is published by a moderation service, which a user or an application can choose to subscribe to.

[!include[Untrusted data warning](includes/untrusted-data-warning.md)]

Once subscribed to a labeler requests to a Bluesky API can request the labeler's labels to be applied to posts or actors,
and the labels will be applied to records underneath the labels key. When an application encounters a label it should act on it based on
the user's preferences for that labeler.

## Verifying issued labels

Labels returned by `QueryLabels`, or received through the Firehose label subscription, can have individual signatures.
This includes negation labels. Label policy definitions in a labeler declaration do not have individual label signatures.

```c#
using idunno.AtProto.Labels;

// Resolve the issuer's DID document over the network and verify one label.
await label.VerifyAsync(cancellationToken: cancellationToken);

// Alternatively, use a DID document obtained from a trusted resolver.
label.Verify(didDocument);

// Verify a collection from the same issuer, decoding its public key once.
LabelSignatureVerifier.Verify(didDocument, issuedLabels);
```

Verification returns normally on success and throws `InvalidDataException` for unsigned labels, unsupported label versions,
missing or mismatched issuer keys, or invalid signatures. Collection verification stops at the first failure; it does not
filter or silently discard invalid labels. Group labels by `Source` before verifying labels from multiple issuers.
The offline methods do not resolve identities or refresh keys. `VerifyAsync` accepts an optional `HttpClient`, resolves the issuer
once per invocation, and does not cache or retry; for a batch, resolve and reuse the DID document instead.
Cryptographic platform failures can throw `CryptographicException`, and cancellation propagates from `VerifyAsync`.

The SDK verifies version-one canonical DAG-CBOR label data against the issuer's `#atproto_label` key, with compact,
low-S ECDSA signatures for P-256 and secp256k1. It preserves exact timestamp and CID strings and whether `neg` was omitted.
JSON round trips and unchanged `with` copies retain this information. Setting a timestamp property replaces its original spelling
with a round-trip representation, and explicitly setting `IsNegationLabel` to `false` introduces a signed `neg: false` field.
Such changes can invalidate an existing signature even when the displayed timestamp or Boolean value is unchanged.
Setting `Cid` similarly replaces its original string with the CID's standard representation.
Other changes to signed properties are also included in subsequent verification, rather than checking stale retained bytes.
Unknown fields and `$type` are not signed.

A valid signature authenticates the issued label's contents against the supplied document; it does not establish trust in the
issuer, evaluate expiry, reconcile negations or supersession, or prove which key was published when the label was created.
The current DID document might no longer contain the key used before a rotation. Handle these policy and key-history decisions
separately. API queries still deserialize without automatically verifying labels; Firehose retains its explicit verification option.

## <a name="labelSubscriptions">Getting a user's labeler subscriptions</a>
A user's labeler subscriptions are part of a user's preferences, and can be loaded through the Bluesky agent's GetPreferences method once
the agent has authenticated.

```c#
using idunno.Bluesky.Actor;

Preferences userPreferences;
var userPreferencesResult = await agent.GetPreferences();
if (userPreferencesResult.Succeeded)
{
    userPreferences = userPreferencesResult.Result;
}
else
{
    userPreferences = new Preferences();
}
```

From there, you use the `SubscribedLabelers` property and pass that into any API that takes a `subscribedLabelers` parameter.
For example, to get a user's notifications with labels applied:

```c#
var notificationsList =
    await agent.ListNotifications(
      limit: pageSize,
      cursor: cursor,
      subscribedLabelers: userPreferences.SubscribedLabelers);
```

For notifications labels are applied to the `Author` property and the `PostView` property. You can iterate through the collection, and act on it.
For example, to display labels applied to the author of a notification:

```c#
foreach (Notification notification in notificationsList.Result)
{
    StringBuilder labelDisplayBuilder = new ();
    foreach (var label in notification.Author.Labels)
    {
        labelDisplayBuilder.Append(CultureInfo.InvariantCulture, $"[{label.Value}] ");
    }
    labelDisplayBuilder.Length--;

    Console.WriteLine($"Author: {notification.Author} {labelDisplayBuilder}");
}
```

Many APIs will take a `subscribedLabelers` parameter, including `GetProfile`, `GetSuggestions`, `SearchActors`, `GetFeed`, `GetTimeline` and so on.
It is recommended you cache the user's subscribed labelers and provide them to any API that accepts them.

By default idunno.Bluesky will return the Bluesky moderation labeler as part of a user's labeler subscriptions. This can be controlled
with the `includeBlueskyModerationLabeler` parameter on the `GetPreferences` method.

## <a name="labelReacting">Reacting to labels</a>
The value of a label will determine your application's behavior. Some example label values are porn, gore, and spam.
Label values are interpreted by their definitions. Those definitions include these attributes:

* blurs which may be content or media or none
* severity which may be alert or inform or none
* defaultSetting which may be hide or warn or ignore
* adultOnly which is a Boolean

A user chooses to hide, warn, or ignore each label from a labeler.

For more information on labels, their values and how your application should react to them, see the
[Bluesky moderation guide](https://docs.bsky.app/docs/advanced-guides/moderation).

## Publishing a labeler declaration

A labeler's policies and report scope live in its `app.bsky.labeler.service/self` repository record.
These helpers manage that declaration; they do not host a labeler, sign labels, or register the labeler
endpoint and public signing key in the account's DID document.

Use an authenticated `BlueskyAgent` to create a declaration:

```csharp
using idunno.Bluesky.Labeler;

Service declaration = new()
{
    CreatedAt = DateTimeOffset.UtcNow,
    Policies = new LabelerPolicies { LabelValues = ["spam"] },
    ReasonTypes = []
};

var createResult = await agent.CreateLabelerDeclaration(declaration);
if (!createResult.Succeeded)
{
    Console.Error.WriteLine($"Declaration creation failed: {createResult.StatusCode}, {createResult.AtErrorDetail}");
}
```

Creation uses the fixed `self` key and does not overwrite an existing declaration. The write helpers
check label-value lengths, custom identifiers, reserved custom-label names, duplicate definitions,
supported presentation settings, and localized text limits before sending a request. Custom definitions
must appear in `LabelValues` and include at least one locale. Reading existing declarations remains
permissive so future presentation values can still be inspected. The PDS also performs lexicon validation.

`ReasonTypes` has three distinct meanings:

| Value | Meaning |
| --- | --- |
| `null` (omitted from JSON) | Accept every report reason, including future additions. |
| `[]` | Accept no reports; appropriate for a publisher without report intake. |
| An explicit collection | Accept the selected reasons. |

Reasons are open strings: legacy `com.atproto.moderation.defs` reasons, granular
`tools.ozone.report.defs` reasons, and future identifiers are preserved without requiring Ozone.
`SubjectTypes` and `SubjectCollections` also preserve the distinction between omission and an empty array.
A `null` `SubjectCollections` accepts any record collection.

For updates, retrieve the record envelope to obtain its CID, then edit its value with a `with` expression:

```csharp
var readResult = await agent.GetLabelerDeclarationRecord();
if (readResult.Succeeded)
{
    Service edited = readResult.Result.Value with
    {
        ReasonTypes = ["tools.ozone.report.defs#reasonOther"]
    };

    var updateResult = await agent.UpdateLabelerDeclaration(edited, readResult.Result.Cid);
    if (!updateResult.Succeeded)
    {
        Console.Error.WriteLine($"Declaration update failed: {updateResult.StatusCode}, {updateResult.AtErrorDetail}");
    }
}
else
{
    Console.Error.WriteLine($"Declaration read failed: {readResult.StatusCode}, {readResult.AtErrorDetail}");
}
```

The update requires the expected CID. An `InvalidSwap` error means the declaration changed since it was read;
reload and reconcile the changes rather than blindly retrying. A `with` expression retains the creation time,
unchanged report scopes, policies, self-labels, and record extension data. Collection properties are shared
by a shallow record copy, so replace collections rather than mutating them in place.

**Breaking correction:** `Service.Labels` is now an optional `SelfLabels` object, matching the lexicon union,
instead of a collection of `SelfLabel`. For example, use
`Labels = new SelfLabels(new SelfLabel("!no-unauthenticated"))`.
Omitted `SubjectTypes` now remains omitted instead of becoming an empty array.
