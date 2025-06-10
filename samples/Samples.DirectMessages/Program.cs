// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Embed;

using Samples.Common;

namespace Samples.DirectMessages
{
    public class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Necessary to render emojis.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var parser = Helpers.ConfigureCommandLine(PerformOperations);
            await parser.InvokeAsync(args);

            return 0;
        }

        static async Task PerformOperations(string? handle, string? password, string? authCode, Uri? proxyUri, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(handle);
            ArgumentException.ThrowIfNullOrEmpty(password);

            // Uncomment the next line to route all requests through Fiddler Everywhere
            // proxyUri = new Uri("http://localhost:8866");

            // Uncomment the next line to route all requests  through Fiddler Classic
            // proxyUri = new Uri("http://localhost:8888");

            // If a proxy is being used turn off certificate revocation checks.
            //
            // WARNING: this setting can introduce security vulnerabilities.
            // The assumption in these samples is that any proxy is a debugging proxy,
            // which tend to not support CRLs in the proxy HTTPS certificates they generate.
            bool checkCertificateRevocationList = true;
            if (proxyUri is not null)
            {
                checkCertificateRevocationList = false;
            }

            // Change the log level in the ConfigureConsoleLogging() to enable logging
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))

            // Create a new BlueSkyAgent
            using (var agent = new BlueskyAgent(
                options: new BlueskyAgentOptions()
                {
                    LoggerFactory = loggerFactory,
                    HttpClientOptions = new HttpClientOptions()
                    {
                        ProxyUri = proxyUri,
                        CheckCertificateRevocationList = checkCertificateRevocationList
                    }
                }))
            {
                var loginResult = await agent.Login(handle, password, authCode, cancellationToken: cancellationToken);
                if (!loginResult.Succeeded)
                {
                    if (loginResult.AtErrorDetail is not null &&
                        string.Equals(loginResult.AtErrorDetail.Error!, "AuthFactorTokenRequired", StringComparison.OrdinalIgnoreCase))
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Login requires an authentication code.");
                        Console.WriteLine("Check your email and use --authCode to specify the authentication code.");
                        Console.ForegroundColor = oldColor;

                        return;
                    }
                    else
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Login failed.");
                        Console.ForegroundColor = oldColor;

                        if (loginResult.AtErrorDetail is not null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;

                            Console.WriteLine($"Server returned {loginResult.AtErrorDetail.Error} / {loginResult.AtErrorDetail.Message}");
                            Console.ForegroundColor = oldColor;

                            return;
                        }
                    }
                }

                var startConversationResult = await agent.GetConversationForMembers(["did:plc:hfgp6pj3akhqxntgqwramlbg"], cancellationToken: cancellationToken);
                if (startConversationResult.Succeeded)
                {
                    var post = await agent.GetPostRecord("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lqxyqocwx22m", cancellationToken: cancellationToken);

                    var sendMessageResult = await agent.SendMessage(
                        startConversationResult.Result.Id,
                        "Embedded post test",
                        embeddedPost: post.Result!.StrongReference,
                        cancellationToken: cancellationToken);
                }

                var listConversations = await agent.ListConversations(cancellationToken: cancellationToken);

                if (listConversations.Succeeded && listConversations.Result.Count != 0 && !cancellationToken.IsCancellationRequested)
                {
                    do
                    {
                        foreach (ConversationView conversation in listConversations.Result)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            StringBuilder conversationMembers = new();
                            foreach (ProfileViewBasic member in conversation.Members)
                            {
                                conversationMembers.Append(member.ToString());
                                conversationMembers.Append(" & ");
                            }
                            conversationMembers.Length -= 3;

                            if (conversation.Status == ConversationStatus.Requested)
                            {
                                Console.Write("\u001b[1mRequested ");
                            }

                            Console.Write($"Conversation #{conversation.Id} between {conversationMembers}");

                            if (conversation.UnreadCount != 0)
                            {
                                Console.Write($" {conversation.UnreadCount} unread");
                            }

                            if (conversation.Status == ConversationStatus.Requested)
                            {
                                Console.Write("\u001b[22m");
                            }

                            Console.WriteLine();

                            if (!cancellationToken.IsCancellationRequested)
                            {
                                var getConversation = await agent.GetConversation(conversation.Id, cancellationToken);

                                if (getConversation.Succeeded)
                                {
                                    if (getConversation.Result.LastMessage is MessageView messageView)
                                    {
                                        Console.WriteLine($"  {messageView.Text}");
                                    }
                                    else
                                    {
                                        Console.WriteLine("  Last message was deleted.");
                                    }

                                    Console.WriteLine();

                                    var getMessages = await agent.GetMessages(conversation.Id, cancellationToken: cancellationToken);
                                    if (getMessages.Succeeded && getMessages.Result.Count != 0 && !cancellationToken.IsCancellationRequested)
                                    {
                                        do
                                        {
                                            foreach (MessageViewBase message in getMessages.Result)
                                            {
                                                if (message is MessageView view)
                                                {
                                                    var sender = getConversation.Result.Members.FirstOrDefault(m => m.Did == view.Sender.Did) ??
                                                                 throw new InvalidOperationException("Cannot find message sender in conversation view");

                                                    Console.WriteLine($"{sender}: {view.Text} {view.SentAt:g}");

                                                    foreach (ReactionView reaction in view.Reactions)
                                                    {
                                                        var reactionSender = getConversation.Result.Members.FirstOrDefault(m => m.Did == view.Sender.Did) ??
                                                            throw new InvalidOperationException("Cannot find message sender in conversation view");
                                                        Console.WriteLine($"{reactionSender}: {reaction.Value} {reaction.CreatedAt:g}");
                                                    }

                                                    if (view.Embed is not null &&
                                                        view.Embed.Record is not null &&
                                                        view.Embed.Record is ViewRecord viewRecord)
                                                    {
                                                        Console.WriteLine("  Embedded Record");

                                                        if (viewRecord.Value is Post post)
                                                        {
                                                            Console.WriteLine($"  {viewRecord.Author}");
                                                            Console.WriteLine($"  {post.Text}");
                                                        }
                                                    }
                                                }
                                                else if (message is DeletedMessageView _)
                                                {
                                                    Console.WriteLine("Deleted Message");
                                                }
                                            }

                                            if (!string.IsNullOrEmpty(getMessages.Result.Cursor) && !cancellationToken.IsCancellationRequested)
                                            {
                                                getMessages = await agent.GetMessages(conversation.Id, cursor: getMessages.Result.Cursor, cancellationToken: cancellationToken);
                                            }

                                        } while (!cancellationToken.IsCancellationRequested &&
                                                 getMessages.Succeeded &&
                                                 !string.IsNullOrEmpty(getMessages.Result.Cursor));
                                    }

                                    await agent.UpdateRead(conversation.Id, cancellationToken: cancellationToken);
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(listConversations.Result.Cursor) && !cancellationToken.IsCancellationRequested)
                        {
                            listConversations = await agent.ListConversations(cursor: listConversations.Result.Cursor, cancellationToken: cancellationToken);
                        }

                    } while (!cancellationToken.IsCancellationRequested &&
                             listConversations.Succeeded &&
                             !string.IsNullOrEmpty(listConversations.Result.Cursor));
                }

                Debugger.Break();
            }
        }
    }
}
