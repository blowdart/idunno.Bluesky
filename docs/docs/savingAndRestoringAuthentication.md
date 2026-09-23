# Saving and Restoring Sessions

If you're writing an app you probably don't want to ask your user to authenticate every time your app starts, or to store their login
credentials somewhere on disk. The Agent class provides a way to restore, or authenticate from a stored refresh token
and the URI of the server that issued it.

Events and an awaitable callback are provided so you can react to background token refresh operations and store the updated refresh token for the next time
your application starts.

## <a name="authenticationEvents">Authentication Events</a>

An agent provides four authentication related events that you can subscribe to:

1. `Authenticated`
1. `CredentialsUpdated`
1. `TokenRefreshFailed`
1. `Unauthenticated`

There is also a `CredentialsUpdatedAsync` callback, an awaitable counterpart to the `CredentialsUpdated` event. It is the callback you should
use to persist credentials, for the reasons described [below](#credentialsUpdatedAsync).

You can subscribe to these events by adding a handler to the event. For example:

```c#
var agent = new BlueskyAgent();

agent.Authenticated += (sender, args) =>
{
    Console.WriteLine("Authenticated");
    // Now persist the refresh token and any DPoPKey and DPopNonce, along with the service URI and DID
    // it belongs to somewhere secure.
    //
    // Optionally you can also persist the handle as an indexer to support multiple accounts within the same application.
};
```

The `Authenticated` event is raised when a login is successful and a new session is created. The `CredentialsUpdated` event is raised, and the
`CredentialsUpdatedAsync` callback invoked, when the background token refresh occurs or you call `RefreshSession()`. A `TokenRefreshFailed` event is
raised when the background refresh fails, or your manual call to `RefreshCredentials()` fails. `Unauthenticated` is raised when you call `Logout()`.

> [!IMPORTANT]
> When you handle `Authenticated`, and when credentials are updated,
> it is suggested you store the refresh token, any DPoPKey and DPopNonce, the URI that issued it and the DID it belongs to securely,
> in whatever way is appropriate for your platform (for example the
> [Windows Credential Store](https://learn.microsoft.com/en-us/samples/microsoft/windows-universal-samples/passwordvault/),
> or the [Mac/iOS keychain](https://developer.apple.com/documentation/security/keychain-services)).
>
> If your application supports multiple accounts you should also store the handle that was used to create the session.

Storing the access token is optional, it is meant to be short lived and may no longer be valid when your application restarts.

## <a name="credentialsUpdatedAsync">Persisting updated credentials</a>

Use the `CredentialsUpdatedAsync` callback to store your updated values. Unlike the `CredentialsUpdated` event it can be awaited, so the agent waits
for your store to be written before it continues, and any exception it throws surfaces to the caller which triggered the refresh.

```c#
agent.CredentialsUpdatedAsync = async (args, cancellationToken) =>
{
    await credentialStore.SaveAsync(args.AccessCredentials, cancellationToken);
};
```

`CredentialsUpdatedAsync` is a property rather than an event, so assigning to it replaces any callback already set, and only one can be set at a time.
If your handler has nothing to await, return `Task.CompletedTask` from it.

> [!WARNING]
> Prefer `CredentialsUpdatedAsync` over the `CredentialsUpdated` event whenever storing credentials is asynchronous.
> Events cannot be awaited, so an `async void` handler, or one which discards the task it starts, runs unobserved. The agent will
> carry on without waiting for your store to be written, and any exception it throws is silently swallowed. As AT Proto refresh
> tokens are single use, a persist which fails or does not complete leaves you holding a stale refresh token and the user is
> signed out on their next request.

Finally in the handlers for `TokenRefreshFailed` and `Unauthenticated` events you should remove any stored values you have for the DID and the service.

## <a name="restoringSessions">Restoring or recreating a session</a>

To restore a session from a refresh token (or an access token if you still have a valid one) first create an instance of `AtProtoCredential`
using `AtProtoCredential.Create()` with the information you stored securely then pass it to `RefreshCredentials()`.

```c#

AtProtoCredential restoredCredential = AtProtoCredential.Create(
    service: persistedLoginState.Service,
    authenticationType: persistedLoginState.AuthenticationType,
    refreshToken: persistedLoginState.RefreshToken,
    dPoPProofKey: persistedLoginState.DPoPProofKey,
    dPoPNonce: persistedLoginState.DPoPNonce);

bool resumeResult = await agent.RefreshCredentials(
    credential: restoredCredential);

if (!resumeResult.Succeeded)
{
    Console.WriteLine($"Restore failed.");
}
```

If the resume is successful the agent will be populated with the current access and refresh tokens and the session will be valid for authenticated
calls. If the tokens were refreshed during restoration (which happens if you have a refresh token, but no access token, or the access token is expired)
the `CredentialsUpdatedAsync` callback will be invoked, where you should store the newly refreshed credentials.
