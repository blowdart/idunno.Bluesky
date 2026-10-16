# OAuth permissions

For the basic login flow, see [Connecting to Bluesky](../connecting.md#oauth).

[Bluesky's OAuth implementation](https://docs.bsky.app/docs/advanced-guides/oauth-client) supports
[granular permissions and published permission sets](https://atproto.com/guides/permission-requests).
The `atproto` identity scope is the minimum scope needed for AT Protocol OAuth and must always be requested.
On its own, it supports a simple "Login with Bluesky" or "Login with your Atmosphere Account" function:
after the user authorizes your application, the authenticated session identifies the account by its DID.
Your application can use that stable identifier to sign the user in without requesting permission to post,
modify their profile, read private preferences, or access their direct messages.

For an application that only needs account authentication, the default `OAuthOptions.Scopes` value of `["atproto"]`
is sufficient; leave `PermissionSets` empty. Request additional permissions only when the application needs to act
on the user's behalf beyond identifying their account.

For access to Bluesky features, prefer published permission sets
or granular scopes that cover only the features your application needs. The legacy transition scopes remain supported
for compatibility, but are not recommended for new applications:

* `transition:generic`: which gives your application broad access to records across applications, except for direct message access.
* `transition:chat.bsky`: which gives your application direct message access; prefer `BlueskyOAuthPermissionSets.FullChatClient`.

## Typed permission sets

Use `OAuthOptions.PermissionSets` to request published sets alongside raw `Scopes`. The default raw scope is `atproto`,
which is still required; permission sets do not include it. For example, a posting application can request:

```c#
using idunno.AtProto.Authentication;
using idunno.Bluesky.Authentication;

OAuthOptions options = new("https://example.com/oauth-client-metadata.json")
{
    ReturnUri = new Uri("https://example.com/oauth/callback"),
    Scopes = ["atproto", "blob:image/*"],
    PermissionSets = [BlueskyOAuthPermissionSets.CreatePosts]
};
```

Assign these options to `BlueskyAgentOptions.OAuthOptions` (or `AtProtoAgentOptions.OAuthOptions`).
`BlueskyOAuthPermissionSets` includes `CreatePosts`, `DeleteContent`, `FullApp`, `ManageFeedDeclarations`,
`ManageLabelerService`, `ManageModeration`, `ManageNotifications`, `ManageProfile`, `ViewAll`, and `FullChatClient`.
Sets requiring inherited RPC audiences use the default Bluesky app view or chat audience. `FullApp` does not
include chat access or blob uploads. Blob, account, and identity permissions must be requested separately.

For a full Bluesky social client, use `FullApp`; for a smaller application, select a narrower set such as
`CreatePosts` or `ManageProfile`. `FullApp` is not a drop-in replacement for `transition:generic`: it covers
Bluesky features rather than records across all applications. Add an appropriate blob scope, such as
`blob:image/*`, only when your application needs to upload media.

For custom AT Protocol lexicons or another service audience, construct a permission-set reference:

There are no published `com.atproto.*` permission sets in the upstream lexicons. The `com.example.authBasic`
name below is a placeholder: replace it with a permission-set lexicon you have actually published.

```c#
options.PermissionSets =
[
    new OAuthPermissionSet("com.example.authBasic", "did:web:api.example.com#appview")
];
```

The authorization server resolves the `include:` reference to the published lexicon. `options.GetRequestedScopes()` returns
the combined, deduplicated scope strings; publish the same scopes in your client metadata document.
No metadata generation or serving is performed by these APIs.

Raw scopes remain supported unchanged. An explicit `scopes` argument on an OAuth request overrides both configured
raw scopes and permission sets. The effective client ID and requested scopes are saved with the login state and
issued credentials, so callback processing and subsequent refreshes retain the original request even if options change.
Older saved state and credentials without this context continue to use the configured options.
Typed sets also convert to strings when placed in a raw scope collection:

```c#
string[] scopes = ["atproto", "blob:image/*", BlueskyOAuthPermissionSets.CreatePosts];
```

For requesting additional permissions later, see [Progressive authorization](progressiveAuthorization.md).
