# Muting users

Muting a user hides their posts from your feeds. Mutes are *private*. Muting a user is as easy as following a user.

`Mute(actor)`

| Parameter    | Type | Description                    | Required   |
|--------------|------|--------------------------------|:----------:|
| actor        | AtIdentifier | The Handle or DID of the user to mute. | Yes |

You can also narrow the scope of a mute, by using the overload with the `onlyReposts` and `onlyQuotePosts` parameters.

```c#
await agent.Mute(actor);
```

`Mute(actor, onlyReposts, onlyQuotePosts)`

| Parameter    | Type | Description                    | Required   |
|--------------|------|--------------------------------|:----------:|
| actor        | AtIdentifier | The Handle or DID of the user to mute. | Yes |
| onlyReposts  | bool? | If true, only mutes reposts from the user. | Yes |
| onlyQuotePosts | bool? | If true, only mutes quote posts from the user. | Yes |

```c#
await agent.Mute(actor, onlyReposts : true, onlyQuotePosts: null);
```

When any 'only' scope is set, just the scoped content is muted; when none are set, the account is fully muted. Repeat calls replace the stored scope rather than adding to it.

## Unmuting a user

`Unmute(actor)`

| Parameter    | Type | Description                      | Required   |
|--------------|------|----------------------------------|:----------:|
| actor        | AtIdentifier  | The Handle or DID of the user to unmute. | Yes        |

```c#
await agent.Unmute(actor);
```

>[!TIP]
> There is no way to unmute a user with a scope, if you muted a user with `onlyReposts` or `onlyQuotePosts`, you will need to call `Unmute` to unmute the user entirely.
