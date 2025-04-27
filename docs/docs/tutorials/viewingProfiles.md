# Viewing profiles

A profile is typically made up of a user's biographical information, and their posts.

> [!TIP]
> To learn how to fetch a user's posts, see the [Author feeds](viewingFeeds.md#authorFeeds) in the [Viewing feeds](viewingFeeds.md) tutorial.

## Fetching a user's profile

To fetch a user's profile info, you can use the `agent.GetProfile()` method.

`GetProfile(actor)`

| Parameter | Type         | Description                                                                                                             | Required   |
|-----------|--------------|-------------------------------------------------------------------------------------------------------------------------|:----------:|
| actor     | AtIdentifier | The [DID](../commonTerms.md#dids) or [handle](../commonTerms.md#handles) of the user whose profile you'd like to fetch. | Yes        |

```c#
var getProfileResult = agent.GetProfile(actor);
```

## Fetching multiple profiles at once

Fetching multiple profiles is as easy as fetching a single profile, you use the `agent.GetProfiles()` method.

`GetProfile([actors])`

| Parameter  | Type           | Description                                                                                                             | Required   |
|----------- |--------------  |-------------------------------------------------------------------------------------------------------------------------|:----------:|
| actors     | AtIdentifier[] | The [DID](../commonTerms.md#dids) or [handle](../commonTerms.md#handles) of the user whose profile you'd like to fetch. | Yes        |

```c#
var getProfilesResult = agent.GetProfiles([actor, actor]);
```
