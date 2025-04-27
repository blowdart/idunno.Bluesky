# Blocking users

Blocking a user prevents interaction and hides the user from a client . Blocked accounts will not be able to like, reply, mention, or follow you.
Their posts, replies, and profile in search will also be hidden from you. Blocks are *public*.

Blocking a user works just like muting.

`Block(did)`

| Parameter    | Type | Description                    | Required   |
|--------------|------|--------------------------------|:----------:|
| did          | Did  | The DID of the user to block . | Yes        |

```c#
await agent.Block(did);
```

> [!TIP]
> If you only know the [handle](../commonTerms.md#handles) of a user you can get their DID with `agent.ResolveHandle()`.

## Unblocking a user

Unblock is also simliar to muting.

`Unblock(did)`

| Parameter    | Type | Description                      | Required   |
|--------------|------|----------------------------------|:----------:|
| did          | Did  | The DID of the user to unblock . | Yes        |

```c#
await agent.Unblock(did);
```
