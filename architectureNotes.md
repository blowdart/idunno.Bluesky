# idunno.Bluesky Architecture notes

This is mostly notes to myself so I don't forget things.

## Agent and API mapping

The `AtProtoHttpClient` provides methods to issued GET and PUSH requests to AtProto or Bluesky APIs..

The `AtProtoServer` and `BlueskyServer` classes contain static methods which call specific ATProto or Bluesky APIs using an `AtProtoHttpClient`.

The `AtProtoHttpResult` class is returned by the `AtProtoServer` and `BlueskyServer` API methods, wrapping the result of the API calls, if any, and/or any error details from the call.

The `AtProtoAgent` and `BlueskyAgent` classes provide a higher level of abstraction for interacting with the ATProto and Bluesky APIs. Furthermore they provide authentication and session management.

## Lexicon

AtProto and Bluesky records and APIs are defined in the [lexicon](https://github.com/bluesky-social/atproto/tree/main/lexicons).

## Record structure

### AtProto

| Record Name | Json | Extension Data | Description |
|_____________|______|_____________|
|```repo/AtProtoObject```| ✗ | ✗ |Abstract record from which all atproto or bluesky records *must* inherit from |
|```repo/AtProtoReferencedObject```| ✓ | ✗ | Abstract record from which all all records which have a strong reference (Uri/Cid) should inherit from.<br />Inherits from `AtProtoObject.` |
|```repo/AtProtoRecord```| ✓ | ✓ | Base record for all AT Proto records. Contains a Value (repo/AtProtoRecordValue) property, and overflow extension data |
|```repo/AtProtoRecordValue``` | ✓ | ✓ | Abstract record for values contained within an AtProto record. |

| Class Name | Json | Extension Data | Description |
|_____________|______|_____________|
|```repo/AtProtoObjectCollection```| ✗ | ✗ | ReadOnly collection of AtProtoObjects with a cursor property for API based pagination.

## Bluesky
### Records and Views

Most Bluesky records have a one or more views associated with it. Typically it is views that are returned from API calls.
All Bluesky views *must* inherit from the `idunno.Bluesky.View` record.

| Class Name | Json | Extension Data | Description |
|_____________|______|_____________|
|```/PagedViewCollection```| ✗ | ✗ | ReadOnly collection of Views with a cursor property for API based pagination.


### Labels
