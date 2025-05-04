# User lists

User lists are collections of users created by users. When lists are created they are given a purpose. The two main purposes are

* Curation: a list of users to drive custom feeds, or to set [thread gates](threadgates.md).
* Moderation : a list of users used for muting or blocking.

## Viewing a user's lists

To view lists created by a user use the `GetLists()` method.

`GetLists(did)`

| Parameter | Type   | Description                                           | Required   | Default   |
|-----------|--------|-------------------------------------------------------|:----------:|:---------:|
| did       | Did    | The DID of the user whose lists to retrieve           | Yes        |           |

[!code-csharp[](code/lists.cs#L11-L12)]

`GetLists()` returns a collection of `ListView`s, which contain details about the list including its name, description, avatar and
the number of items in the list.

## Viewing a list's details and members.

To view a lists details and its members call `GetList()` with the `at://` [uri](../commonTerms.md#uri) of the list.
`GetList(did)`. This returns the list details and a paginated collection of members.

`GetList(did)`

| Parameter | Type   | Description                                                         | Required   | Default   |
|-----------|--------|---------------------------------------------------------------------|:----------:|:---------:|
| uri       | AtUri  | The AtUri of the list whose details and members should be retrieved | Yes        |           |
| limit     | int    | The number of list items to return per page (max 100)               | No         |  50       |
| cursor    | string | A cursor that tells the server where to paginate from               | No         |  *null*   |

[!code-csharp[](code/lists.cs#L30-L31)]

You can use the `Cursor` property in the results from the `GetList()` call to paginate through the list members.

[!code-csharp[](code/lists.cs?highlight=1-2,13-14,22-27,29#L30-L58)]

## List management

### Creating a list

To create a list call `CreateList()` with an instance of `BlueskyList` containing the metadata you require.

`CreateList(list)`

| Parameter | Type           | Description                                                                 | Required   | Default   |
|-----------|----------------|-----------------------------------------------------------------------------|:----------:|:---------:|
| list      | BlueskyList    | An instance of BlueskyList containing the details of the list to be created | Yes        |           |

[!code-csharp[](code/manageLists.cs#L8-L12)]

### Updating a list's metadata

To update a list's metadata you can either call `UpdateList()` with results of a `GetListRecord()` on which you have changed
the metadata, or with `UpdateList()` providing the list `at://` [uri](../commonTerms.md#uri) and an instance of `BlueskyList`.

`UpdateList(list)`

| Parameter | Type                                 | Description                                                                     | Required   | Default   |
|-----------|--------------------------------------|---------------------------------------------------------------------------------|:----------:|:---------:|
| list      | AtProtoRepositoryRecord<BlueskyList> | A referenced instance of the BlueskyList containing the metadata to be updated. | Yes        |           |

[!code-csharp[](code/manageLists.cs?highlight=5#L18-L22)]

### Adding and removing a user from a list

To add a user to a list call `AddToList()` with the `at://` [uri](../commonTerms.md#uri) of the list and the [DID](../commonTerms.md#dids) of the user.

`AddToList(uri, did)`

| Parameter | Type  | Description                               | Required   | Default   |
|-----------|-------|-------------------------------------------|:----------:|:---------:|
| uri       | AtUri | The AtUri of the list to add the user to. | Yes        |           |
| did       | Did   | The Did of the user to add.               | Yes        |           |

[!code-csharp[](code/manageLists.cs#L28-L30)]

You can also supply the user's handle.

`AddToList(uri, handle)`

| Parameter | Type     | Description                               | Required   | Default   |
|-----------|----------|-------------------------------------------|:----------:|:---------:|
| uri       | AtUri    | The AtUri of the list to add the user to. | Yes        |           |
| handle    | Handle   | The Handle of the user to add.            | Yes        |           |

[!code-csharp[](code/manageLists.cs#L32-L34)]

Removing a user from a list requires calling `DeleteFromList()` with the same information required to a user to a list.

`DeleteFromList(uri, did)`

| Parameter | Type  | Description                               | Required   | Default   |
|-----------|-------|-------------------------------------------|:----------:|:---------:|
| uri       | AtUri | The AtUri of the list to add the user to. | Yes        |           |
| did       | Did   | The Did of the user to add.               | Yes        |           |

[!code-csharp[](code/manageLists.cs#L38-L40)]

You can also supply the user's handle.

`DeleteFromList(uri, handle)`

| Parameter | Type     | Description                               | Required   | Default   |
|-----------|----------|-------------------------------------------|:----------:|:---------:|
| uri       | AtUri    | The AtUri of the list to add the user to. | Yes        |           |
| handle    | Handle   | The Handle of the user to add.            | Yes        |           |

[!code-csharp[](code/manageLists.cs#L42-L44)]

### Deleting a list

To delete a list to call `DeleteList()` with the list's `at://` [uri](../commonTerms.md#uri)

`DeleteList()`

| Parameter | Type           | Description                     | Required   | Default   |
|-----------|----------------|---------------------------------|:----------:|:---------:|
| uri       | AtUri          | The AtUri of the list to delete | Yes        |           |

[!code-csharp[](code/manageLists.cs#L48-L49)]

## Using moderation lists

Moderation lists are used to mute or block the list members. Muting and blocking can be done with any user's lists, not
just lists created by the authenticated users.

To mute or block individual users see [muting users](muteAndUnmute.md) and [blocking users](blockingUsers.md).

Mutes are *private*, blocks are *public*.

### Muting and unmuting members of a moderation list

To mute all users of a moderation list use `MuteModList()`. Mutes are *private*.

`MuteModList(listUri)`

| Parameter | Type           | Description                              | Required   | Default   |
|-----------|----------------|------------------------------------------|:----------:|:---------:|
| listUri   | AtUri          | The AtUri of the moderation list to mute | Yes        |           |

```c#
await agent.MuteModList(listUri)
```

To unmute all users of a moderation list use `MuteModList()`.

`UnmuteModList(listUri)`

| Parameter | Type           | Description                              | Required   | Default   |
|-----------|----------------|------------------------------------------|:----------:|:---------:|
| listUri   | AtUri          | The AtUri of the moderation list to mute | Yes        |           |

```c#
await agent.UnmuteModList(listUri)
```

> [!TIP]
> Muting with a moderation list will mute the "live" list contents. If the list owner adds or removes
> an actor from a list this will reflect in the current user's mutes.

### Blocking and unblocking members of a moderation list

To mute all users of a moderation list use `BlockModList()`. Blocks are *public*.

`BlockModList(listUri)`

| Parameter | Type           | Description                               | Required   | Default   |
|-----------|----------------|-------------------------------------------|:----------:|:---------:|
| listUri   | AtUri          | The AtUri of the moderation list to block | Yes        |           |

```c#
await agent.BlockModList(listUri)
```

To unmute all users of a moderation list use `MuteModList()`.

`UnmuteBlockList(listUri)`

| Parameter | Type           | Description                               | Required   | Default   |
|-----------|----------------|-------------------------------------------|:----------:|:---------:|
| listUri   | AtUri          | The AtUri of the moderation list to block | Yes        |           |

```c#
await agent.UnmuteBlockList(listUri)
```

> [!TIP]
> Blocking with a moderation list will block the "live" list contents. If the list owner adds or removes
> an actor from a list this will reflect in the current user's blocks.


## Determining if a user is using a moderation list.

When you call `GetList()`, or any API that returns a `ListView` you can examine the `Viewer` property to
determine the relationship between the current user and the list. This can used to adjust your UX
accordingly.

If `Viewer` is `null` then the current user has no relationship with the list.

If the `Viewer` property is not `null` then the `Muted` property on `Viewer`
will indicate if the user is using the list to mute its members, and the `Blocked` property, if not null,
will contain the `at://` [uri](../commonTerms.md#uri) to the blocking record if the user is using
the list to block its members.

In addition `GetListBlocks()` and `GetListMutes()` return a paginated collection of the lists the
authenticated user is using to block and mute list members.

`GetListBlocks(cursor?, limit?)`

| Parameter | Type   | Description                                               | Required   |  Default  |
|-----------|--------|-----------------------------------------------------------|:----------:|:---------:|
| cursor    | string | A cursor that tells the server where to paginate from     | No         | *null*    |
| limit     | int    | The number of posts to return per page (max 100)          | No         | 50        |

```c#
var listBlocksResult = agent.GetListBlocks();
```

`GetListMutes(cursor?, limit?)`

| Parameter | Type   | Description                                               | Required   |  Default  |
|-----------|--------|-----------------------------------------------------------|:----------:|:---------:|
| cursor    | string | A cursor that tells the server where to paginate from     | No         | *null*    |
| limit     | int    | The number of posts to return per page (max 100)          | No         | 50        |

```c#
var listMutesResult = agent.GetListMutes();
```


## Using curation lists

Curation lists can be used to limit who can reply to a thread you created. For more information see [thread gates](threadGates.md).
