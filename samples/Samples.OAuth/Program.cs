// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.OAuth;
using idunno.Bluesky;

using var cts = new CancellationTokenSource();
{
    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    ILoggerFactory loggerFactory = LoggerFactory.Create(configure =>
    {
        configure.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "G";
            options.UseUtcTimestamp = false;
        });
        configure.SetMinimumLevel(LogLevel.Debug);
    });

    Console.Write("Please enter your Bluesky username: ");
    var userName = Console.ReadLine();

    if (string.IsNullOrEmpty(userName))
    {
        return;
    }

    using (var agent = new BlueskyAgent())
    {
        Did? did = await agent.ResolveHandle(userName, cts.Token);

        if (did is null)
        {
            Console.WriteLine("Could not resolve DID");
            return;
        }

        Uri? pds = await agent.ResolvePds(did, cts.Token);

        if (pds is null)
        {
            Console.WriteLine($"Could not resolve PDS for {did}.");
            return;
        }

        Uri? authorizationServer = await agent.ResolveAuthorizationServer(pds, cts.Token);

        if (authorizationServer is null)
        {
            Console.WriteLine($"Could not discover authorization server for {pds}.");
            return;
        }

        string clientId = "http://localhost";

        var loginUri = await agent.GetOAuthAuthorizationUri(clientId: clientId, authorizationServer: authorizationServer, redirectUri: new Uri("http://127.0.0.1"));

        Console.WriteLine($"Username:             {userName}");
        Console.WriteLine($"DID:                  {did}");
        Console.WriteLine($"PDS:                  {pds}");
        Console.WriteLine($"Authorization Server: {authorizationServer}");
        Console.WriteLine($"Login URI           : {loginUri}&login_hint={Uri.EscapeDataString(userName)}");

        Debugger.Break();

        //int postToListenOn = CallbackServer.GetRandomUnusedPort();

        await using var callbackServer = new CallbackServer(51163, loggerFactory: loggerFactory);
        Console.WriteLine($"Awaiting callback on {callbackServer.Uri}");
        string queryString = await callbackServer.WaitForCallbackAsync().ConfigureAwait(true);

        if (!string.IsNullOrEmpty(queryString))
        {
            Console.WriteLine($"Got {queryString}");
        }

        Debugger.Break();
    }
}

