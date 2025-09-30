# idunno.Bluesky

## About

.NET class libraries for the [Bluesky social network](https://bsky.social/).

## Key Features

* Viewing a user's timeline and notifications
* Viewing feeds
* Viewing threads
* Creating and deleting posts
  * Adding mentions, links and hashtags to posts
  * Adding images and video to posts
  * Gating threads and posts
  * Liking, quoting, and reposting posts
* Notifications
  * Viewing and setting preferences for
  * Subscribing to user activities
* Viewing user profiles
* Following and unfollowing users
* Muting and blocking users
* Sending, receiving, and deleting messages

* Trimming is supported for applications targeting .NET 9.0 or later.

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

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
