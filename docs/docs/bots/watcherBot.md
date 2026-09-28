# Writing a watcher bot

Watcher bots monitor actions performed in the Bluesky network and take action based upon those actions. For example a bot might
watch for a new post containing a particular hashtag and flag that post in a CRM system so a support team could monitor them
and reply to any posts that indicate a problem.

For this example we're going to write a bot that watches for posts that contain certain keywords.

> [!NOTE]
> You must already have created an account for your bot to run as, and generated a [app password](https://bsky.app/settings/app-passwords) for that account.

## Create a .NET project and add the idunno.Bluesky NuGet package

Let's start by creating a .NET project for our bot and adding the idunno.Bluesky package.
The `StreamAsync` API used below requires idunno.Bluesky 8.0.0 or newer. Until that version is published,
the five example projects in this repository build against the SDK source directly.

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
1. In the "**Additional information**" dialog choose the Framework as .NET 10.0, uncheck the "Do not use top level statements" check box then click **Create**.
1. Under the **Project** menu Select **Manage NuGet packages**, select the *Browse* tab. Search for `idunno.Bluesky`, and click **Install**.
1. Close the **Manage NuGet packages** dialog.

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

## Listen to the Jetstream

Now let's listen to the Jetstream. The [Jetstream](https://github.com/bluesky-social/jetstream) is a streaming service that provides information on activity on the ATProto network.
It lists commits to records (for example creating a post, favoriting or unfavoriting a post, following or unfollowing a user), updates to an identity (for example changing a handle)
and account operations (for example an account takedown). It encompasses the entire ATProto network, not just Bluesky operations, so you might see [WhiteWind](https://whtwnd.com)
blog records or [Tangled](https://blog.tangled.sh/intro) collaboration messages if you watch the commit stream.
The v2 `StreamAsync` API opens a live connection when `await foreach` begins, yields `JetstreamEvent` values, and
automatically reconnects after transient disconnections. This step prints commit events; other event kinds are ignored.
`await using` disposes the client and closes the connection when the loop ends. Pressing `CTRL+C` cancels the
enumeration rather than leaving an empty loop running.

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
> You may have noticed there's no authentication. The live Jetstream, and its older sibling the Firehose, are open access. No authentication is needed. This is part
> of why Bluesky is described as a public network.

## Move to the application host model

Next, change our console application to a hosted service, using .NET's
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

1. Under the **Project** menu Select **Manage NuGet packages**, select the *Browse* tab, ensure that the Include prerelease checkbox is unchecked.
   Search for `Microsoft.Extensions.Hosting`, and click **Install**.
1. Close the **Manage NuGet packages** dialog.
1. Click on `Program.cs` and replace its contents with the following
   [!code-csharp[](code/WatcherBot/Step3/Program.cs)]
1. Press `f5` to compile and run the program to start watching the jetstream.
1. Exit the program by pressing `CTRL+C`

# [Visual Studio Code](#tab/apphost/vsCode)
1. Open the Command Palette (Ctrl + Shift + P) and then search for and select **Nuget: Add**
1. Enter `Microsoft.Extensions.Hosting` in the package search dialog and choose the latest 10.x version.
1. Click on `Program.cs` and replace its contents with the following
   [!code-csharp[](code/WatcherBot/Step3/Program.cs)]
1. Press `f5` to compile and run the program to start watching the jetstream.
1. Exit the program by pressing `CTRL+C`

---

We have moved the `await foreach` loop into `BackgroundService.ExecuteAsync` and created a host to run it.
The host supplies the stopping token and handles `CTRL+C`, so we no longer need our own console cancellation handler.
`StreamAsync` uses that token to stop the live connection during host shutdown; it also retries transient
disconnections while the host is running.

## Add a settings file to contain watch words

This time around we're going to have a setting, `WatchWords`, which will be words that the bot will watch for and react to.

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
1. Open `BotOptions.cs` in your editor of choice and change the contents to the following
   [!code-csharp[](code/WatcherBot/Step4/BotOptions.cs)]
1. Open `ValidateBotOptions.cs` in your editor of choice and change the contents to the following
   [!code-csharp[](code/WatcherBot/Step4/ValidateBotOptions.cs)]
1. Open `WatcherBot.csproj` in the editor of choice and add the following lines before the closing `</project>` 
   [!code-xml[](code/WatcherBot/Step4/Step4.csproj#L18-L22)]
1. Still in `WatcherBot.csproj` add the following lines before the closing `</project>`
   [!code-xml[](code/WatcherBot/Step4/Step4.csproj#L24-L27)]
1. Click on `Program.cs` and make the following changes
   [!code-csharp[](code/WatcherBot/Step4/Program.cs?highlight=16-21,25-50)]
1. At the command line enter `dotnet build` to make sure there aren't any mistakes.

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
1. Click on the WatcherBot project file to open it and add the following lines before the closing `</Project>`
   [!code-xml[](code/WatcherBot/Step4/Step4.csproj#L24-L27)]
1. Click on `Program.cs` and make the following changes
   [!code-csharp[](code/WatcherBot/Step4/Program.cs?highlight=16-21,25-50)]
1. Choose **File ▶ Save All**
1. In the main VS menu choose **Build ▶ Build Solution** to make sure there aren't any mistakes.

# [Visual Studio Code](#tab/settings/vsCode)

1. Right click on the WatcherBot folder in the Explorer window and choose **New File..**, then call the new file `appsettings.json`
1. Replace the generated contents with the following
   [!code-json[](code/WatcherBot/Step4/appsettings.json)]
1. Open the `WatcherBot.csproj` file to open it and add the following before the `</Project>` line
   [!code-xml[](code/WatcherBot/Step4/Step4.csproj#L18-L22)]
1. Right click on the WatcherBot folder in the Explorer window and choose **New File..**, then call the new file `BotOptions.cs`
1. Replace the generated contents with the following
   [!code-csharp[](code/WatcherBot/Step4/BotOptions.cs)]
1. Right click on the WatcherBot folder in the Explorer window and choose **New File..**, then call the new file `ValidateBotOptions.cs`
1. Replace the generated contents with the following
   [!code-csharp[](code/WatcherBot/Step4/ValidateBotOptions.cs)]
1. Open `WatcherBot.csproj` add the following lines before the closing `</project>`
   [!code-xml[](code/WatcherBot/Step4/Step4.csproj#L24-L27)]
1. Click on `Program.cs` and make the following changes
   [!code-csharp[](code/WatcherBot/Step4/Program.cs?highlight=16-21,25-50)]
1. Choose **File ▶ Save All**
1. Open the Command Palette (Ctrl + Shift + P) and search for, and select **.NET: build** to make sure there aren't any mistakes.

---

This step binds the watch words to `BotOptions`; the worker starts using them in the next step.

## Examine and react to new Bluesky posts that contain a watch word

Jetstream commit events can be limited by collection [NSIDs](../commonTerms.md#records) or by the [DID](../commonTerms.md#dids) of the actor performing the event.
As we want to watch for posts we will limit commit events to the `app.bsky.feed.post` collection.
Collection filtering does not exclude identity, account, or sync events, so the bot still checks for a
`JetstreamCommitEvent` with a create operation and a record before trying to deserialize a `Post`.
The options monitor reads the current watch words from `appsettings.json`; it can supply updated values
without restarting the worker.

# [Command Line](#tab/limitJetstreamToPosts/commandLine)
1. Open `Program.cs` and change the `Worker` class to take `IOptionsMonitor<BotOptions>` through its primary constructor.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L30)]
1. In `ExecuteAsync`, filter for new post commits, deserialize each record, and compare its text with the watch words.
   Use `StreamAsync` with the host's stopping token.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L32-L76)]
1. Save `Program.cs`
1. At the command line enter `dotnet run` and watch for posts containing your watch words. Press `CTRL+C` to stop.

# [Visual Studio](#tab/limitJetstreamToPosts/visualStudio)

1. Open `Program.cs` and give `Worker` a primary constructor that takes `IOptionsMonitor<BotOptions>`.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L30)]
1. In `ExecuteAsync`, filter for new post commits, deserialize each record, and compare its text with the watch words.
   Use `StreamAsync` with the host's stopping token.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L32-L76)]
1. Choose **File ▶ Save All**
1. Press `F5` to compile and run the program, and watch for matching posts. Stop debugging to exit.

# [Visual Studio Code](#tab/limitJetstreamToPosts/vsCode)

1. Open `Program.cs` and give `Worker` a primary constructor that takes `IOptionsMonitor<BotOptions>`.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L30)]
1. In `ExecuteAsync`, filter for new post commits, deserialize each record, and compare its text with the watch words.
   Use `StreamAsync` with the host's stopping token.
   [!code-csharp[](code/WatcherBot/Step5/Program.cs#L32-L76)]
1. Choose **File ▶ Save All**
1. Press `F5` to compile and run the program, and watch for matching posts. Stop debugging to exit.

---

Now that you have your bot detecting posts with your watched words in them you can expand it to flag the post for examination, or even to send a reply to the
post, or whatever else you want to do.

`StreamAsync` reconnects after transient interruptions and suppresses the repeated event from its inclusive
cursor during this run. It does not persist a cursor between restarts; after a restart this tutorial bot starts
at the live tail, so it can miss posts while stopped. For durable processing, save the sequence **after**
handling each event and see [resuming where you left off](../jetstream.md#resuming-where-you-left-off).
An expired live cursor is surfaced rather than skipped; use [archive-to-live replay](../jetstreamReplay.md#replay-into-the-live-tail)
if you need to recover beyond the live service's lookback window.
The example checks for case-insensitive substrings in post text, not word boundaries.

> [!TIP]
> If you want to watch for hash tags, or links to web sites, or mentions you should look at the
`Post`'s [facets](https://docs.bsky.app/docs/advanced-guides/post-richtext), not the post text.
