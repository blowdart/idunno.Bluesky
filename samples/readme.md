# idunno.Bluesky Samples

This folder contains samples demonstrating various features of the `idunno.Bluesky` library.

Most console samples use the same command line arguments:

* `--handle` : The handle to use when authenticating.
* `--password` : The password to use when authenticating (this parameter is ignored by OAuth samples).
* `--authcode` : The authorization code to use when authenticating.
* `--proxy`: The URI of the proxy server you wish to use.

If you don't supply a handle or a password samples will check the `_BlueskyHandle` and `_BlueskyPassword` environment variables.

If you use a Bluesky app password you don't need to worry about authorization codes.

## Sample List

* `Samples.ConsoleShell` - a skeleton console application which authenticates with a handle and password that you can use as a starting point for experimentation.
* `Samples.ConsoleShellOAuth` - a skeleton console application which authenticates with OAuth that you can use as a starting point for experimentation.
* `Samples.Common` - helper functions used in the sample applications.

* `Samples.AtProto` - a sample showing how to use the underlying AtProto APIs.
* `Samples.Bot` - a sample showing a simple bot posting on a scheduled time.
* `Samples.DirectMessages` - a sample showing how to use the conversation APIs.
* `Samples.EmbeddedCard` - a sample showing how to embed an Open Graph card in a post.
* `Samples.Feed` - a sample showing how to page through a feed.
* `Samples.Firehose` - a live `SubscribeReposAsync()` firehose sample showing each event type, with a five-retry reconnection limit and Ctrl+C shutdown.
* `Samples.Jetstream` - a live v2 `StreamAsync()` sample with ordered events, a five-retry reconnection limit, and Ctrl+C shutdown.
* `Samples.JetstreamReplay` - a Jetstream v2 archive snapshot and live replay sample with checkpoints.
* `Samples.Logging` - a sample showing how to configure logging with the .net console logger.
* `Samples.LoginDiscovery` - a sample that walks through the various stages of how a handle is resolved to its Personal Data Store (PDS).
* `Samples.ModerationLabels` - a `SubscribeLabelsAsync()` sample which shows the labels applied, and negated, by a labeler, defaulting to the Bluesky moderation service, or the labeler given by `--labeler`. `--list` lists every labeler which has published a labeler service record, and `--live` narrows that list to the labelers which answer a query. `dotnet publish` produces it as a single native AOT executable; an ordinary build does not, as the native link step needs a platform C/C++ toolchain.
* `Samples.Notifications` - a sample which shows notifications for the authenticated user.
* `Samples.OAuth` - a sample that demonstrates how to login via OAuth.
* `Samples.Posting` - a sample that shows how to make posts.
* `Samples.Timeline` - a sample that shows reading and paging through the authenticated user's timeline.
* `Samples.TokenRefresh` - a sample that shows background token refresh happening, by hacking the refresh timer to be very short.
* `Samples.Video` - a sample that demonstrates video uploading and embedding.

* `Samples.BulkDelete` - an implementation of a bulk delete application, which allows you to specify the date/time before which your posts, likes etc. will be deleted.
