// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;

using Microsoft.Extensions.Logging;

using idunno.Bluesky;

using Samples.Common;
using idunno.Bluesky.Chat;
using System.Text;
using idunno.Bluesky.Actor;
using System.Diagnostics;
using idunno.Bluesky.Notifications;

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
            ArgumentNullException.ThrowIfNullOrEmpty(handle);
            ArgumentNullException.ThrowIfNullOrEmpty(password);

            // Uncomment the next line to route all requests through Fiddler Everywhere
            proxyUri = new Uri("http://localhost:8866");

            // Uncomment the next line to route all requests  through Fiddler Classic
            // proxyUri = new Uri("http://localhost:8888");

            // Get an HttpClient configured to use a proxy, if proxyUri is not null.
            using (HttpClient? httpClient = Helpers.CreateOptionalHttpClient(proxyUri))

            // Change the log level in the ConfigureConsoleLogging() to enable logging
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))

            // Create a new BlueSkyAgent
            using (var agent = new BlueskyAgent(httpClient: httpClient, loggerFactory: loggerFactory))
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

                            if (!conversation.Opened)
                            {
                                Console.Write("\u001b[1mUnopened ");
                            }

                            Console.Write($"Conversation #{conversation.Id} between {conversationMembers}");

                            if (conversation.UnreadCount != 0)
                            {
                                Console.Write($" {conversation.UnreadCount} unread");
                            }

                            if (!conversation.Opened)
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
