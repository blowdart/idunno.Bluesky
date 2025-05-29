# Writing your first bot

Bots are accounts on the network that post automatically. Popular ones include bots that post the magnitude of recent earthquakes, traffic alerts, etc.

Let's write a simple bot that posts every fifteen minutes. We're choosing to implement the bot as a command line application, so it could eventually live inside a docker container,
or ran in something like a Digital Ocean droplet easily.

> [!NOTE]
> You must already have created an account for your bot to run as, and generated a [app password](https://bsky.app/settings/app-passwords) for that account.

## Create a .NET project and add the idunno.Bluesky nuget package

Let's start by creating a .NET project for our bot and adding the idunno.Bluesky package.

# [Command Line](#tab/commandLine)

1. In a PowerShell console window run the following commands
   ```PowerShell
   dotnet new console -n BlueskyBot
   cd BlueskyBot
   dotnet add package idunno.Bluesky --prerelease
   ```

# [Visual Studio](#tab/visualStudio)

1. Create a new .NET Command Line project by opening the File menu, and choosing **New ▶ Project**.
1. In the "**Create a new project**" dialog select C# as the language, choose **Console App** as the project type then click Next.
1. In the "**Configure your new project**" dialog name the project `BlueskyBot` and click Next.
1. In the "**Additional information**" dialog choose the Framework as .NET 8.0, uncheck the "Do not use top level statements" check box then click **Create**.
1. Under the **Project** menu Select **Manage nuget packages**, select the *Browse* tab, ensure that the Include prelease checkbox is checked. Search for `idunno.Bluesky`, and click **Install**.
1. Close the **Manage nuget packages** dialog.

# [Visual Studio Code](#tab/vsCode)

First configure VS Code to [allow pre-release nuget packages](https://code.visualstudio.com/docs/csharp/package-management#_include-prerelease-package-versions).

1. Create a new .NET Command Line project by opening the Command Palette (**Ctrl + Shift + P**) and then search for and select **.NET New Project**
1. In the Create a new .NET Project template search for and select **Console App**
1. Select the folder you want to save your project in
1. Name your project `BlueskyBot`
1. Choose the solution format you prefer.
1. Press **Enter** to create the solution.
1. Select the `BlueskyBot.csproj` file in Explorer window.
1. Open the Command Palette (Ctrl + Shift + P) and then search for and select **Nuget: Add**
1. Enter `idunno.Bluesky` in the package search dialog and choose the latest version.

---

## Add nuget packages for scheduling

Now add the [Coravel](https://docs.coravel.net/) nuget package to provide the scheduling.

# [Command Line](#tab/coravel/commandLine)

1. In a console window run the following commands
   ```PowerShell
   dotnet add package Coravel
   dotnet add package Microsoft.Extensions.Hosting
   ```
1. Open `BlueskyBot.csproj` in the editor of your choice and add add the following before the `</Project>` line
   [!code-xml[](code/BlueskyBot/BlueskyBot.csproj#L21-L25)]

# [Visual Studio](#tab/coravel/visualStudio)

1. Under the **Project** menu Select **Manage nuget packages**, select the *Browse* tab, search for `Coravel`, and click **Install**.
1. Search for `Microsoft.Extensions.Hosting`, and click **Install**.
1. Close the **Manage nuget packages** dialog.
1. Choose **File ▶ Save All**
1. Open `BlueskyBot.csproj` in the editor of your choice and add add the following before the `</Project>` line
   [!code-xml[](code/BlueskyBot/BlueskyBot.csproj#L21-L25)]

# [Visual Studio Code](#tab/coravel/vsCode)

1. Open the Command Palette (Ctrl + Shift + P) then search for and select **Nuget: Add**
1. Enter `Coravel` in the package search dialog and choose the latest version.
1. Open the Command Palette (Ctrl + Shift + P) then search for and select **Nuget: Add**
1. Enter `Microsoft.Extensions.Hosting` in the package search dialog and choose the latest version.
1. Open `BlueskyBot.csproj` in the editor of your choice and add add the following before the `</Project>` line
   [!code-xml[](code/BlueskyBot/BlueskyBot.csproj#L21-L25)]
---

## Add a settings file

Now we have all our dependencies lined up we'll add a settings file and a class for the settings file.

# [Command Line](#tab/settings/commandLine)

1. If you are using Windows run the following commands in PowerShell
   ```Powershell
   New-Item -Path . -Name "appsettings.json"
   New-Item -Path . -Name "BotSettings.cs"
   New-Item -Path . -Name "ValidateBotSettings.cs"
   ```

   If you are using Linux or MacOS run the following commands 
   ```bash
   touch appsettings.json
   touch BotSettings.cs
   touch ValidateBotSettings.cs
   ```

1. Open `appsettings.json` in your editor of choice and add the following, replacing
   **<yourAccountHandle>** with the handle of your bot account.
   [!code-json[](code/BlueskyBot/appsettings.json?highlight=3)]
1. Open `BotSettings.cs` in your editor of choice and and change the contents to the following
   [!code-csharp[](code/BlueskyBot/BotSettings.cs)]
1. Open `ValidateBotSettings.cs` in your editor of choice and and change the contents to the following
   [!code-csharp[](code/BlueskyBot/ValidateBotSettings.cs)]
1. Open `BlueskyBot.csproj` in the editor of choice and add the following lines before the closing `</project>` 
   [!code-xml[](code/BlueskyBot/BlueskyBot.csproj#L27-L31)]
1. Still in `BlueskyBot.csproj` add the following lines before the closing `</project>`
   [!code-xml[](code/BlueskyBot/BlueskyBot.csproj#L33-L35)]
1. Run `dotnet build` to make sure there aren't any mistakes.

# [Visual Studio](#tab/settings/visualStudio)

1. Right click on the `BlueskyBot.csproj` file in Solution explorer and choose **Add ▶ New Item**.
1. Search for and select `JSON file`
1. In the Name input box enter `appsettings.json`
1. Replace the generated contents with the following, replacing
   **<yourAccountHandle>** with the handle of your bot account.
   [!code-json[](code/BlueskyBot/appsettings.json?highlight=3)]
1. Right click on `appsettings.json` in Solution Explorer and choose **Properties**
1. Change the `Copy to Output Directory` to `Copy always`. Close the **Properties** dialog.
1. Right click on the `BlueskyBot.csproj` file in Solution Explorer and choose **Add ▶ Class**.
1. In the name input box enter `BotSettings.cs`
1. Replace the generated contents with the following
   [!code-csharp[](code/BlueskyBot/BotSettings.cs)]
1. In the name input box enter `ValidateBotSettings.cs`
1. Replace the generated contents with the following
   [!code-csharp[](code/BlueskyBot/ValidateBotSettings.cs)]
1. Click on the BlueskyBot project file to open it and add the following lines before the closing `</project>`
   [!code-xml[](code/BlueskyBot/BlueskyBot.csproj#L33-L35)]
1. Choose **File ▶ Save All**
1. In the main VS menu choose choose **Build ▶ Build Solution** to make sure there aren't any mistakes.
 
# [Visual Studio Code](#tab/settings/vsCode)

1. Right click on the BlueskyBot folder in the Explorer window and choose **New File..**, then call the new file `appsettings.json`
1. Enter the following, replacing
   **<yourAccountHandle>** with the handle of your bot account.
   [!code-json[](code/BlueskyBot/appsettings.json?highlight=3)]
1. Open the `BlueskyBot.csproj` file to open it and add the following before the `</Project>` line
   [!code-xml[](code/BlueskyBot/BlueskyBot.csproj#L27-L31)]
1. Right click on the BlueskyBot folder in the Explorer window and choose **New File..**, then call the new file `BotSettings.cs`
1. Replace the generated contents with the following
   [!code-csharp[](code/BlueskyBot/BotSettings.cs)]
1. Right click on the BlueskyBot folder in the Explorer window and choose **New File..**, then call the new file `ValidateBotSettings.cs`
1. Replace the generated contents with the following
   [!code-csharp[](code/BlueskyBot/ValidateBotSettings.cs)]
1. Click on the `BlueskyBot.csproj` file to open it and add the following lines before the closing `</project>`
   [!code-xml[](code/BlueskyBot/BlueskyBot.csproj#L33-L35)]
1. Choose **File ▶ Save All**
1. Open the Command Palette (Ctrl + Shift + P) and search for, and select **.NET: build**

---

## Configure the app password

Now we'll use the [User Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) to store our app password,

> [!TIP]
> Secret Manager is a developer resource, it does not exist on production servers. If you moved the bot to a production server
> you could use [environment variables](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-9.0&tabs=windows#environment-variables)
> to store the bot password, or something like> Azure KeyVault with its
> [.NET configuration provider](https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration).
> Refer to your hosting providers documentation to discover your options.

# [Command Line](#tab/appPassword/commandLine)

At the command line run the following commands, replacing **<yourAppPassword>** with an app password for your bot account.
   ```Powershell
   dotnet user-secrets init
   dotnet user-secrets set "Bot:AppPassword" "<yourAppPassword>"
   ```

# [Visual Studio](#tab/appPassword/visualStudio)

1. Right click on the `BluseskyBot` project and select **Manage User Secrets**
1. Add the following on a new line between the {} brackets, replacing **<yourAppPassword>** with an app password for your bot account.
   ```json
   "Bot:AppPassword": "<yourAppPassword>"
   ```
1. Choose **File ▶ Save All**

# [Visual Studio Code](#tab/appPassword/vsCode)

1. Open a new Terminal window with **Ctrl-Shift-`**
1. In the Terminal Window enter the following commands
 replacing **<yourAppPassword>** with an app password for your bot account.
   ```Powershell
   dotnet user-secrets init --project ".\BlueskyBot\BlueskyBot.csproj"
   dotnet user-secrets set "Bot:AppPassword" "<yourAppPassword>" --project ".\BlueskyBot\BlueskyBot.csproj"
   ```

---

## Write a scheduled posting bot

Finally let's actually put some code together, and write the bot and run it.

# [Command Line](#tab/program/commandLine)

1. Open `Program.cs` in your editor of choice, delete the contents and replace them with the following code
[!code-json[](code/BlueskyBot/Program.cs)]
2. At the command line run the bot using
```Powershell
dotnet run --environment:DOTNET_ENVIRONMENT=Development
```
3. Wait until you see "Just posted" then check your bot's posts.

# [Visual Studio](#tab/program/visualStudio)
1. Open `Program.cs`, delete the contents and replace them with the following code
[!code-json[](code/BlueskyBot/Program.cs)]
1. On the Visual Studio main menu choose ** Debug  ▶ BlueskyBot Debug Properties **
1. In the **Launch Profiles** dialog enter `DOTNET_ENVIRONMENT` as the Name and `Development` as the value.
1. Close the **Launch Profiles** dialog
1. Choose **File ▶ Save All**
1. Run the project by pressing `F5`
1. Wait until you see "Just posted" then check your bot's posts.

# [Visual Studio Code](#tab/program/vsCode)
1. Open `Program.cs`, delete the contents and replace them with the following code
[!code-json[](code/BlueskyBot/Program.cs)]
1. Right click on the `BlueskyBot` folder and choose **New Folder**.
1. Name the folder `.vscode`
1. Right click on the new `.vscode` folder and choose **New File**
1. Name the new file `launch.json`
1. Click the **Add Configuration..** button
1. Replace the contents of `launch.json` with following. If you have changed the target framework to be anything
   other than .NET 9 make sure to change the program path.
[!code-json[](code/BlueskyBot/.vscode/launch.json?highlight=10)]
1. Run the project by pressing `F5`
1. Wait until you see "Just posted" then check your bot's posts.

---

> [!TIP]
> If you want to see log information from `idunno.Bluesky` you can
> [configure logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging#configure-logging-without-code)
> with the `idunno.AtProto` and `idunno.Bluesky` categories.

