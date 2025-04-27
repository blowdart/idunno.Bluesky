# Thread Gates

Thread gates set who can reply to a post.

The available rules are

* **idunno.Bluesky.Feed.Gates.MentionRule** : Allow replies from actors mentioned in your post.
* **idunno.Bluesky.Feed.Gates.FollowingRule**: Allow replies from actors you follow.
* **idunno.Bluesky.Feed.Gates.FollowerRule**: Allow replies from actors you follow you.
* **idunno.Bluesky.Feed.Gates.ListRule**: Allow replies from actors in a list.

A thread gate may have up to 5 rules.

## Setting a thread gate

You set a thread gate on one of your existing posts with `agent.AddThreadGate();`

`AddThreadGate(post, rules?)`

| Parameter | Type              | Description                                                | Required   |  Default  |
|-----------|-------------------|------------------------------------------------------------|:----------:|:---------:|
| post      | AtUri             | The [at:// uri](../commonTerms.md#uri) of the post to gate | Yes        |           |
| rules     | ThreadGateRule[]? | A collection of rules to apply to the post                 | No         | *null*    |

```c#
await agent.AddThreadGate(
    post: postUri,
    rules: [
        new FollowingRule(),
        new MentionRule()
    ]);
```

If you pass an empty array for the `rules` parameter then nobody will be able to reply.

```c#
await agent.AddThreadGate(
    post: postUri,
    // allow nobody to reply.
    rules: []);
```

## Deleting a thread gate

To delete a thread gate one of your posts use `agent.DeleteThreadGate();`

`DeleteThreadGate(post)`

| Parameter | Type             | Description                                                | Required   |  Default  |
|-----------|------------------|------------------------------------------------------------|:----------:|:---------:|
| post      | AtUri            | The [at:// uri](../commonTerms.md#uri) of the post to gate | Yes        |           |

```c#
await agent.DeleteThreadGate(
    post: postUri);
```

## Creating a new gated post

You can create a post and post gate rules at the same time by passing an array of `ThreadGateRule` to any of
the `agent.Post()` method that accepts the array.

`Post(text, ..., threadGateRules[]?)`

| Parameter | Type              | Description                                                | Required   |  Default  |
|-----------|-------------------|------------------------------------------------------------|:----------:|:---------:|
| post      | string            | The text for the new post                                  | Yes        |           |
| rules     | ThreadGateRule[]? | A collection of thread gate rules to apply to the post     | No         | *null*    |

```c#
await agent.Post(
    text: "New gated post only for people I follow.",
    threadGateRules: [new FollowingRule()]);
```
