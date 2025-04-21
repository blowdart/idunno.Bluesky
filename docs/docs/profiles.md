# <a name="profileViewing">Viewing a user's profile</a>

## <a name="gettingAProfile">Getting a profile</a>

To retrieve a user's profile use `agent.GetProfile()`, with either a `Handle` or `Did`.

```c#
var profileViewResult = await agent.GetProfile(new Handle("blowdart.me"), cancellationToken: cancellationToken);
```

A user's `ProfileView` includes their `Did`, `Handle`, display name, description, avatar etc. It also includes their follower, following and posts count, information
on the number of lists, starter packs and feed generators they have created, as well who they will accept direct messages from.

The profile view also includes a `Viewer` property which describes the relationship between the user that requested the profile and the user the profile refers to,
including properties such as `Muted`, when the user requesting the profile has muted the user the profile refers to, as well as `BlockedBy`, `Following`, `FollowedBy`
and other properties that will effect how you should render the profile, or the item in a view that also contains the author profile, to the requesting user.

Feeds, timelines etc. will also contain a `ProfileViewBasic`, `ProfileView` or `ProfileViewDetailed`, depending on the view or API definition.

## <a name="profileVerification">Profile labels</a>

Profile views can include labels from labelers. Please see [Labels](labels.md) for more information.

## <a name="profileVerification">Profile verification</a>

Bluesky supports a composible verification system where various organications will verify accounts, for example the New York Times may verify their reporters. The
`Verification` property holds the verification status for a user, and a list of the verifiers who have verified it. The `VerifiedStatus` can be valid, invalid, none or unknown,
you can use this to decide if you want to display an indicator of the status to your users.

