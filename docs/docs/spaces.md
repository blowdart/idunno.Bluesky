# AT Protocol Spaces (experimental)

AT Protocol Spaces are an alpha feature for permissioned data. The APIs in this library are experimental and may change or be removed. Using them emits the `BSKYSpaces` diagnostic; see the linked page for current guidance.

The library supports space URI parsing and alpha-era record-value compatibility. Space creation and record write APIs are not included yet.

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

The feature is based on the [AT Protocol Spaces alpha announcement](https://atproto.com/blog/atproto-spaces-alpha), the [November 24 alpha migration guidance](https://github.com/bluesky-social/atproto/blob/permissioned-data/alpha-migrations/prompt-11-24-26.md), and the [permissioned data proposal](https://github.com/bluesky-social/proposals/tree/main/0016-permissioned-data).
