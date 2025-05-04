# <a name="logging">Logging</a>

The Bluesky agent provides logging through the standard [.NET logging interface](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line).

The log messages come in three categories, the `BlueskyAgent` category for operations specific to Bluesky, such as reading the timeline,
the `AtProtoAgent` category for operations which are not Bluesky specific and could be used against any AT Proto server, and the
`DirectoryAgent` category for operations performed by the DirectoryAgent class for any [DID PLC Directory](https://web.plc.directory/) operations.

To configure a `LoggerFactory` for the agent, set the `LoggerFactory`property  in the `AtProtoAgentOptions` when creating an agent.

## <a name="configuring">Configuring logging</a>

The following code illustrates using the console logger to log messages from an AtProtoAgent.

```c#
// Setup console logging, and configure the log level to Information
using ILoggerFactory loggerFactory = LoggerFactory.Create(configure =>
{
    configure.AddConsole();
    configure.SetMinimumLevel(LogLevel.Debug);
});

using var agent = new BlueSkyAgent(
    options: new BlueskyAgentOptions()
    {
        LoggerFactory = loggerFactory
    }))
{
    await agent.Login(handle, password);
    await agent.Logout();
}
```

> [!IMPORTANT]
> Depending on the regulatory environment you are working under logs may contain personal information such as a user's DID.
> Make sure your logs are secured appropriately.
