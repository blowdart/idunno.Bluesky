# Using `idunno.Bluesky`

This document provides a starting point to reading from, and posting to, the [Bluesky social network](https://bsky.app/), using the `idunno.AtProto`. library. 

Bluesky's HTTP Reference can be found in their [documentation](https://docs.bsky.app/docs/category/http-reference).

The status page for `idunno.AtProto`'s [current and planned APIs](endpointStatus.md) contains information on the library classes that implement Bluesky or AT Proto APIs along with the Bluesky and AT Proto endpoints they call. 

## <a name="toc">Table of Contents</a>

* [Getting Started](gettingStarted.md)
    * [Making requests to Bluesky and understanding the results](gettingStarted.md#makingRequests)
    * [Connecting to Bluesky](gettingStarted.md#connecting)
        * [Using a proxy server](gettingStarted.md#usingAProxy)
        * [Disabling token refresh](gettingStarted.md#disablingTokenRefresh)
    * [Error Handling](gettingStarted.md#errorHandling)
* [Common Terms](commonTerms.md)
    * [AT URIs, CIDs and Strong References](commonTerms.md#atUris)
    * [Records, Collections and NSIDs](commonTerms.md#records)
    * [Actors, handles and DIDs](commonTerms.md#actorsHandlesDids)
        * [Handles](commonTerms.md#handles)
        * [DIDs](commonTerms.md#dids)
        * [Resolving a Handle to a DID](commonTerms.md#resolvingHandles)
    * [Views](commonTerms.md#views)
* [Timelines and Feeds](timeline.md)
  * [Reading your timeline](timeline.md#timeline)
  * [Reading feeds](timeline.md#feeds)
  * [Searching](timeline.md#searching)
* [Checking notifications](notifications.md#checkingNotifications)
* [Cursors and pagination](cursorsAndPagination.md)
* [Posting](posting.md#posting)
    * [Creating a post](posting.md#creatingAPost)
    * [Understanding the results from a post call](posting.md#understandingPostResults)
    * [Deleting a post](posting.md#deletingAPost)
    * [Replying to a post](posting.md#replyingToAPost)
    * [Getting a post from its AT URI](posting.md#gettingAPost)
    * [Liking, reposting and quote posting posts](posting.md#likeRepostQuote)
    * [Getting your relationships with a post](posting.md#postRelationships)
    * [Rich Posts](posting.md#richPosts)
      * [Facet auto-detection](posting.md#autoDetection)
      * [Replacing the facet extractor](posting.md#facetProvider)
      * [Building facets with a PostBuilder](posting.md#postBuilder)
    * [Posting with images](posting.md#images)
    * [Embedding an external link (Open Graph cards)](posting.md#openGraphCards)
* [Thread Gates and Post Gates](threadGatesAndPostGates.md)
* [Labels](labels.md)
    * [Getting a user's labeler subscriptions](labels.md#labelSubscriptions)
    * [Reacting to labels](labels.md#labelReacting)]
* [Conversations and Messages](conversationsAndMessages.md)
    * [Reading conversations and messages](conversationsAndMessages.md#reading)
    * [Starting a conversation](conversationsAndMessages.md#starting)
    * [Sending a message](conversationsAndMessages.md#sending)
    * [Deleting a message](conversationsAndMessages.md#deleting)
    * [Leaving a conversation](conversationsAndMessages.md#leaving)
* [Saving and restoring sessions](savingAndRestoringAuthentication.md)
    * [Authentication Events](savingAndRestoringAuthentication.md#authenticationEvents)
    * [Restoring or recreating a session](savingAndRestoringAuthentication.md#restoringSessions)
* [Logging](logging.md)
    * [Configuring logging](logging.md#configuring)
