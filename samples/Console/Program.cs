// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Bluesky;
using idunno.AtProto.Bluesky.Actor;
using idunno.AtProto.Bluesky.Feed;
using idunno.AtProto.Bluesky.Notifications;
using idunno.AtProto.Repo;
using idunno.AtProto.Server;

internal class Program
{
    private static async Task<int> Main()
    {
        string? username = Environment.GetEnvironmentVariable("_BlueskyUserName");
        string? password = Environment.GetEnvironmentVariable("_BlueskyPassword");

        if (username is null || password is null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Set _BlueSkyUserName and _BlueSkyPassword environment variables before running.");
            Console.ReadLine();
            return 1;
        }

        //// Fiddler Setup
        var proxy = new WebProxy
        {
            Address = new Uri($"http://127.0.0.1:8866"),
            BypassProxyOnLocal = false,
            UseDefaultCredentials = false,
        };

        //https://bsky.app/profile/blowdart.me/post/3kqwecgayrk2s

        using (var httpClientHandler = new HttpClientHandler { Proxy = proxy })
        {

            using (BlueskyAgent recordAgent = new(httpClientHandler: httpClientHandler))
            {
                HttpResult<bool> loginResult = await recordAgent.Login(username, password);
                if (!loginResult.Succeeded || recordAgent.Session is null)
                {
                    Console.WriteLine("login failed");
                }

                AtUri atUri = await recordAgent.BuildAtUriFromBlueskyWebUri(new Uri("https://bsky.app/profile/blowdart.me/post/3kqwecgayrk2s")).ConfigureAwait(false);

                HttpResult<FeedPost> individualPostRecord = await recordAgent.GetPost(atUri);
                HttpResult<ThreadView> postThread = await recordAgent.GetPostThread(atUri);
            }

            using (BlueskyAgent agent = new(httpClientHandler: httpClientHandler))
            {

                HttpResult<Did> did = await agent.ResolveHandle("blowdart.me");

                Console.WriteLine($"Connecting to {agent.DefaultService}");
                Console.WriteLine();

                HttpResult<ServerDescription> descriptionResult = await agent.DescribeServer(agent.DefaultService);

                if (descriptionResult.Succeeded)
                {
                    if (descriptionResult.Result is not null)
                    {
                        Console.WriteLine($"Connected to Server DID : {descriptionResult.Result.Did}");
                        if (descriptionResult.Result.Contact is not null &&
                            !string.IsNullOrEmpty(descriptionResult.Result.Contact.Email))
                        {
                            Console.WriteLine($"\tServer Contact : {descriptionResult.Result.Contact.Email}");
                        }

                        if (descriptionResult.Result.Links is not null)
                        {
                            if (descriptionResult.Result.Links.PrivacyPolicy is not null)
                            {
                                Console.WriteLine($"\tPrivacy Policy: {descriptionResult.Result.Links.PrivacyPolicy}");
                            }

                            if (descriptionResult.Result.Links.TermsOfService is not null)
                            {
                                Console.WriteLine($"\tTerms of Service: {descriptionResult.Result.Links.TermsOfService}");
                            }
                        }

                        Console.WriteLine("\tAvailable Domains :");
                        foreach (string availableDomain in descriptionResult.Result.AvailableUserDomains)
                        {
                            Console.WriteLine($"\t\t{availableDomain}");
                        }
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine("Could not get description for service.");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"{descriptionResult.StatusCode} occurred when getting server description.");
                    return 1;
                }

                HttpResult<bool> loginResult = await agent.Login(username, password);
                if (loginResult.Succeeded && agent.Session is not null)
                {
                    Console.WriteLine($"Logged in as {agent.Session.Handle}");
                    Console.WriteLine($"\tSession (My) DID: {agent.Session.Did}");
                    Console.WriteLine($"\tAccess JWT: {agent.Session.AccessJwt}");
                    Console.WriteLine($"\tRefresh JWT: {agent.Session.RefreshJwt}");

                    DateTimeOffset notificationCheckDateTime = DateTimeOffset.UtcNow;

                    HttpResult<int> unreadCount = await agent.GetNotificationUnreadCount(notificationCheckDateTime);

                    Console.WriteLine($"You have {unreadCount.Result} notification{(unreadCount.Result != 1 ? "s" : "")}.");

                    if (unreadCount.Result > 0)
                    {
                        HttpResult<NotificationsView> notifications = await agent.ListNotifications();
                    }

                    HttpResult<EmptyResponse> updateSeen = await agent.UpdateNotificationSeenAt(notificationCheckDateTime);

                    HttpResult<RepoDescription> repoDescription = await agent.DescribeRepo(new Handle("blowdart.me"));

                    HttpResult<ThreadView> postThread = await agent.GetPostThread(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3kqeov3xlym2y"));

                    HttpResult<ThreadView> postThreadWithFacets = await agent.GetPostThread(new AtUri("at://did:plc:rrtzr4tlipqrlxzs7jn7xwsb/app.bsky.feed.post/3kqot2yple52v"));

                    HttpResult<ActorSuggestions> suggestionsResult = await agent.GetSuggestions();

                    HttpResult<ActorSearchResults> searchActorResults = await agent.SearchActors("blowdart.me");

                    HttpResult<PostSearchResults> searchFeedResults = await agent.SearchPosts("goatse");

                    DidDocument? didDocument = agent.Session.DidDocument;
                    if (didDocument is not null)
                    {
                        Uri? pds = didDocument.Services!.Where(s => s.Id == @"#atproto_pds").FirstOrDefault()!.ServiceEndpoint;
                        Console.WriteLine($"\tPDS: {pds}");
                    }

                    HttpResult<ActorProfile> getActorProfileResult = await agent.GetActorProfile(agent.Session.Did);

                    if (!getActorProfileResult.Succeeded)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{getActorProfileResult.StatusCode} occurred when getting the actor profile for {agent.Session.Did}");
                        return 1;
                    }

                    if (getActorProfileResult.Result is not null)
                    {
                        Console.WriteLine($"My profile:\n\tDID : {getActorProfileResult.Result.Did}\n\tHandle : {getActorProfileResult.Result.Handle}");
                        if (!string.IsNullOrEmpty(getActorProfileResult.Result.Description))
                        {
                            Console.WriteLine($"\tDescription: {getActorProfileResult.Result.Description}");
                        }
                        Console.WriteLine($"\tFollowers: {getActorProfileResult.Result.FollowersCount}");
                        Console.WriteLine($"\tFollows: {getActorProfileResult.Result.FollowsCount}");

                    }

                    AtIdentifier[] actors = new AtIdentifier[2];
                    actors[0] = new Handle("blowdart.me");
                    actors[1] = agent.Session.Did;

                    HttpResult<IReadOnlyList<ActorProfile>?> getActorProfilesResult = await agent.GetActorProfiles(actors);

                    HttpResult<Timeline> getTimelineResult = await agent.GetTimeline();

                    if (getTimelineResult.Result is not null)
                    {
                        Console.WriteLine($"Timeline: \n\t{getTimelineResult.Result.Feed.Count} posts.");

                        foreach (FeedView feedView in getTimelineResult.Result.Feed)
                        {
                            string? author = feedView.Post.Author.Handle.ToString();
                            if (feedView.Post.Author.DisplayName is not null && feedView.Post.Author.DisplayName.Length > 0)
                            {
                                author = feedView.Post.Author.DisplayName;
                            }

                            Console.WriteLine($"\t\t{feedView.Post.Record.Text} - {author}.");
                            Console.WriteLine($"\t\t\t{feedView.Post.Record.CreatedAt}");
                            Console.WriteLine($"\t\t\t{feedView.Post.LikeCount} like{(feedView.Post.LikeCount != 1 ? "s" : "")} {feedView.Post.RepostCount} repost{(feedView.Post.RepostCount != 1 ? "s" : "")}.");
                            Console.WriteLine($"\t\t\tSubject: Uri={feedView.Post.Uri}/Cid={feedView.Post.Cid}.");
                        }
                    }

                    Task<HttpResult<Did>> resolvedHandle = agent.ResolveHandle("blowdart.me");

                    AtUri postAtUri = await agent.BuildAtUriFromBlueskyWebUri(new Uri("https://bsky.app/profile/blowdart.me/post/3kqekdlhgaw2c")).ConfigureAwait(false);
                    HttpResult<FeedPost> postDetails = await agent.GetPost(postAtUri).ConfigureAwait(false);

                    await agent.Logout();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"{loginResult.StatusCode} occurred when getting creating a server session.");
                    return 1;
                }
                //string s = "✨ example mentioning @atproto.com to share the URL 👨‍❤️‍👨 https://en.wikipedia.org/wiki/CBOR.";

                Console.WriteLine("Going for posting");

                HttpResult<bool> postLoginResult = await agent.Login(username, password);

                if (postLoginResult.Succeeded && agent.Session is not null && agent.Session.Did is not null)
                {
                    HttpResult<StrongReference> simplePostResult = await agent.CreatePost($"Testing like/unlike repost/delete repost. {DateTimeOffset.Now}");

                    if (!simplePostResult.Succeeded || simplePostResult.Result is null || simplePostResult.Result.Uri.RKey is null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{simplePostResult.StatusCode} occurred when creating simple post.");
                        return 1;
                    }

                    Console.WriteLine($"Post created with\n\tCID {simplePostResult.Result.Cid}\n\tURI {simplePostResult.Result.Uri}\n\trkey {simplePostResult.Result.Uri.RKey}");

                    Console.WriteLine("Getting the record for the newly created post");

                    HttpResult<AtProtoRecord> record = await agent.GetRecord(agent.Session.Did, CollectionType.Post, simplePostResult.Result.Uri.RKey);

                    Console.WriteLine("Liking own post.");

                    HttpResult<StrongReference> likeResult = await agent.LikePost(simplePostResult.Result);

                    if (!likeResult.Succeeded || likeResult.Result == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{likeResult.StatusCode} occurred when liking the simple post.");
                        return 1;
                    }

                    Console.WriteLine($"Like created with CID {likeResult.Result.Cid} URI {likeResult.Result.Uri}");

                    Console.WriteLine("Deleting the like.");

                    HttpResult<bool> deleteLikeResult = await agent.UndoLike(likeResult.Result);

                    Console.WriteLine("Reposting own post");

                    HttpResult<StrongReference> repostResult = await agent.Repost(simplePostResult.Result);

                    if (!repostResult.Succeeded || repostResult.Result == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{repostResult.StatusCode} occurred when reposting the simple post.");
                        return 1;
                    }

                    Console.WriteLine("Deleting the repost.");

                    HttpResult<bool> deleteRepostResult = await agent.UndoRepost(repostResult.Result);

                    Console.WriteLine("Deleting the post.");

                    HttpResult<bool> deleteResult = await agent.DeletePost(simplePostResult.Result.Uri.RKey);

                    if (!deleteResult.Succeeded)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{deleteResult.StatusCode} occurred when deleting the simple post.");
                        return 1;
                    }

                    HttpResult<Did> handleResolutionResult = await agent.ResolveHandle("blowdart.me");
                    if (!handleResolutionResult.Succeeded)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{handleResolutionResult.StatusCode} resolving a handle.");
                        return 1;
                    }

                    if (handleResolutionResult.Result is null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Resolve handle returned a null document.");
                        return 1;
                    }

                    HttpResult<ActorProfile> getActorProfileResult = await agent.GetActorProfile(handleResolutionResult.Result);

                    if (!getActorProfileResult.Succeeded)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{getActorProfileResult.StatusCode} occurred when getting the actor profile for {agent.Session.Did}.");
                        return 1;
                    }

