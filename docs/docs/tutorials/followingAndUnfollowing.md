# Following and unfollowing

## Following

To follow a user call `agent.Follow()` with the [DID](../commonTerms.md#dids) of the user you want to follow.

`Follow(did)`

| Parameter    | Type | Description                    | Required   |
|--------------|------|--------------------------------|:----------:|
| did          | Did  | The DID of the user to follow. | Yes        |

```c#
await agent.Follow(did);
```

If you only know the [handle](../commonTerms.md#handles) of the user you can pass that instead, and the agent will resolve it to a DID for you.

`Follow(handle)`

| Parameter    | Type   | Description                       | Required   |
|--------------|--------|-----------------------------------|:----------:|
| handle       | Handle | The handle of the user to follow. | Yes        |

```c#
await agent.Follow(handle);
```

## Unfollowing

To unfollow a user call `agent.Unfollow()` with the [DID](../commonTerms.md#dids) of the user you want to unfollow.
`Unfollow()` looks up the follow record for you and deletes it.

`Unfollow(did)`

| Parameter    | Type | Description                      | Required   |
|--------------|------|----------------------------------|:----------:|
| did          | Did  | The DID of the user to unfollow. | Yes        |

```c#
await agent.Unfollow(did);
```

As with `Follow()` you can pass a [handle](../commonTerms.md#handles) instead of a DID.

`Unfollow(handle)`

| Parameter    | Type   | Description                         | Required   |
|--------------|--------|-------------------------------------|:----------:|
| handle       | Handle | The handle of the user to unfollow. | Yes        |

```c#
await agent.Unfollow(handle);
```

> [!NOTE]
> `Unfollow()` returns an `AtProtoHttpResult<DeleteResult>` whose `StatusCode` is `NotFound` if the handle could not be resolved,
> or if the current user is not following the specified user.

### Deleting a follow record directly

If you already have the follow record's [at:// uri](../commonTerms.md#uri) or its `StrongReference` you can delete it directly with
`agent.DeleteFollow()`, which saves the profile lookup `Unfollow()` performs.

`DeleteFollow(atUri)`

| Parameter    | Type  | Description                                | Required   |
|--------------|-------|--------------------------------------------|:----------:|
| atUri        | AtUri | The AtUri of the follow record to delete.  | Yes        |

```c#
await agent.DeleteFollow(atUri);
```

`DeleteFollow(strongReference)`

| Parameter       | Type            | Description                                         | Required   |
|-----------------|-----------------|-----------------------------------------------------|:----------:|
| strongReference | StrongReference | The StrongReference of the follow record to delete. | Yes        |

```c#
await agent.DeleteFollow(strongReference);
```

A `StrongReference` to the follow record is returned from the call to `Follow()`
or from [getting the user's profile](viewingProfiles.md).

If the `Viewer` property on a profile view is not null then the `Following` property on `Viewer` will be a `StrongReference` if the current user is following
the user whose profile you looked up. That `StrongReference`'s `Uri` property will be the at:// uri of the follow record.
