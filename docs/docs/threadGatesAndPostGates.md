# Thread Gates and Post Gates

## <a name="threadGates">Thread Gates</a>

Thread gates allow the original author of a thread to control who can reply to the thread, allowing people mentioned in the root post to reply,
people the author is following to reply, replies from actors in a list or allow no replies at all.
A thread gate can have up to five rules, but allowing no replies is an exclusive rule, no other rules can be applied.
A thread gate can also be used to hide replies in a thread.

You can apply a thread gate to an existing using `AddThreadGate()`. This method requires the `AtUri` of the post to be gated,
and, optionally a collection of gate rules and/or a collection of `AtUri`s of thread replies to be hidden.
If you provide no rules then the post will not allow any replies at all.

The following example demonstrates how to gate a thread so that replies are restricted to users the post author follows.

```c#
await agent.AddThreadGate(
    postUri,
    new List<ThreadGateRule>()
    {
        new FollowingRule()
    },
    cancellationToken: cancellationToken);
```

The four types of thread gate rules are `FollowerRule`, `FollowingRule`, `MentionRule` and `ListRule`. Note that adding,
or updating a thread gate replaces any gate already in place. If you want to update rules or hidden posts first get any existing rule
with `GetThreadGate()`, if that is successful update the returned`ThreadGate` class then apply it with with `UpdateThreadGate()`.

You can use `GetPostThread()` to see a view over a thread, including replies.

## <a name="postGates">Post Gates</a>

Post gates allow the post author to remove it from someone else's post quoting their post, and also to disable embedding on a post.

```c#
await agent.AddPostGate(
    postUri,
    new List<PostGateRule>()
    {
        new DisableEmbeddingRule()
    },
    cancellationToken: cancellationToken);
```

You can get a view of posts quoting a post with `GetQuotes()`.

You can also specify gates when creating a post:

```c#
await agent.Post("New gated post",
    threadGateRules: new List<ThreadGateRule>() { new FollowingRule() },
    postGateRules: new List<PostGateRule>() { new DisableEmbeddingRule() },
    cancellationToken: cancellationToken);
```

## Default user preferences for post and thread gates.

Bluesky allows the users to set a default preference for post. and thread gates. You can retrieve these preferences with `agent.GetPreferences()`.

```c#
InteractionPreferences? interactionPreferences = null;
var userPreferences = await agent.GetPreferences(cancellationToken: cancellationToken);
if (userPreferences.Succeeded)
{
    interactionPreferences = userPreferences.Result.InteractionPreferences;
}
```

You can then past this into `agent.Post()`

```c#
await agent.Post(
    "Sample text, using default preferences",
    interactionPreferences: interactionPreferences,
    cancellationToken: cancellationToken);
```

Note that the Bluesky application allows a user to override their preferences on a post by post basis.
If you specify a `threadGateRules` or `postGateRules` value to `agent.Post()` they will take precedence.

If you are using a `PostBuilder` you can apply the user preferences to the builder with `PostBuilder.ApplyInteractionPreferences()`.

