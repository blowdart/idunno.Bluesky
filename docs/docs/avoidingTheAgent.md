# Avoiding the Agent

The agents are meant as the main way to interact with Bluesky. However, there are scenarios where you might want to avoid
using agents, and talk to the PDS directly, bypassing an app view or other intermediate API.

The `AtProtoServer` class has methods to get, list create, update and delete records directly on a PDS, given appropriate credentials.

This approach entails you discovering the resolve a user handle to a DID, then discovering the PDS endpoint for a user. At that point
you can read and list records directly from that PDS. To create, update and delete records you will need to handle you must
authenticate with that PDS to get a session, create access credentials from the session, then using the access credentials
you can create, update and delete directly records on the PDS. You will also need to manually refresh sessions as they expire.

The [idunno.AtProto.Lexicons](https://github.com/blowdart/idunno.AtProto.Lexcions) library aims to provide common third party
record types as C# records, so you can work with strongly typed records.

To discover the PDS endpoint for a user you first resolve the DID from the user handle, then resolve the PDS for the DID.

```c#
sring userHandle = "example.bsky.social";

var did = await idunno.AtProto.Resolution.ResolveHandle(userHandle, cancellationToken: cancellationToken);
if (did is null)
{
    // Handle is invalid, error appropriately.
}

// Get the PDS for the DID.
var pds = await idunno.AtProto.Resolution.ResolvePds(did, cancellationToken: cancellationToken);
if (pds is null)
{
    // PDS could not be resolved, error appropriately.
}
```

Before you can use the `AtProtoServer` class you need to create a c# `HttpClient`.

```c#
var httpClientHandler = new HttpClientHandler()
{
    AutomaticDecompression = DecompressionMethods.All,
    UseCookies = false,
};

var httpClient = new HttpClient(httpClientHandler);
```

At this point you can use the `AtProtoServer` class to read and list records directly from the PDS, if you have a record definition.
If you want to use raw JSON please see the [Sending raw AT Protocol requests](rawClient.md).

For these examples we will use the [statusphere.xyz](https://statusphere.xyz) sample records,
which are defined in the [idunno.AtProto.Lexicons](https://github.com/blowdart/idunno.AtProto.Lexicons) library.

Assuming you have adding the `idunno.AtProto.Lexicons` nupkg, and added the appropriate `using` statements you could
list the statusphere status for a user like this:

```c#
var listResult = await AtProtoServer.ListRecords<StatusphereStatus>(
    repo: did,
    collection: StatusphereConstants.Collection,
    limit: 25,
    cursor: null,
    reverse : false,
    accessCredentials: null,
    service: pds,
    httpClient: httpClient);
```

You'll note that the AtProtoServer methods require you to specify a lot of parameters, and very few are optional. This is deliberate,
the assumption is if you are using the `AtProtoServer` class directly you are likely building your own higher level abstraction on top of it.

To retrieve an individual record you use `GetRecord`. This required credentials. So you would first need to authenticate with the PDS,
via the `CreateSession` method, then create access credentials from the session.

```c#
AtProtoAccessCredentials? accessCredentials;
var createSessionResult = await AtProtoServer.CreateSession(
                service: pds,
                identifier: userHandle,
                password: password,
                authFactorToken: null,
                authFactorToken: authCode,
                httpClient: httpClient);

if (createSessionResult.Succeeded)
{
    accessCredentials = createSessionResult.Result.ToAccessCredentials();
}
else
{
    // Handle error appropriately.
}
```

You must check the `CreateSessionResult` to ensure the session was created successfully. If a user has 2FA enabled
the `CreateSession` call will fail and the `AtErrorDetails` property will have an `Error` value of `AuthFactorCodeRequired`.
If that is returned you will need prompt the user for, and provide the MFA token in the `authFactorToken` parameter.

Once you have the access credentials you perform create, update and delete operations. For example, to create a new
statusphere status record with `CreateRecord` you would do:
```c#

var status = new StatusphereStatus
{
    Status = "😁"
};

var createRecordResult = await AtProtoServer.CreateRecord(
    record: status,
    creator: did,
    collection: StatusphereConstants.Collection,
    rKey: TimestampIdentifier.Next(),
    validate: false,
    swapCommit: null,
    service: pds,
    accessCredentials: accessCredentials,
    httpClient: httpClient);
```

When creating records it is typical to use `TimestampIdentifier.Next()` for the `rKey` parameter. This produces a unique
record key based on the current timestamp. Some applications may use "self" as a record key to identify a record of which a single
instance is being created (such as a user profile), or completely custom record keys.

To get a record  with `GetRecord` you need the repo (the user's DID), the collection, and the record key. For example, to get the record we just created

```c#
var getRecordResult = await AtProtoServer.GetRecord<StatusphereStatus>(
    repo: did,
    collection: StatusphereConstants.Collection,
    rKey: createRecordResult.Result.Uri.RecordKey!,
    cid: null, // get the latest version,
    service: pds,
    accessCredentials: null,
    httpClient: httpClient,
    cancellationToken: cancellationToken);
```

The `PutRecord` method allows you to update an existing record.
```c#
// Update the record we just retrieved.
StatusphereStatus statusToUpdate = getRecordResult.Result.Value;
statusToUpdate.Status = "😎";

var putRecordResult = await AtProtoServer.PutRecord(
    record: statusToUpdate,
    collection: StatusphereConstants.Collection,
    creator: did,
    rKey: getRecordResult.Result.Uri.RecordKey!,
    validate: false,
    swapCommit: null,
    swapRecord: getRecordResult.Result.Cid,
    service: pds,
    accessCredentials: accessCredentials);
```

Here you can use the `swapRecord` parameter to ensure you are updating the version of the record you think you are. If
the record has been updated since you retrieved it the update will fail.

Finally, to delete the record you just created you would do:

```c#
var deleteRecordResult = await AtProtoServer.DeleteRecord(
    repo: did,
    collection: StatusphereConstants.Collection,
    rKey: createResult.Result.Uri.RecordKey!,
    swapCommit: null,
    swapRecord: null,
    service: pds,
    accessCredentials: accessCredentials,
    httpClient: httpClient,
    loggerFactory: loggerFactory,
    cancellationToken: cancellationToken);
```
