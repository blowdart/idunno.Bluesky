# <a name="logging">Logging</a>

The Bluesky agent provides logging through the standard [.NET logging interface](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line).

You can use the logging interface to log structed log messages from the agent.

The log messages come in three categories, the `BlueskyAgent` category for operations specific to Bluesky, such as reading the timeline,
the `AtProtoAgent` category for operations which are not Bluesky specific and could be used against any AT Proto server, and the
`DirectoryAgent` category for operations performed by the DirectoryAgent class for any [DID PLC Directory](https://web.plc.directory/) operations.

As the standard `ILoggerFactory` interface is used when creating an agent either the standard dependency injection systems such as WebApplication or
GenericHost can be used to inject a logger factory into the agent, or you can create a logger factory and pass it to the agent, for applications such
as console applications.

## <a name="configuring">Configuring logging</a>

The following code illustrates using the console logger to log messages from an AtProtoAgent.

```c#
// Setup console logging, and configure the log level to Information
using ILoggerFactory loggerFactory = LoggerFactory.Create(configure =>
{
    configure.AddConsole();
    configure.SetMinimumLevel(LogLevel.Debug);
});

using var agent = new AtProtoAgent(new("https://bsky.social"), loggerFactory: loggerFactory);
{
    _ = await agent.Login(new Credentials(handle, password), cancellationToken: cancellationToken).ConfigureAwait(false);
    await agent.Logout(cancellationToken: cancellationToken).ConfigureAwait(false);
}

```
