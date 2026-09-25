# <a name="userPreferences">User Preferences</a>

## Reading user preferences

User preferences are stored against a user's profile and can be retrieved with `agent.GetPreferences()`.

```c#
var userPreferences = await agent.GetPreferences();
```

The `Bluesky.Actor.Preferences` class is both a list of user preferences, but also has properties for the commonly used preferences. These include

* `SubscribedLabelers` - a list of labelers the user has subscribed to.
* `ContentLabelPreferences` - a list of preferences for content labelling, the DID of the labeler that produces it and the visibility of content that is labeled.
* `SavedFeedsPreferenceV2` - a list of saved feeds.
* `HiddenPosts` - a list of AT URIs for posts the user has hidden.
* `AdultContentPreference` - the user's preferences for the display of adult content.
* `FeedViewPreferences` - how the user wants to view their feeds, including settings for hiding replies, hiding replies by actors they don't follow etc.
* `MutedWords` - a list of words the user does not want to see, including settings for expiry of the mute, whether it applies to everyone or just actors they don't follow, etc.
* `ThreadViewPreference` - how the user would like threads to be displayed in a client.
* `PostInteractionSettingsPreferences` - the [thread gates](threadGatesAndPostGates.md#threadGates) and [post gates](threadGatesAndPostGates.md#postGates) the user would like applied to new threads or posts they create.

Most of these preferences must be implemented within your client, they are not applied on the Bluesky backend.

For example, the `SubscribedLabelers` preference affects the `Labels` attached to content, but you must pass it into API calls that retrieve posts and threads.

```c#
Preferences preferences = new();
var preferencesResult = await agent.GetPreferences(cancellationToken: cancellationToken);
if (preferencesResult.Succeeded)
{
    preferences = preferencesResult.Result;
}

var timelineResult = await agent.GetTimeline(
    limit: pageSize,
    subscribedLabelers: preferences.SubscribedLabelers);
```

The same applies to `PostInteractionSettingsPreferences`, which you must pass into any of the `Post` methods, or apply to a `PostBuilder`.

```c#
PostInteractionSettingsPreferences? interactionPreferences = null;
var userPreferences = await agent.GetPreferences(cancellationToken: cancellationToken);
if (userPreferences.Succeeded)
{
    interactionPreferences = userPreferences.Result.PostInteractionSettingsPreferences;
}

await agent.Post(
    "Post, using default preferences, if any",
    interactionPreferences: interactionPreferences);
```

For things like `MutedWords` you will need to implement the logic to filter out posts that contain muted words, or for `SubscribedLabelers` you
need to implement the logic to label or hide according to the user preferences.

## Writing user preferences

You can write user preferences with `agent.PutPreferences()`. This method takes a `Preferences` list and will update the user's preferences.
`PutPreferences()` also accepts an `IList<Preference>`, should you only want to update a small number of preferences. You do not have to supply the
full list of preferences returned by `GetPreferences()` only the ones you want to change, for example

```c#
var updatedPreference = new ThreadViewPreference()
{
    PrioritizeFollowedUsers = true
};

await agent.PutPreferences(new List<Preference> { updatedPreference }, cancellationToken: cancellationToken);
```
