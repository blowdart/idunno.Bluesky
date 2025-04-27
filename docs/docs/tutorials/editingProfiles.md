# Editing profiles

An authenticated user can edit their profile, including updating their display name, avatar, banner image, and bio.

> [!TIP]
> Only accounts with a valid profile are indexed for search. When creating a new account, Bluesky recommends creating a minimal
> profile with `DisplayName` set to the user's [handle](../commonTerms.md#handles).

To create a profile call `agent.SetProfile()` with a profile record value.

`SetProfile(profile)`

| Parameter    | Type                | Description                                                             | Required   |
|--------------|---------------------|-------------------------------------------------------------------------|:----------:|
| profile      | ProfileRecordValue  | The new profile record to create.                                       | Yes        |

```
await agent.SetProfile(
    new ProfileRecordValue(
        displayName: "display name",
        description: "description");
```

To edit a profile get the user's existing profile with `agent.GetProfile()`, edit the profile,
then call `agent.SetProfile()` with the edited profile record.

`SetProfile(profile)`

| Parameter    | Type                | Description                                                             | Required   |
|--------------|---------------------|-------------------------------------------------------------------------|:----------:|
| profile      | ProfileRecord       | The updated profile record to write.                                    | Yes        |

```c#
var getProfileResult = await agent.GetProfile();
if (getProfileResult.Succeeded)
{
    getProfileResult.Result.Profile.Description = "The idunno.Bluesky Test Bot";
    agent.SetProfile(getProfileResult.Result, cancellationToken: cancellationToken);
}
```

### Adding or updating the profile avatar or banner

Images are handled as separate records by Bluesky, so adding or updating an avatar or profile banner requires the additional step of uploading the image to create a Blob record.

```c#
var avatarUploadBlobResult = await agent.UploadBlob(imageAsByteArray, mimeType: "image/jpeg");
var getProfileResult = await agent.GetProfile();

if (getProfileResult.Succeeded &&
    avatarUploadBlobResult.Succeeded)
{
    getProfileResult.Result.Profile.Description = "The idunno.Bluesky Test Bot";
    getProfileResult.Result.Profile.Avatar = avatarUploadBlobResult.Result;
    agent.SetProfile(getProfileResult.Result, cancellationToken: cancellationToken);
}
```
