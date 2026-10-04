# ASP.NET progressive authorization

Progressive authorization starts with the permissions needed now and asks for more when a user enables another feature.
It requires a new OAuth authorization request, not a token refresh or logout followed by an unrelated login.
Read [Progressive authorization](progressiveAuthorization.md) and [OAuth permissions](oauthPermissions.md) first.
The [ASP.NET authentication guide](../asp.net.md) explains the authentication handler, default UI and metadata middleware.

`Samples.AspNetAuthentication` deliberately requests `BlueskyOAuthPermissionSets.FullApp` at initial login and saves
profiles directly. It does not implement the workflow below: this guide describes how to add progressive authorization
to your own application without making the basic authentication sample more complicated.

## Choose where consent fits your application

| Approach | User experience | Application responsibilities |
| --- | --- | --- |
| Enable a feature before editing | An **Enable profile editing** button requests consent, then opens the editor. | Bind consent to the current account/session and validate credentials before enabling the feature. No submitted edit needs to survive OAuth. |
| Save, authorize, then complete | **Save** retains the edit, requests consent if needed, then returns to a confirmation form. | Store an expiring draft, handle concurrent requests and consume completion authorization exactly once. |
| Keep edits in the browser | The editor retains unsaved values while a separate window requests consent. | Handle popup blocking, cancellation, window lifetime and cross-window communication. Revalidate every submitted value on the server; never send credentials to JavaScript. |

Enabling a feature before editing is the simplest option when the extra step fits the product.
The remainder gives a concrete **save, authorize, then complete** pattern for applications that need to preserve an
already submitted operation. Use an explicit **Complete save** button after consent; automatic form submission is an
optional enhancement, not a reason to perform a write in the OAuth callback GET.

## Run the progressive authentication sample

[`Samples.AspNetProgressiveAuthentication`](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.AspNetProgressiveAuthentication)
is a separate Razor Pages application based on `Samples.AspNetAuthentication`. It implements the save, authorize,
then complete workflow without adding that complexity to the basic sample.

```powershell
dotnet run --project samples\Samples.AspNetProgressiveAuthentication --launch-profile http
```

Browse to `http://127.0.0.1:5254/`, sign in, and open **Manage Profile**. Initial consent requests timeline, private
preferences and user-profile reads, not profile writes. Edit the fields and click **Update**: when either required
write permission is missing, the sample retains the edit and redirects for same-account consent. After consent,
JavaScript submits the antiforgery-protected completion form; without JavaScript, click **Complete save**.
A confirmed save displays **Your profile was saved.** Subsequent edits can save directly while both write grants remain.
Use a test account: completing this workflow changes its public profile.

If consent is denied, canceled or fails, return to **Manage Profile** to recover edits for up to ten minutes.
A new submission replaces the session's previous draft. **Discard pending edits and reload profile** loads the latest
record after a conflict. If a write cannot be confirmed, inspect the profile before retrying.
The encrypted in-memory store can lose drafts on expiry, capacity eviction or restart; it is not production storage.
The application has a separate data-protection purpose and authentication/correlation cookie names from the basic
sample, but its default stores still share credentials between sessions for the same DID within this application.

For hosted deployment, replace the example HTTPS client ID and callback in `appsettings.json`, configure allowed
hosts and [production storage/key management](../productionConfiguration.md), and publish the configured metadata URL.
Localhost configuration fixes a client ID declaring the maximum while each authorization request uses its own subset.
No live consent is exercised by the automated tests.

