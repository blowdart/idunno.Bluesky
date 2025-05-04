# <a name="profileEditing">Changing a user's profile</a>

A user's profile consists of the user's title, a description, a profile picture and a banner picture, all of which can be left empty, as
well as other settings like a pinned post that is shown when someone views the account's profile page, a setting to discourage applications
for displaying the profile and posts to unauthenticated users and self labels,
which can be any of the configuration [global label values](https://docs.bsky.app/docs/advanced-guides/moderation#global-label-values).

To get the profile record for the current user call `agent.GetProfileRecord()`.

```c#
var profileRecordResult = await agent.GetProfileRecord();
```

If the call is successful, it will return an `AtProtoHttpResult` wrapping an `AtProtoRecord<Profile>` which, in turn,
exposes the profile values in its `Value` property.

```c#
var profileRecordResult = await agent.GetProfileRecord();
if (profileRecordResult.Succeeded)
{
    string displayName = profileRecord.Result.Value.DisplayName);
}
```

To make changes to the basic profile information you must get the current record, change the `Value` properties you want to update, then call
`agent.UpdateProfileRecord()`. For example:

```c#
var profileRecordResult = await agent.GetProfileRecord();
if (profileRecordResult.Succeeded)
{
     profileRecordResult.Result.Value.Description =
         $"Profile updated on {DateTimeOffset.Now:G}";

     var updateResult = await agent.UpdateProfileRecord(profileRecordResult.Result);
}
```

If you try to update a profile a second time, without re-reading it, you will get a concurrency error.

## <a name="pinningAPost">Pinning a post.</a>

To pin a post you set the `PinnedPost` property on the `ProfileRecord` value, then call `UpdateProfileRecord()`.
This requires a strong reference to a post, and the post must belong to the current user.

To remove the pinned post set `PinnedPost` to null and call `UpdateProfileRecord()`.

## <a name="discouragingUnauthenticatedViewing">Discouraging apps from showing an account to unauthenticated users.</a>

Bluesky has a setting to *discourage* applications from showing an account to unauthenticated users. The Bluesky application respects this setting,
and other applications *may* respect the setting.

To turn this hint on get the current profile record, then set the `DiscourageShowingToLoggedOutUsers` property on the value to `true`
and then update the profile record.

```c#
var profileRecordResult = await agent.GetProfileRecord();
if (profileRecordResult.Succeeded)
{
    profileRecordResult.Result.Value.DiscourageShowingToLoggedOutUsers = true;
    await agent.UpdateProfileRecord(profileRecordResult.Result);
}
```

To remove the hint set the property to `false` and update the profile record.

