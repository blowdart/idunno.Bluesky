# Muting users

Muting a user hides their posts from your feeds. Mutes are *private*. Muting a user is as easy following a user.

`Mute(did)`

| Parameter    | Type | Description                    | Required   |
|--------------|------|--------------------------------|:----------:|
| actor        | Did  | The DID of the user to mute .  | Yes        |

```c#
await agent.Mute(did);
```

> [!TIP]
> If you only know the [handle](../commonTerms.md#handles) of a user you can get their DID with `agent.ResolveHandle()`.

## Unmuting a user

`Unmute(did)`

| Parameter    | Type | Description                      | Required   |
|--------------|------|----------------------------------|:----------:|
| actor        | Did  | The DID of the user to un-mute . | Yes        |

```c#
await agent.Unmute(did);
```
