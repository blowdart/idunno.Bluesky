# Writing a watcher bot

Watcher bots monitor actions performed in the Bluesky network and take action based upon those actions. For example a bot might
watch for a new post containing a particular hashtag and flag that post in a CRM system so a support team could monitor them
and reply to any posts that indicate a problem.

For this example we're going to write a bot that watches for posts that contains certain keywords.

> [!NOTE]
> You must already have created an account for your bot to run as, and generated a [app password](https://bsky.app/settings/app-passwords) for that account.

## Create a .NET project and add the idunno.Bluesky nuget package

Let's start by creating a .NET project for our bot and adding the idunno.Bluesky package.

# [Command Line](#tab/commandLine)

1. In a console run the following commands
   ```PowerShell
   dotnet new console -n WatcherBot
   cd WatcherBot
   dotnet add package idunno.Bluesky
   ```

# [Visual Studio](#tab/visualStudio)

1. Create a new .NET Command Line project by opening the File menu, and choosing **New ▶ Project**.
1. In the "**Create a new project**" dialog select C# as the language, choose **Console App** as the project type then click Next.
1. In the "**Configure your new project**" dialog name the project `WatcherBot` and click Next.
1. In the "**Additional information**" dialog choose the Framework as .NET 9.0, uncheck the "Do not use top level statements" check box then click **Create**.
1. Under the **Project** menu Select **Manage nuget packages**, select the *Browse* tab. Search for `idunno.Bluesky`, and click **Install**.
1. Close the **Manage nuget packages** dialog.

# [Visual Studio Code](#tab/vsCode)

1. Create a new .NET Command Line project by opening the Command Palette (**Ctrl + Shift + P**) and then search for and select **.NET New Project**
1. In the Create a new .NET Project template search for and select **Console App**
1. Select the folder you want to save your project in
1. Name your project `WatcherBot`
1. Choose the solution format you prefer.
1. Press **Enter** to create the solution.
1. Select the `WatcherBot.csproj` file in Explorer window.
1. Open the Command Palette (Ctrl + Shift + P) and then search for and select **Nuget: Add**
1. Enter `idunno.Bluesky` in the package search dialog and choose the latest version.

---

## Listen to the jetstream

Now let's listen to the jetstream. The [Jetstream](https://github.com/bluesky-social/jetstream) is a streaming service that provides information on activity on the ATProto network.
It lists commits to records (for example creating a post, favoriting or unfavoriting a post, following or unfollowing a user), updates to an identity (for example changing a handle)
and account operations (for example an account takedown). It encompasses the entire ATProto network, not just Bluesky operations, so you might see [WhiteWind](https://whtwnd.com)
blog records or [Tangled](https://blog.tangled.sh/intro) collaboration messages if you watch the commit stream.

# [Command Line](#tab/listen/commandLine)

1. Open `Program.cs` in the editor of your choice and replace its contents with the following
   [!code-csharp[](code/WatcherBot/Step2/Program.cs)]
1. At the command line enter `dotnet run` to start watching the jetstream.
1. Exit the program by pressing `CTRL+C`

# [Visual Studio](#tab/listen/visualStudio)

1. Click on `Program.cs` and replace its contents with the following
   [!code-csharp[](code/WatcherBot/Step2/Program.cs)]
1. Press `f5` to compile and run the program to start watching the jetstream.
1. Exit the program by pressing `CTRL+C`

# [Visual Studio Code](#tab/listen/vsCode)

1. Click on `Program.cs` and replace its contents with the following
   [!code-csharp[](code/WatcherBot/Step2/Program.cs)]
1. Run the project by pressing `F5` to start watching the jetstream.
1. Exit the program by pressing `CTRL+C`

---

> [!TIP]
> You may have noticed there's no authentication. The JetStream, and its older sibling the Firehose, are open access. No authentication is needed. This is part
> of why Bluesky is described as a public network.

## Move to the application host model

Now we have our watch words configured we need to change our console application a hosted service, using .NET's
[HostApplicationBuilder](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host?tabs=appbuilder). This will allow
us to use configuration, application startup and shutdown and dependency injection (DI).

# [Command Line](#tab/apphost/commandLine)

1. In a console run the following commands
   ```PowerShell
   dotnet add package Microsoft.Extensions.Hosting
   ```
1. Open up `Program.cs` and replace the contents with the following
   [!code-csharp[](code/WatcherBot/Step3/Program.cs)]
1. Save `program.cs` and at the command line enter `dotnet run` to start watching the jetstream.
1. Exit the program by pressing `CTRL+C`

# [Visual Studio](#tab/apphost/visualStudio)

1. Under the **Project** menu Select **Manage nuget packages**, select the *Browse* tab, ensure that the Include prelease checkbox is unchecked.
   Search for `Microsoft.Extensions.Hosting`, and click **Install**.
1. Close the **Manage nuget packages** dialog.
1. Click on `Program.cs` and replace its contents with the following
   [!code-csharp[](code/WatcherBot/Step3/Program.cs)]
1. Press `f5` to compile and run the program to start watching the jetstream.
1. Exit the program by pressing `CTRL+C`

# [Visual Studio Code](#tab/apphost/vsCode)
1. Open the Command Palette (Ctrl + Shift + P) and then search for and select **Nuget: Add**
1. Enter `Microsoft.Extensions.Hosting` in the package search dialog and choose the latest 9.x version.
1. Click on `Program.cs` and replace its contents with the following
   [!code-csharp[](code/WatcherBot/Step3/Program.cs)]
1. Press `f5` to compile and run the program to start watching the jetstream.
1. Exit the program by pressing `CTRL+C`

---

What we've done is move the code to watch the jetstream to into a `BackgroundService`, and then created a host to contain that service and run it.
You get the `CTRL-C` close functionality for free with a `HostApplicationBuilder` so that code has been removed.

## Add a settings file to contain watch words

This time around we're going to have an setting, `WatchWords`, which will be words that the bot will watch for and react to.

# [Command Line](#tab/settings/commandLine)

1. If you are using Windows run the following commands in PowerShell
   ```Powershell
   New-Item -Path . -Name "appsettings.json"
   New-Item -Path . -Name "BotOptions.cs"
   New-Item -Path . -Name "ValidateBotOptions.cs"
   ```

   If you are using Linux or MacOS run the following commands 
   ```bash
   touch appsettings.json
   touch BotOptions.cs
   touch ValidateBotOptions.cs
   ```

1. Open `appsettings.json` in your editor of choice and add the following with the editor of your choice
   [!code-json[](code/WatcherBot/Step4/appsettings.json)]
1. Open `BotOptions.cs` in your editor of choice and and change the contents to the following
   [!code-csharp[](code/WatcherBot/Step4/BotOptions.cs)]
1. Open `ValidateBotOptions.cs` in your editor of choice and and change the contents to the following
   [!code-csharp[](code/WatcherBot/Step4/ValidateBotOptions.cs)]
1. Open `WatcherBot.csproj` in the editor of choice and add the following lines before the closing `</project>` 
   [!code-xml[](code/WatcherBot/Step4/Step4.csproj#L17-L21)]
1. Still in `BlueskyBot.csproj` add the following lines before the closing `</project>`
   [!code-xml[](code/WatcherBot/Step4/Step4.csproj#L23-L26)]
1. Click on `Program.cs` and make the following changes
   [!code-xml[](code/WatcherBot/Step4/Program.cs?highlight=17-22,31-92)]
1. 1. At the command line enter `dotnet build` to make sure there aren't any mistakes.

# [Visual Studio](#tab/settings/visualStudio)

1. Right click on the `WatcherBot.csproj` file in Solution explorer and choose **Add ▶ New Item**.
1. Search for and select `JSON file`
1. In the Name input box enter `appsettings.json`
1. Replace the generated contents with the following
   [!code-json[](code/WatcherBot/Step4/appsettings.json)]
1. Right click on `appsettings.json` in Solution Explorer and choose **Properties**
1. Change the `Copy to Output Directory` to `Copy always`. Close the **Properties** dialog.
1. Right click on the `WatcherBot.csproj` file in Solution Explorer and choose **Add ▶ Class**.
1. In the name input box enter `BotOptions.cs`
1. Replace the generated contents with the following
   [!code-csharp[](code/WatcherBot/Step4/BotOptions.cs)]
1. In the name input box enter `ValidateBotOptions.cs`
1. Replace the generated contents with the following
   [!code-csharp[](code/WatcherBot/Step4/ValidateBotOptions.cs)]
1. Click on the BlueskyBot project file to open it and add the following lines before the closing `</project>`
   [!code-xml[](code/WatcherBot/Step4/Step4.csproj#L23-L26)]
1. Click on `Program.cs` and make the following changes
   [!code-xml[](code/WatcherBot/Step4/Program.cs?highlight=17-22,31-92)]
1. Choose **File ▶ Save All**
1. In the main VS menu choose choose **Build ▶ Build Solution** to make sure there aren't any mistakes.

# [Visual Studio Code](#tab/settings/vsCode)

1. Right click on the WatcherBot folder in the Explorer window and choose **New File..**, then call the new file `appsettings.json`
1. Replace the generated contents with the following
   [!code-json[](code/WatcherBot/Step4/appsettings.json)]
1. Open the `WatcherBot.csproj` file to open it and add the following before the `</Project>` line
   [!code-xml[](code/WatcherBot/Step4/Step4.csproj#L17-L21)]
1. Right click on the WatcherBot folder in the Explorer window and choose **New File..**, then call the new file `BotOptions.cs`
1. Replace the generated contents with the following
   [!code-csharp[](code/WatcherBot/Step4/BotOptions.cs)]
1. Right click on the WatcherBot folder in the Explorer window and choose **New File..**, then call the new file `ValidateBotOptions.cs`
1. Replace the generated contents with the following
   [!code-csharp[](code/WatcherBot/Step4/ValidateBotOptions.cs)]
1. Open `WatcherBot.csproj` add the following lines before the closing `</project>`
   [!code-xml[](code/WatcherBot/Step4/Step4.csproj#L23-L26)]
1. Click on `Program.cs` and make the following changes
   [!code-xml[](code/WatcherBot/Step4/Program.cs?highlight=17-22,31-92)]
1. Choose **File ▶ Save All**
1. Open the Command Palette (Ctrl + Shift + P) and search for, and select **.NET: build** to make sure there aren't any mistakes.

---

## Examine and react to new Bluesky posts that contain a watch word

Jetstream commit events can be limited by collection [NSIDs](../commonTerms.md#records) or by the [DID](../commonTerms.md#dids) of the actor performing the event.
As we want to watch for posts we will limit the jetstream to only tell us about commits to post collections, which have an NSID of `app.bsky.feed.post`.

# [Command Line](#tab/limitJetstreamToPosts/commandLine)
1. Open `Program.cs` in the editor of your choice and change the `Worker` class initialization of `_jetstream` to add a parameter, `collections`, which will limit
   the commit events raised to events in the Bluesky posts collection.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L35)]
1. Now change the `Worker` class to have a primary constructor that takes `IOptionsMonitor<BotOptions>` parameter. This parameter will be injected by the host.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L33)]
1. Finally build out `OnRecordReceived` so
    1. Looks for a create operation, indicating a new record has been created in an `app.bsky.feed.post` collection
    1. Try to convert the `Record` in `CommitEventArgs` to a `Post`
    1. If the conversion is successful, scans the post text, if the post has any text for the watched phases from our `appsettings.json`.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L38-L81)]
1. Save `Program.cs`
1. At the command line enter `dotnet run` to make sure there aren't any mistakes and watch the console output to see when posts are made on Bluesky that contain your watch words.

# [Visual Studio](#tab/limitJetstreamToPosts/visualStudio)

1. Open `Program.cs` and change the `Worker` class initialization of `_jetstream` to add a parameter, `collections`, which will limit
   the commit events raised to events in the Bluesky posts collection.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L35)]
1. Now change the `Worker` class to have a primary constructor that takes `IOptionsMonitor<BotOptions>` parameter. This parameter will be injected by the host.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L33)]
1. Finally build out `OnRecordReceived` so
    1. Looks for a create operation, indicating a new record has been created in an `app.bsky.feed.post` collection
    1. Try to convert the `Record` in `CommitEventArgs` to a `Post`
    1. If the conversion is successful, scans the post text, if the post has any text for the watched phases from our `appsettings.json`.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L38-L81)]
1. Choose **File ▶ Save All**
1. Press `f5` to compile and run the program, and watch the console output to see when posts are made on Bluesky that contain your watch words.

# [Visual Studio Code](#tab/limitJetstreamToPosts/vsCode)

1. Open `Program.cs` and change the `Worker` class initialization of `_jetstream` to add a parameter, `collections`, which will limit
   the commit events raised to events in the Bluesky posts collection.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L35)]
1. Now change the `Worker` class to have a primary constructor that takes `IOptionsMonitor<BotOptions>` parameter. This parameter will be injected by the host.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L33)]
1. Finally build out `OnRecordReceived` so
    1. Looks for a create operation, indicating a new record has been created in an `app.bsky.feed.post` collection
    1. Try to convert the `Record` in `CommitEventArgs` to a `Post`
    1. If the conversion is successful, scans the post text, if the post has any text for the watched phases from our `appsettings.json`.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L38-L81)]
1. Choose **File ▶ Save All**
1. Press `f5` to compile and run the program, and watch the console output to see when posts are made on Bluesky that contain your watch words.

---

Now that you have your bot detecting posts with your watched words in them you can expand it to flag the post for examination, or even to send a reply to the
post, or whatever else you want to do.

The bot will exit if its connection to the jetstream is interrupted. If you want to add retry logic for your jetstream connection see [retry logic](../jetstream.md#retry)
in [Using the Jetstream](../jetstream.md).

> [!TIP]
> If you want to watch for hash tags, or links to web sites, or mentions you should look at the
`Post`'s [facets](https://docs.bsky.app/docs/advanced-guides/post-richtext), not the post text.

