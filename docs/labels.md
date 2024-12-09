# <a name="labels">Labels</a>
Labelers and [labels](https://docs.bsky.app/docs/advanced-guides/moderation) are how Bluesky allows for composable moderation.
A label is published by a moderation service, which a user or an application can choose to subscribe to.

Once subscribed to a labeler requests to a Bluesky API can request the labeler's labels to be applied to posts or actors,
and the labels will be applied to records underneath the labels key. When an application encounters a label it should act on it based on
the users preferences for that labeler.

## <a name="labelSubscriptions">Getting a user's labeler subscriptions</a>
A user's labeler subscriptions are part of a user's preferences, and can be loaded through the Bluesky agent's GetPreferences method once
the agent has authenticated.

```c#
using Bluesky.Agent;

Preferences userPreferences;
AtProtoHttpResult<Preferences> userPreferencesResult = await agent.GetPreferences();
if (userPreferencesResult.Succeeded)
{
    userPreferences = userPreferencesResult.Result;
}
else
{
    userPreferences = new Preferences();
}
````

From there, you can use the `SubscribedLabelers` property and pass that into any API that takes a `subscribedLabelers` parameter.
For example, to get a user's notifications with labels applied:

```c#
AtProtoHttpResult<NotificationsList> notificationsList =
    await agent.ListNotifications(limit: pageSize, cursor: cursor, subscribedLabelers: userPreferences.SubscribedLabelers).ConfigureAwait(false);
```

For notifications labels are applied to the `Author` property and the `PostView` property. You can iterate through the collection, and act on it

```c#
foreach (Notification notification in notificationsList.Result)
{
    StringBuilder labelDisplayBuilder = new ();
    foreach (Label label in author.Labels)
    {
        labelDisplayBuilder.Append(CultureInfo.InvariantCulture, $"[{label.Value}] ");
    }
    labelDisplayBuilder.Length--;

    Console.WriteLine($"Author: {author} {labelDisplayBuilder}");
}
```

By default idunno.Bluesky will return the Bluesky moderation labeler as part of a user's labeler subscriptions. This can be controlled
with the `includeBlueskyModerationLabeler` parameter on the `GetPreferences` method.

## <a name="labelReacting">Reacting to labels</a>
The value of a label will determine your application's behavior. Some example label values are porn, gore, and spam.
Label values are interpreted by their definitions. Those definitions include these attributes:

* blurs which may be content or media or none
* severity which may be alert or inform or none
* defaultSetting which may be hide or warn or ignore
* adultOnly which is a Boolean

A user chooses to hide, warn, or ignore each label from a labeler.

For more information on labels, their values and how your application should react to them, see the
[Bluesky moderation guide](https://docs.bsky.app/docs/advanced-guides/moderation).

---

## Chapters

[Table of Contents](readme.md) | [Common Terms](commonTerms.md) | [Timelines and Feeds](timeline.md) | [Checking notifications](notifications.md#checkingNotifications) | [Cursors and pagination](cursorsAndPagination.md) | [Posting](posting.md#posting) | [Thread Gates and Post Gates](threadGatesAndPostGates.md) | [Labels](labels.md) | [Conversations and Messages](conversationsAndMessages.md) | [Saving and restoring sessions](savingAndRestoringAuthentication.md) | [Logging](logging.md)
