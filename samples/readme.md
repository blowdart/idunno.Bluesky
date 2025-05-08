# idunno.Bluesky Samples

This folder contains various samples to demonstrate various features of the `idunno.Bluesky` library.

Most console samples uses the same command line arguments:

* `--handle` : The handle to use when authenticating.
* `--password` : The password to use when authenticating (this parameter is ignored by OAuth samples).
* `--authcode` : The authorization code to use when authenticating.
* `--proxy`: The URI of the proxy server you wish to use.

If you don't supply a handle or a password samples will check the `_BlueskyHandle` and `_BlueskyPassword` environment variables.

If you use an Bluesky app password you don't need to worry about authorization codes.

## Sample List

* `Samples.ConsoleShell` - a skeleton console application which authenticates with a handle and password that you can use as a starting point for experimentation.
* `Samples.ConsoleShell.OAuth` - a skeleton console application which authenticates with OAuth that you can use as a starting point for experimentation.
* `Samples.Common` - helper functions used in the sample applications.

* `Samples.AtProto` - a sample showing how to use the underlying AtProto APIs.
* `Samples.DirectMessages` - a sample showing how to use the conversation APIs.
* `Samples.EmbeddedCard` - a sample showing how to embed an Open Graph card in a post.
* `Samples.Feed` - a sample showing how to page through a feed.
* `Samples.Jetstream`- a sample showing how to subscribe to the AtProto Jetstream.
* `Samples.Logging` - a sample showing how to configure logging with the .net console logger.
* `Samples.LoginDiscovery` - a sample that walks threw the various stages of how a handle is resolved its Personal Data Store (PDS).
* `Samples.Notifications` - a sample which shows notifications for the authenticated user.
* `Samples.OAuth` - a sample that demonstrates how to login via OAuth.
* `Samples.Posting` - a sample that shows how to make posts.
* `Samples.Timeline` - a sample that shows reading and paging through the authenticated user's timeline.
* `Samples.TokenRefresh` - a sample that shows background token refresh happening, by hacking the refresh timer to be very short.
* `Samples.Video` - a sample that demonstrates video uploading and embedding.

* `Samples.BulkDelete` - a implementation of a bulk delete application, which allows you to specify the date/time before which your posts, likes etc. will be deleted.
