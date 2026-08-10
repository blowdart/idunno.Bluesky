# Muting users

Muting a user hides their posts from your feeds. Mutes are *private*. Muting a user is as easy asfollowing a user.

`Mute(identifier)`

| Parameter    | Type | Description                    | Required   |
|--------------|------|--------------------------------|:----------:|
| actor        | AtIdentifier | The Handle or DID of the user to mute . | Yes |

You can also narrow the scope of a mute state,
by using the overload with the `onlyReposts` and `onlyQuotePosts` parameters.

```c#
await agent.Mute(identifier, bool, bool);
```

`Mute(identifier)`

| Parameter    | Type | Description                    | Required   |
|--------------|------|--------------------------------|:----------:|
| actor        | AtIdentifier | The Handle or DID of the user to mute . | Yes |
| onlyReposts  | bool | If true, only mutes reposts from the user. | Yes |
| onlyQuotePosts | bool | If true, only mutes quote posts from the user. | Yes |

```c#
await agent.Mute(identifier, onlyReposts : true, onlyQuotePosts: false);
```

## Unmuting a user

`Unmute(identifier)`

| Parameter    | Type | Description                      | Required   |
|--------------|------|----------------------------------|:----------:|
| actor        | AtIdentifier  | The Handle or DID of the user to un-mute . | Yes        |

```c#
await agent.Unmute(did);
```
