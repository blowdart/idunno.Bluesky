# Conversations and Messages

## <a name="reading">Reading conversations and messages</a>

> [!IMPORTANT]
> If you or your users are using app passwords they must select the option to "Allow access to your direct messages" when creating the app password for
> any of the conversation apis to work.
>
> If you are using [OAuth](connecting.md#oauth) you must request the `transition:chat.bsky` scope.

To get a list of conversations for the authenticated user use the `ListConversations()` api:

```c#
var listConversations = await agent.ListConversations();
```

This returns a [pageable list](cursorsAndPagination.md) of `ConversationView` which supplies the conversation ID, members,
a flag indicating whether the user has opened the conversation,the number of unread messages and a view of the last message in the conversation.

If you already have a conversation ID you can use `GetConversation` to retrieve the individual `ConversationView` for that identifier.

To retrieve the messages in a conversation use `GetMessages`:

```c#
var getMessages = await agent.GetMessages(conversationId, cancellationToken: cancellationToken);
```

The returns a [pageable list](cursorsAndPagination.md) of either `MessageView` or `DeletedMessageView` for each message in the conversation, as well
as the DID of the sender, which you can match up to the `ConversationView` to get the sender information, for example

```c#
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
```

Once you've shown a user their messages you can update the read status of a conversation with `UpdateRead()` which takes the conversation id, and,
optionally the message id of the last message seen.

## <a name="sending">Sending a message</a>

To send a message to a conversation use `SendMessage()`:

```c#
var sendMessage = await agent.SendMessage(conversationID, "hello"", cancellationToken);
```

This returns a `MessageView` which includes the message identifier, which you can use to delete a message.

For rich messages, tagging DIDs, embedding links etc. you can pass an instance of `MessageInput` and specify the facets for the message.

You can send multiple messages into multiple conversations using `SendMessageBatch()`.

## <a name="deleting">Deleting a message in a conversation</a>

To delete a message in a conversation use `DeleteMessageForSelf()`, passing the conversation id and the message id.

## <a name="reacting">Message reactions</a>

Bluesky allows for simple message reactions in conversations. The `MessageView` you get from `getMessages` has a `Reactions` property which is a collection of `ReactionView`. To display reactions
you might adjust the code above for display a message to include reactions like this:

```
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
    }
    else if (message is DeletedMessageView _)
    {
        Console.WriteLine("Deleted Message");
    }
}
```

To add a reaction to a message call `AddReaction()`. This requires the conversation id and the message id, and the reaction you want to add. A reaction is an single emoji grapheme.
To delete a reaction call `RemoveReaction()` with the same parameters with which you added a reaction.


## <a name="creating">Starting a conversation</a>

To start a conversation you will need the DIDs of the conversation members, which you pass a collection to `GetConversationForMembers()`. If the user has left a conversation with these DIDs
it will be restored in the direct message list.

```c#
var memberDid = await agent.ResolveHandle("example.invalid.handle", cancellationToken);
List<Did> conversationMembers = new() { agent.Did!, bot2Did! };

var startConversation = await agent.GetConversationForMembers(conversationMembers, cancellationToken);
```

`StartConversation()` returns a `ConversationView` which includes the conversation ID, which you can then use to send messages to the chat.

## <a name="accepting">Accepting a conversation</a>

A conversation has two states, `Requested` and `Accepted`, indicated by the `Status` field in the `ConversationView`.
To accept a requested conversation use the `AcceptConversation` method and pass the `conversationId` of the conversation you wish to accept.

## <a name="leaving">Leaving a conversation</a>

To leave a conversation call `LeaveConversation()` with the conversation identifier.
