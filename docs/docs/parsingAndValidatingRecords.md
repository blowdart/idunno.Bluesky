# Parsing and validating records

[!include[Untrusted data warning](includes/untrusted-data-warning.md)]

Bots read records written by other clients. Those records may contain overlong text, oversized collections or invalid
nested values, but still contain useful information. A record can be readable without being valid for authoring.

The SDK's JSON `Parse` and `TryParse` methods separate reading a typed value from validating its constraints.
Use tolerant parsing to inspect a record, then decide which validation failures matter to your bot.

## Reading records in a bot

Pass `validate: false` to retain representable values that violate constraints. Validation is optional and can be performed
after parsing without changing the record.

This example accepts a `JsonElement` containing an `app.bsky.feed.post` record value. Its policy is to log validation
failures but still read the text; a bot that acts on facets or media should assess the relevant failures before using them.

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using idunno.Bluesky;

static void InspectPost(JsonElement record)
{
    if (!Post.TryParse(record, validate: false, out Post? post))
    {
        Console.Error.WriteLine("Skipping a record that cannot be read as a Post.");
        return;
    }

    ValidationResult[] errors = post.Validate(new ValidationContext(post)).ToArray();

    if (errors.Length > 0)
    {
        foreach (ValidationResult error in errors)
        {
            Console.Error.WriteLine($"{string.Join(", ", error.MemberNames)}: {error.ErrorMessage}");
        }
    }
    else
    {
        Console.WriteLine("The post passed SDK validation.");
    }

    Console.WriteLine(post.Text);
}
```

There are three outcomes: an unreadable record is skipped, a readable but invalid record is inspected according to the
bot's policy, and a record with no reported failures is processed normally. Parsing does not truncate, repair or normalize
constraint-invalid values to make them pass validation.

For [Jetstream](jetstream.md), a commit's `Record` contains the record value as a nullable `JsonElement`.
Check the event's collection and operation, and that a record is present, before passing it to the appropriate parser.
Pass the record value itself, not the event, repository response envelope or a view object.
Choose the parser from the collection's lexicon: a different collection with similar JSON is not necessarily a Bluesky post.

## Choosing a parsing mode

Both `JsonDocument` and `JsonElement` overloads are available.

| Call | Behavior |
| ---- | -------- |
| `Post.TryParse(document, out Post? post)` | Reads and validates; returns `false` with a null result if either fails. |
| `Post.TryParse(document, validate: false, out Post? post)` | Reads without constraint validation; returns `false` with a null result if the value cannot be read. |
| `Post.Parse(document)` | Reads and validates; throws `JsonException` if the value cannot be read or fails validation. |
| `Post.Parse(document, validate: false)` | Reads without constraint validation; throws `JsonException` if the value cannot be read. |

Use `TryParse` when rejecting individual input records is expected. Strict parsing is the default: omitting `validate`
does not opt into tolerance. Use tolerant parsing followed by validation when you need the record and its individual
validation failures; a failed strict `TryParse` does not return a partially populated record or a list of failures.

These methods consume already parsed JSON. If starting with JSON text, `JsonDocument.Parse` can itself throw
`JsonException` for malformed JSON before a record parser is called.

## What tolerant parsing accepts

Tolerant parsing retains constraint-invalid values that the SDK can represent, including over-limit text and collections,
invalid facet ranges, and invalid nested media dimensions or metadata. The supported constraints vary by type.

It does not accept arbitrary JSON. Incompatible JSON shapes, missing required structural fields and invalid protocol
identifiers still cause parsing to fail. For example, a blob must supply `ref`, `mimeType` and `size`; a missing size is
not interpreted as zero. Reply references require a non-null message ID.
Supplied `$type` discriminators must be strings. Unknown dollar-prefixed extension fields retain the SDK's normal handling;
tolerant parsing does not add support for every unknown lexicon or embed type.

Tolerant parsing is not a resource limit. Bound incoming payload sizes and processing costs independently.

## Inspecting validation failures

Supported types implement `IValidatableObject`. Call `Validate(new ValidationContext(record))` to enumerate their
validation failures. Validation cascades into supported nested values, including facets, media, captions and external cards.
Nested types can also be validated independently.
Validation checks required URI and CID members of embedded and associated strong references, mention DIDs, and nonblank
link facet values. Link facets retain their existing support for nonempty strings that are not valid absolute URIs;
validate those destinations before using them.
Profile links, pinned posts and starter pack attribution also validate their strong-reference members without fetching
referenced records. Message validation accepts record references and group-chat join links.
Thread and post gate targets must identify a post record, including its record key; tolerant parsing retains collection-only
targets so validation can report them.

Failures identify member paths, including collection indices, such as:

* `Facets[1].Features[0].Tag`
* `EmbeddedRecord.Media.Images[0].AspectRatio.Width`

Treat these failures as input to your application's policy. An invalid facet need not prevent a bot from reading plain
text, but it should not be used to slice text without checking its bounds. Passing validation is not proof of authenticity,
safe content or permission to perform an action.

Blob validation checks declared MIME types and sizes, not the actual file. Those values remain untrusted.
Draft local-file validation checks path text and never accesses the filesystem.

## Reading versus authoring limits

Public constructors, setters and authoring helpers retain their existing guards. Tolerant parsing is a separate reading
path, not a way to relax the authoring APIs or establish that a record is suitable for publishing.

Galleries illustrate the distinction. The
[`app.bsky.embed.gallery` lexicon](https://github.com/bluesky-social/atproto/blob/main/packages/api/lexicons/app/bsky/embed/gallery.json)
recommends a soft limit of ten items in authoring UIs, while reserving a schema ceiling of 20 items:

| Operation | Gallery item limit |
| --------- | ------------------ |
| Public constructors and authoring helpers | 10 items. |
| Strict record parsing and explicit validation | 20 items. |
| Tolerant record parsing | Retains collections beyond either limit; subsequent validation reports counts above 20. |

Other validation rules include the post's three-language maximum, text limits measured in graphemes and UTF-8 bytes,
and nested media constraints. Validation does not change the values it checks.
Image and video alternative text has no length limit in the embed lexicons.

List validation checks name and description limits, description facets and avatar blob metadata, retaining unknown
service-defined purpose values. Starter pack validation checks name and description limits, its list reference and feed collection.
Feed generator validation checks the required service DID and display name, display-name limits of 24 graphemes / 240 UTF-8
bytes, description limits of 300 graphemes / 3,000 bytes, description facets, and PNG/JPEG avatar metadata of at most
1,000,000 bytes. Unknown generator content modes retain the existing `GeneratorContentMode.Unknown` behavior.

## Supported types and scope

The following types provide JSON `Parse`/`TryParse` overloads and implement `IValidatableObject`.

| Types | Related documentation |
| ----- | --------------------- |
| `Post` | [Posting](posting.md) |
| `Actor.Profile`, `Actor.Link`, `Actor.Status` | [Profiles](profiles.md) |
| `Feed.Generator` | Feed generator repository records, not generator views. |
| `Graph.List`, `Graph.StarterPack` | List and starter pack repository records. |
| `Feed.Gates.ThreadGate`, `Feed.Gates.PostGate` | [Thread gates and post gates](threadGatesAndPostGates.md) |
| `Chat.MessageInput` | [Conversations and messages](conversationsAndMessages.md) |
| `Drafts.Draft`, `Drafts.DraftPost` | [Working with drafts](drafts.md) |

Supporting nested types provide recursive validation; they do not all expose standalone parsing methods.
`PostBuilder` also implements `IValidatableObject`, preserving its existing authoring validation rules.

This behavior is opt-in through these parsing methods. It does not make agent API responses, ordinary
`JsonSerializer.Deserialize` calls, view types or bot dispatch automatically tolerant.
It is also distinct from the SDK's general handling of unknown JSON metadata.
