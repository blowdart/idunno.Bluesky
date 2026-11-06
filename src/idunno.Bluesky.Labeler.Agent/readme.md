# idunno.Bluesky.Labeler.Agent

## About

`idunno.Bluesky.Labeler.Agent` is a client library for bots that issue or retract labels through a
separately hosted AT Protocol labeler. It uses an authenticated `AtProtoAgent` to obtain short-lived,
method-bound service credentials from the bot's PDS; the labeler host remains responsible for team
authorization, policy enforcement, signing and durable storage.

## Key Features

* Remote label issuance and negation through an authenticated account agent
* Short-lived, method-bound service credentials with a separate issuance audience
* Durable operation-receipt lookup and caller-supplied retry identifiers
* Public applied-label queries without forwarding account credentials
* Typed labeler errors through AT Protocol HTTP results
* Support for owned or borrowed account agents

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

You need a running labeler host with issuance enabled and your account granted the required team permissions; see the [hosting guide](https://bluesky.idunno.dev/docs/labeler/hosting.html) for setup.

Configure the labeler service URI and its issuer DID. The builder creates the
`labeler_issuance` service audience; callers do not need to construct the service reference:

```csharp
using idunno.AtProto;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Agent;

Did labelerDid = new("did:web:labeler.example");

using LabelerAgent labeler = LabelerAgentBuilder.Create()
    .WithLabelerService(new Uri("https://labeler.example/"), labelerDid)
    .ConfigureAccountAgent(agent => agent.ForService(new Uri("https://bsky.social")))
    .Build();

AtProtoHttpResult<bool> login = await labeler.AccountAgent.Login(
    identifier: "bot.example",
    password: password,
    cancellationToken: cancellationToken);
if (!login.Succeeded)
{
    throw new AtProtoHttpRequestException("The bot could not log in.");
}

AtProtoHttpResult<IReadOnlyList<string>> available =
    await labeler.GetAvailableLabels(cancellationToken: cancellationToken);
```

Use `IssueLabel` and `NegateLabel` to issue and remove labels.

## Related Packages

* [idunno.AtProto](https://www.nuget.org/packages/idunno.AtProto) provides the account agent and AT Protocol client.
* [idunno.Bluesky](https://www.nuget.org/packages/idunno.Bluesky) provides a Bluesky account agent that this client can borrow.
* [idunno.Bluesky.AspNet.Labeler](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Labeler) provides hosting and authorization for the labeler service.

## Documentation

See the [labeler agent guide](https://bluesky.idunno.dev/docs/labeler/agent.html) for complete
configuration, sample commands, retry behavior, permissions and error handling.
