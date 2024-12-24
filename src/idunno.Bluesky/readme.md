# idunno.Bluesky

## About

.NET class libraries for the [Bluesky social network](https://bsky.social/).

## Key Features

* Viewing feeds
* Viewing a user's timeline and notifications
* Viewing threads
* Creating and deleting posts
* Gating threads and posts
* Likes, quotes, and reposts
* Viewing user profiles
* Following and unfollowing users
* Muting and blocking users
* Sending, receiving, and deleting messages

## How to Use

```c#
BlueskyAgent agent = new();

var loginResult = await agent.Login(username, password);
if (loginResult.Succeeded)
{
    var response = await agent.CreatePost("Hello World");
    if (response.Succeeded)
    {
    }
}
```

## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.

The [API status page](https://bluesky.idunno.dev/docs/endpointStatus.html) shows the current state of API support.