                    if (getActorProfileResult.Result is not null)
                    {
                        Console.WriteLine($"Retrieved profile:\n\tDID : {getActorProfileResult.Result.Did}\n\tHandle : {getActorProfileResult.Result.Handle}");
                        if (!string.IsNullOrEmpty(getActorProfileResult.Result.Description))
                        {
                            Console.WriteLine($"\tDescription: {getActorProfileResult.Result.Description}");
                        }
                        Console.WriteLine($"\tFollowers: {getActorProfileResult.Result.FollowersCount}");
                        Console.WriteLine($"\tFollows: {getActorProfileResult.Result.FollowsCount}");
                    }

                    var post = new NewBlueskyPost("Hello ", "en-US");
                    post += new Facet("@blowdart.me", post.LengthAsUTF8, handleResolutionResult.Result);
                    post += ' ';
                    post += new Facet("#itWorks", post.LengthAsUTF8, "itWorks");
                    post += ". (or does it?)";

                    HttpResult<StrongReference> createRecordResponse = await agent.CreatePost(post);

                    if (createRecordResponse.Succeeded && createRecordResponse.Result is not null)
                    {
                        Console.WriteLine($"Post created with\n\tCID {createRecordResponse.Result.Cid}\n\tURI {createRecordResponse.Result.Uri}");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{createRecordResponse.StatusCode} occurred when posting.");
                        return 1;
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"{loginResult.StatusCode} occurred when getting creating a server session.");
                    return 1;
                }

                Console.ReadLine();
                return 0;
            }
        }
    }
}
