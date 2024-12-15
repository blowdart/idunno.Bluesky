# Conversations and Messages

## <a name="reading">Reading conversations and messages</a>

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

Once you've shown a user their messages you can update the read status of a conversation with `UpdateRead()` which takes the conversation id, and, optionally the message id of the last message seen.

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

## <a name="creating">Starting a conversation</a>

To start a conversation you will need the DIDs of the conversation members, which you pass a collection to `GetConversationForMembers()`. If the user has left a conversation with these DIDs it will be restored in the direct message list.

```c#
var memberDid = await agent.ResolveHandle("example.invalid.handle", cancellationToken);
List<Did> conversationMembers = new() { agent.Did!, bot2Did! };

var startConversation = await agent.GetConversationForMembers(conversationMembers, cancellationToken);
```

`StartConversation()` returns a `ConversationView` which includes the conversation ID, which you can then use to send messages to the chat.

## <a name="leaving">Leaving a conversation</a>

To leave a conversation call `LeaveConversation()` with the conversation identifier.

---

>**Chapters**
>  
>*[Table of Contents](readme.md)*
>  
>[Common Terms](commonTerms.md)  
[Timelines and Feeds](timeline.md)  
[Checking notifications](notifications.md#checkingNotifications)  
[Cursors and pagination](cursorsAndPagination.md)  
[Posting](posting.md#posting)  
[Thread Gates and Post Gates](threadGatesAndPostGates.md)  
[Labels](labels.md)  
[Conversations and Messages](conversationsAndMessages.md)  
[Changing a user's profile](profileEditing.md)  
[Saving and restoring sessions](savingAndRestoringAuthentication.md)  
[Logging](logging.md)
