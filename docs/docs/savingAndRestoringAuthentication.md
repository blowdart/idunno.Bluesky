# Saving and Restoring Sessions

If you're writing an app you probably don't want to ask your user to authenticate every time your app starts, or to store their login
credentials somewhere on disk. The Agent class provides a way to restore, or create a new authenticated session from a stored refresh token
and the URI of the server that issued it. Events are provided so you can react to background token refresh operations and store the updated refresh
token for the next time your application app starts.

## <a name="authenticationEvents">Authentication Events</a>

An agent provides four authenticated related events that you can subscribe to:

1. `SessionCreated`
1. `SessionRefreshed`
1. `SessionRefreshFailed`
1. `SessionEnded`

You can subscribe to these events by adding a handler to the event. For example:

```c#
var agent = new BlueskyAgent()
{
    agent.SessionCreated += (sender, args) =>
    {
        Console.WriteLine("Session created");
        // Now persist the refresh token along with the service URI and DID it belongs to somewhere secure.
        // Optionally you can also persist the handle, in combination with the refresh token, service URI and DID
        // to support multiple accounts within the same application.
    };
}
```

The `SessionCreated` is raised when a login is successful and a new session is created.The `SessionRefreshed` event is raised when the background
token refresh occurs or you call `RefreshSession()`. `SessionRefreshFailed` event is raised when the background refresh fails, or your manual call to
`RefreshSession()` fails. `SessionEnded` is raised when you call `Logout()`.

> [!IMPORTANT]
> In your handler for `SessionCreated` it is suggested you store the refresh token, the URI that issued it and the DID it belongs to securely,
> in whatever way is appropriate for your platform (for example the
[Windows Credential Store](https://learn.microsoft.com/en-us/samples/microsoft/windows-universal-samples/passwordvault/),
or the [Mac/iOS keychain](https://developer.apple.com/documentation/security/keychain-services)). If your application supports
> multiple accounts you should also store the handle that was used to create the session.

Storing the access token is optional, it is meant to be short lived and may no longer be valid when your application restarts.

You should update these your stored values in your handler for `SessionRefreshed`.

Finally in the handlers for `SessionRefreshFailed` and `SessionEnded` events you should remove any stored values you have for the DID and the service.
The DID and service may not be available if the session refresh fails.

## <a name="restoringSessions">Restoring or recreating a session</a>

To restore a session from a refresh token (or an access token if you still have a valid one call `ResumeSession()`. This takes the user's DID,
the access token (or null if you don't have one), the refresh token, and the URI of the service that issued the tokens.

```c#

bool resumeResult = await agent.ResumeSession(
    persistedLoginState.Did,
    persistedLoginState.AccessToken,
    persistedLoginState.RefreshToken,
    persistedLoginState.Service,
    cancellationToken);

    if (!resumeResult.Succeeded)
    {
        Console.WriteLine($"Restore failed.");
    }
```

If the resume is successful the agent will be populated with the current access and refresh tokens and the session will be valid for authenticated
calls.If the tokens were refreshed during restoration (which happens if you have a refresh token, but no access token, or the access token is expired)
the `SessionRefreshed` event will be raised.

Don't forget to save the new refresh token in wherever you are securely storing them.