| Implementation | What to read |
| --- | --- |
| [Program.cs](https://github.com/blowdart/idunno.Bluesky/blob/main/samples/Samples.AspNetProgressiveAuthentication/Program.cs) | Authentication events invalidate old drafts; DI registers the store, OAuth adapter and maximum metadata scopes. |
| [ProfilePermissions.cs](https://github.com/blowdart/idunno.Bluesky/blob/main/samples/Samples.AspNetProgressiveAuthentication/ProfilePermissions.cs) | Exact read/write scopes, fixed localhost metadata declaration and retained effective permissions. |
| [ProfileEditStore.cs](https://github.com/blowdart/idunno.Bluesky/blob/main/samples/Samples.AspNetProgressiveAuthentication/ProfileEditStore.cs) | Encrypted payloads, owner binding, absolute expiry and atomic single-use transitions. |
| [ProfileOAuthClient.cs](https://github.com/blowdart/idunno.Bluesky/blob/main/samples/Samples.AspNetProgressiveAuthentication/ProfileOAuthClient.cs) | Explicit per-request scopes, correlation storage and isolated expected-DID validation. |
| [Callback.cshtml.cs](https://github.com/blowdart/idunno.Bluesky/blob/main/samples/Samples.AspNetProgressiveAuthentication/Areas/Bluesky/Pages/Callback.cshtml.cs) | Normal login versus progressive consent, credential installation and fixed return route without profile writes. |
| [Manage/Index.cshtml.cs](https://github.com/blowdart/idunno.Bluesky/blob/main/samples/Samples.AspNetProgressiveAuthentication/Pages/Manage/Index.cshtml.cs) | Draft recovery, protected completion/discard handlers and conditional profile saves. |
| [Manage/Index.cshtml](https://github.com/blowdart/idunno.Bluesky/blob/main/samples/Samples.AspNetProgressiveAuthentication/Pages/Manage/Index.cshtml) | Antiforgery-enabled editor, completion and discard forms. |
| [ProfileTests.cs](https://github.com/blowdart/idunno.Bluesky/blob/main/test/Samples.AspNetProgressiveAuthentication.Test/ProfileTests.cs) | Mock consent/PDS coverage for ownership, grants, antiforgery, replay and recoverable failures. |

## Plan the initial and maximum permissions

Derive scopes from the actual API calls, including claims transformation and other framework services.
For an application that reads a timeline, its subscribed labelers and user details, the initial scope list could be:

```c#
string[] initialScopes =
[
    "atproto",
    "rpc:app.bsky.feed.getTimeline?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview",
    "rpc:app.bsky.actor.getPreferences?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview",
    "rpc:app.bsky.actor.getProfile?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview"
];
string[] profileWriteScopes =
[
    "repo:app.bsky.actor.profile?action=create",
    "repo:app.bsky.actor.profile?action=update"
];
```

The authoritative lexicons are
[`getTimeline`](https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/feed/getTimeline.json),
[`getPreferences`](https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/actor/getPreferences.json) and
[`getProfile`](https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/actor/getProfile.json).
The preferences permission grants access to private preferences generally, not just subscribed labelers.
Omit it if your application does not call that endpoint. Claims transformation needs the app-view `getProfile`
permission even when your pages do not explicitly request user details.

Reading the public profile repository record through
[`com.atproto.repo.getRecord`](https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/repo/getRecord.json)
does not require another OAuth permission. Writing through
[`putRecord`](https://github.com/bluesky-social/atproto/blob/main/packages/pds/src/api/com/atproto/repo/putRecord.ts)
requires **both create and update**, even when updating an existing `self` record with `swapRecord`.
Update-only consent can therefore fail with `ScopeMissingError` naming the create scope.

Use typed permission sets where their permissions match your feature. For example,
`BlueskyOAuthPermissionSets.ManageProfile` is convenient but its
[lexicon](https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/authManageProfile.json)
also permits profile deletion and status/notification declaration changes. Use the two granular scopes above if
those additional operations are not needed; do not substitute `FullApp` for a profile-only upgrade.
See the [permission specification](https://atproto.com/specs/permission).

Configure `OAuthOptions.Scopes` with the initial scopes and leave `PermissionSets` empty unless they are needed initially.
The scope argument of `BuildOAuth2LoginUri` overrides **both** configured scopes and permission sets.
For each upgrade, build an ordinal-deduplicated union of required initial scopes, effective prior permissions you intend
to retain, and the new feature scopes. Validate retained scopes against your application's allowed maximum.
Do not treat the scopes requested in a previous flow as proof of what the server granted. Explicit access-token scope
claims can inform your feature checks; opaque `ref:` grants cannot be expanded locally, and recorded requested scopes
are only a hint. The PDS remains authoritative.

### Advertise the maximum without requesting it initially

For a hosted client, register metadata publication and add the later scopes:

```c#
builder.Services.AddBlueskyOAuthClientMetadata(options =>
{
    foreach (string scope in profileWriteScopes)
    {
        options.AdditionalScopes.Add(scope);
    }
});
```

Publish it with `UseBlueskyOAuthClientMetadata()` as described in the
[metadata guide](../asp.net.md#publishing-oauth-client-metadata).
`AdditionalScopes` affects the advertised maximum, not initial login.

For localhost, set one fixed client ID containing the URL-encoded maximum in its `scope` query parameter, plus the
loopback `redirect_uri`. Use that exact ID for initial login and every upgrade. A bare localhost ID is expanded from
the current request's scopes, so it is unsuitable when different requests use different subsets.
Use `BlueskySignInManager.CreateReturnUri()` to preserve the configured callback path and current development browser
port; see [Local OAuth development](localOAuthDevelopment.md).

## Persist a pending operation

On the authenticated, antiforgery-protected **Save** POST, validate the edited fields and original record CID.
Verify that the injected agent's DID matches the authenticated ticket. If the required grants are already available,
perform the normal conditional save. Otherwise, persist a pending operation before preparing the consent redirect.

Store a cryptographically random operation ID, original DID, editing-session ID, validated fields, original CID,
creation time, absolute expiry and workflow status. A ten-minute lifetime is a reasonable starting policy; transitions
must not extend it. Create the editing-session ID at normal login, keep it in protected `AuthenticationProperties`,
preserve it during upgrades, and invalidate pending operations on logout or a new login, including a new login with
the same DID. DID binding alone does not bind an operation to a browser session.

Encrypt sensitive fields at rest, for example with ASP.NET Core Data Protection. Use durable, shared storage with
atomic conditional transitions for multiple instances; `IDistributedCache` alone does not supply compare-and-swap.
A database row with a concurrency token is one option. Sharing a data-protection key ring does not share drafts.
Store only opaque references in correlation cookies; keep editor values and credentials out of callback URLs and
browser-readable storage.
Decide whether a newer submission replaces an older draft or each tab has its own operation, and make that policy visible.

| State | Allowed transition | Meaning |
| --- | --- | --- |
| Awaiting consent | Processing consent | A matching callback claims the operation once. |
| Processing consent | Ready or Failed | Validated credentials were persisted, or consent failed. |
| Ready | Saving | An owner-bound completion POST claims the operation once. |
| Saving | Completed or Failed | The write succeeded, was rejected, or has an uncertain outcome. |

Every transition checks the operation ID, DID, editing-session ID and original expiry atomically.
Terminal or expired operations cannot return to Ready. Retain failed payloads for recovery until expiry, but a user's
explicit retry starts a new attempt rather than reusing a consumed completion token. Recover abandoned in-flight
states as failures after an application-defined timeout; do not retry their writes automatically.
The sample uses `Failed` as a recovery state while a single-use save is in flight, removes the draft after confirmed
success, and retains its uncertain-outcome explanation otherwise. Consent can likewise remain in progress until the
user explicitly retries or the original draft expires; there is no automatic write retry.

## Start consent with the existing SDK primitives

`BlueskySignInManager.CreateRedirectUri()` uses the configured scopes and does not accept a per-request scope list.
For an upgrade, use `BuildOAuth2LoginUri` and then the manager's correlation storage.
The following is the body of an application's consent-preparation method. `manager`, `clients`, `handle`,
`expandedScopes`, `operationId` and `cancellationToken` come from the authenticated request and the server-side operation:

```c#
using var isolatedAgent = new BlueskyAgent(
    httpClientFactory: clients, options: manager.BlueskyAgentOptions);
OAuthClient oauth = isolatedAgent.CreateOAuthClient();
Uri callback = manager.CreateReturnUri();
Uri redirect = await isolatedAgent.BuildOAuth2LoginUri(
    oauth,
    handle,
    scopes: expandedScopes,
    returnUri: callback,
    stateExtraProperties: new() { ["pendingOperation"] = operationId },
    cancellationToken: cancellationToken);

OAuthLoginState state = oauth.State
    ?? throw new OAuthException("OAuth state was not prepared.");
await manager.SaveStateAndCreateCorrelationCookie(
    state, markCookieAsSecure: callback.Scheme == "https");

return Redirect(redirect.ToString());
```

Use `idunno.AtProto.Authentication`, `idunno.Bluesky`, and `idunno.Bluesky.AspNet.Authentication` for these types.
The handle is a login hint, not the account-binding check. Keep the original DID in the server-side operation.
If discovery or redirect preparation fails, log the failure and redisplay the retained edit with a clear explanation.
The operation reference belongs in correlated OAuth state, not in an untrusted return URL.

## Validate the callback before replacing credentials

Override the default UI callback at `Areas/Bluesky/Pages/Callback.cshtml` and its page model, or use an application-owned
callback route configured in `ReturnUri` and metadata. The callback must distinguish normal login from upgrades using
saved state, not a query flag. Preserve normal login behavior for callbacks without a pending-operation reference.

For an upgrade:

1. Call `manager.LoadState()` with no correlation identifier from the request. It validates the state-specific protected
   browser cookie and consumes server-side OAuth state. Reject missing, expired or replayed state.
2. Read the operation reference from `state.ExtraProperties`. Authenticate the existing ticket and atomically claim
   Awaiting consent for the same DID and editing-session ID. Reject a changed login, missing ticket or expired draft.
3. Create an **isolated agent**: a separate `BlueskyAgent` with the application's configured options and HTTP client
   factory, but without the request's credential-persistence hook. Restore the OAuth client on this agent and validate
   the authorization response with the expected DID. This keeps the new credentials separate from the existing login
   until the application has checked the granted permissions and rechecked pending-operation ownership and expiry:

```c#
OAuthClient restored = isolatedAgent.CreateOAuthClient(state);
bool authorized = await isolatedAgent.ProcessOAuth2LoginResponse(
    restored, callbackData, expectedDid, cancellationToken);
```

Here `callbackData` is the OAuth response query without its leading `?`, and `expectedDid` comes from the claimed
operation. Check `authorized`, require DPoP credentials, verify required grants where they can be evaluated, and
recheck operation ownership/expiry after the exchange. The expected-DID overload rejects a mismatch before installing
credentials on the isolated agent.

> [!WARNING]
> Do not use the request's factory agent for this exchange. Its `CredentialsUpdatedAsync` hook can persist new
> credentials before your application finishes checking granted permissions and pending-operation ownership/expiry.
> Use an isolated agent without that hook so failed validation leaves the existing login credentials untouched.
> Only persist the validated credentials through `SignInAsync` after all application checks pass.

Only after validation, sign in through the existing handler, preserving the old ticket's properties and lifetime:

```c#
await HttpContext.SignInAsync(
    manager.AuthenticationScheme,
    new ClaimsPrincipal(IIdentityStore.BuildClaimsIdentity(
        credentials, manager.AuthenticationScheme)),
    existingTicket.Properties);
```

The snippet assumes validated `credentials` and a successful `existingTicket` from `AuthenticateAsync`; use
`Microsoft.AspNetCore.Authentication` and `System.Security.Claims`.
The handler persists credentials through the configured identity store. Mark the operation Ready **after** sign-in
succeeds, then redirect to a fixed editor route. Do not accept callback query-string return routes.
If persistence succeeds but the operation expires, explain that authorization succeeded but nothing was saved.
Credential persistence and the draft transition are not one transaction: fail safely, never claim a completed write.
The default identity store is keyed by DID, so other sessions for that DID can observe credential updates; use
session-scoped storage if your application needs credential isolation as well as operation isolation.

Your implementation should:

* Handle denial, cancellation, `OAuthException`, HTTP failures and interrupted exchanges explicitly.
* Keep the old login when validation fails, retain recoverable edits and display **not saved**.
* Offer recovery or an explicit retry when the user reopens the editor after closing the consent page without returning.
* Log diagnostics without tokens or draft contents.

See [Production configuration](../productionConfiguration.md) for storage and key-ring requirements.

## Complete with a protected POST

The editor GET displays the retained fields and a **Complete save** form containing only the opaque operation ID.
Razor Pages form tag helpers generate antiforgery tokens; controllers must enforce antiforgery validation explicitly.
The completion POST rechecks the authenticated owner, atomically claims Ready as Saving, and reads authoritative values
from server-side storage. Ignore edited values, DID, CID and return routes supplied with this completion form.
A duplicate POST, another browser session or an older form must not perform another write.

Re-read the profile, compare its CID to the draft's original CID, merge only the edited fields and call
`UpdateProfile(profile, cid: originalCid, ...)`. The conditional `swapRecord` closes the race between reading and writing.
Do not overwrite unrelated profile fields or automatically replace the original CID with the newest version.
On conflict, preserve edits and offer a deliberate reload/reconciliation action.

Remove only the matching operation after a confirmed successful write, then display success.
Keep a recovery draft for rejected or uncertain writes. A network timeout can occur after the server commits:
tell the user to inspect the current profile before explicitly retrying.
Never automatically repeat an ambiguous write or start another consent loop from a failed completion.
Only a recognized `ScopeMissingError` or `InsufficientScope` HTTP 403 can justify reconsidering permissions;
generic 403s, expired tokens, rate limits and network errors are not scope upgrades.
Make discard/retry actions owner-bound and antiforgery-protected too.

## Verify the workflow

Use the following suggested test plan to check both the successful consent flow and its failure paths. Most cases
can run against mock authorization and resource servers, as in the sample's `ProfileTests.cs`. Follow those with a
manual browser test using a test account to confirm real consent, cancellation and server permission enforcement.
For each case, check the resulting credentials, pending-operation state, number of profile writes and user-facing
message, rather than relying on the redirect or HTTP status alone.

* **Initial scopes and maximum metadata:** Verify independently that initial login requests only the intended reads,
  while hosted metadata and the fixed localhost client ID advertise all potentially requested scopes. This catches
  accidental write access at login and scope mismatches that cause later authorization requests to fail.
* **Missing permissions and successful consent:** Start without one or both profile-write grants, submit an edit,
  authorize with the same account, and complete the save. Confirm that consent retains required reads, the callback
  performs no write, and completion saves the retained values exactly once. Also check that an already-authorized
  session saves directly, avoiding unnecessary consent.
* **Denied, canceled or insufficient consent:** Deny consent, close the consent page, return without all required
  grants, and simulate callback errors. Confirm that the existing login remains usable and edits can be recovered
  without a success message. These paths must not silently discard user input or authorize an incomplete grant.
* **Account and session changes:** Switch accounts, sign out and back in with the same DID, and attempt completion
  from another browser session. Each attempt must be rejected because an operation belongs to the original editing
  session, not merely to whoever currently presents its identifier or uses the same account.
* **Expiry and replay:** Expire drafts before the callback, during token exchange and before completion; then repeat
  callbacks and completion POSTs. Confirm that expired or consumed operations cannot install upgraded credentials
  or trigger another write. This checks that lifetime and single-use rules hold across network delays.
* **Tampered identifiers and return routes:** Alter operation identifiers, submit extra edited values with completion,
  and supply external or unexpected return URLs. The application must use the owner-bound server-side draft and its
  fixed completion route, preventing unauthorized saves and redirect manipulation.
* **Antiforgery protection:** Submit save, completion, retry and discard requests without a valid antiforgery token.
  Each mutation must be rejected before it changes a draft or profile, preventing another site from initiating
  these actions through the user's authenticated browser.
* **Conflicts and uncertain writes:** Change the profile after loading the editor, simulate a rejected write, and
  interrupt the response after the server may have committed. Verify CID conflict protection, recoverable edits
  and clear failure or uncertainty messages without automatic retries. This prevents overwriting newer changes,
  repeating an ambiguous write or falsely reporting success.
* **Parallel tabs and application restart:** Submit competing edits and complete or discard an older operation while
  a newer one exists, then restart the application during consent or saving. Verify the documented replacement
  policy and your store's persistence guarantees, ensuring stale requests cannot remove newer drafts and unavailable
  edits are explained rather than treated as completed saves.
