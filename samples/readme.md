# idunno.Bluesky Samples

This folder contains various samples to demonstrate various features of the `idunno.Bluesky` library.

Each console sample uses the same command line arguments:

* `--handle` : The handle to use when authenticating.
* `--password` : The password to use when authenticating.
* `--authcode` : The authorization code to use when authenticating.
* `--proxy`: The URI of the proxy server you wish to use.

If you don't supply a handle or a password samples will check the `_BlueskyHandle` and `_BlueskyPassword` environment variables.

If you use an Bluesky app password you don't need to worry about authorization codes.

## Sample List

* `Samples.ConsoleShell` - a skeleton console application that you can use as a starting point for experimentation.
* `Samples.AtProto` - a sample showing how to use the underlying AtProto APIs.
* `Samples.LoginDiscovery` - a sample that walks threw the various stages of how a handle is resolved its Personal Data Store (PDS).
* `Samples.SessionEvents` - a sample showing how to subscribe to session events so you can persist authentication tokens and recreate sessions on app startup.
* `Samples.Notifications` - a sample which shows notifications for the authenticated user.
* `Samples.Timeline` - a sample that shows reading and paging through the authenticated user's timeline.
* `Samples.TokenRefresh` - a sample that shows background token refresh happening, by hacking the refresh timer to be very short.
