# idunno.Bluesky Version History

## 0.3.0

### Features

#### idunno.AtProto

* OAuth support

### Breaking Changes

* Agent constructors no longer take parameters directly. Use the appropriate options class instead.

  For example

  ```c#
  using (var agent = new BlueskyAgent(
    proxyUri: proxyUri,
    checkCertificateRevocationList: checkCertificateRevocationList,
    loggerFactory: loggerFactory))
  ```

  becomes

  ```c#
  using (var agent = new BlueskyAgent(
    new BlueskyAgentOptions()
    {
      LoggerFactory = loggerFactory,

      HttpClientOptions = new HttpClientOptions()
      {
        CheckCertificateRevocationList = checkCertificateRevocationList,
        ProxyUri = proxyUri
      }
    }))
  ```

* Authentication has be reworked to support OAuth.
  * `agent.Login(Credentials)` has been removed.
     Use `agent.Login(Login(string identifier, string password, string? authFactorToken = null)` instead.

* The `Session` property on an agent has been removed.

  `Credentials` can be accessed by the `Credentials` property.

  The DID for the authenticated user, if any, can be accessed by the `Did` property.

* The agent events for changes in session state have been replaced with events for changes in authentication state.

  The `SessionChanged` event has been removed, replaced by the `Authenicated` event.

  The `SessionEnded` event has been removed, replaced by the `Unauthenticated` events.

  Event arguments have changed to support OAuth credentials. If you are saving authentication state please see the AgentEvents sample for details.

* `RefreshToken()` has been removed, to manually refresh the agent credentials use `agent.RefreshCredentials()`.

#### idunno.AtProto.OAuthCallback

* Local HTTP server for testing OAuth authentication.

## 0.2.1

### Features

#### idunno.AtProto

* Extra logging in token refresh

### Bug Fixes

#### idunno.AtProto

* Fixed incorrect JWT DateTime comparison - thank you [alexmg](https://github.com/alexmg)
* Fixed json deserialization errors in GetSessionResponse

#### Samples

* Added catch in Samples.SessionEvents when a bad token is being set on purpose - thank you [peteraritchie](https://github.com/peteraritchie)

## 0.2.0

### Features

#### idunno.AtProto

* Add support for GetServiceAuth()

#### idunno.Bluesky

* Add support for video

## 0.1.3

### Features

#### idunno.Bluesky

* Add self labels for posts
* *Breaking* - Consolidation of record value classes

### Bug fixes

#### idunno.Bluesky

* Fixed facet positioning

### Docs

* Add Profile editing sample

## 0.1.2

### Features

#### idunno.Bluesky

* Adds Profile editing

### Bug fixes

#### idunno.Bluesky

* Fixed positioning bug for PostBuilder facets
* Removed incorrect link length check.

## 0.1.1

### Bug Fixes

#### idunno.AtProto

* Fixed bug when making requests a personal PDS timed out after authentication.

## 0.1.0

### Features

#### idunno.AtProto

* PDS authentication and session management
* List, Create, Get, Put, Delete records
* Blob uploads
* Handle and PDS resolution

#### idunno.Bluesky

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

