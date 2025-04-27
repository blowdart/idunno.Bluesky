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

> [!TIP]
> If you only know the [handle](../commonTerms.md#handles) of a user you can get their DID with `agent.ResolveHandle()`.

## Unfollowing

To unfollow a user call `agent.DeleteFollow()` with the [DID](../commonTerms.md#dids) of the user you want to follow.

`DeleteFollow(did)`

| Parameter    | Type | Description                      | Required   |
|--------------|------|----------------------------------|:----------:|
| did          | Did  | The DID of the user to unfollow. | Yes        |

```c#
await agent.DeleteFollow(did);
```

If you have the `at://` [uri](../commonTerms.md#uri) of the follow record for a `DID` you can also pass that to `DeleteFollow()`.

`DeleteFollow(atUri)`

| Parameter    | Type  | Description                     | Required   |
|--------------|-------|---------------------------------|:----------:|
| atUri        | AtUri | The AtUri of the follow record. | Yes        |

```c#
await agent.DeleteFollow(atUri);
```

A `StrongReference` to the follow record is returned from the call to `Follow()`
or from [getting the user's profile](viewingProfiles.md).

If the `Viewer` property on a profile view is not null then the `Following` property on `Viewer` will be a `StrongReference` if the current is following
the user whose profile you looked up. That `StrongReference`'s `Uri` property will be the at:// uri of the follow record.
