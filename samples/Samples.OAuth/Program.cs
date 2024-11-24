// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.IdentityModel.Tokens;

using DnsClient;

using idunno.AtProto;
using idunno.DidPlcDirectory;


using var cts = new CancellationTokenSource();
{
    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    Console.Write("Please enter your Bluesky username: ");
    var userName = Console.ReadLine();

    if (string.IsNullOrEmpty(userName))
    {
        return;
    }

    // This function is also exposed via the BlueskyAgent class,
    // BlueskyAgent.ResolveHandle();
    // For the purposes of this demo the code is mirrored here for clarity.

    Did? did = await ResolveHandle(userName, cts.Token);

    if (did is null)
    {
        Console.WriteLine("Could not resolve DID");
        return;
    }

    // This function is also exposed via the BlueskyAgent class,
    // BlueskyAgent.ResolvePds();
    // For the purposes of this demo the code is mirrored here for clarity.

    Uri? pds = await ResolvePds(did, cts.Token);

    if (pds is null)
    {
        Console.WriteLine($"Could not resolve PDS for {did}.");
        return;
    }

    // This function is also exposed via the BlueskyAgent class,
    // BlueskyAgent.ResolveAuthorizationServer();
    // For the purposes of this demo the code is mirrored here for clarity.

    Uri? authorizationServer = await ResolveAuthorizationServer(pds, cts.Token);

    if (authorizationServer is null)
    {
        Console.WriteLine($"Could not discover authorization server for {pds}.");
        return;
    }

    Console.WriteLine($"Username:             {userName}");
    Console.WriteLine($"DID:                  {did}");
    Console.WriteLine($"PDS:                  {pds}");
    Console.WriteLine($"Authorization Server: {authorizationServer}");

    // Now we have all the information needed to kick off an OAuth authorization request.

    var dPoPJwk = GenerateDPopKey();

}

static async Task<Did?> ResolveHandle(string userName, CancellationToken cancellationToken = default)
{
    Did? did = null;

    ArgumentNullException.ThrowIfNullOrEmpty(userName);

    if (Uri.CheckHostName(userName) != UriHostNameType.Dns)
    {
        throw new ArgumentOutOfRangeException(nameof(userName), "userName is not a valid DNS name.");
    }

    LookupClient lookupClient = new(new LookupClientOptions()
    {
        ContinueOnDnsError = true,
        ContinueOnEmptyResponse = true,
        ThrowDnsErrors = false,
        Timeout = TimeSpan.FromSeconds(15),
        UseCache = true
    });


    // First try DNS lookup
    var didTxtRecordHost = $"_atproto.{userName}";
    const string didTextRecordPrefix = "did=";

    var dnsLookupResult = await lookupClient.QueryAsync(didTxtRecordHost, QueryType.TXT, QueryClass.IN, cancellationToken);
    if (!cancellationToken.IsCancellationRequested && !dnsLookupResult.HasError)
    {
        foreach (var textRecord in dnsLookupResult.Answers.TxtRecords())
        {
            foreach (var text in textRecord.Text)
            {
                if (text.StartsWith(didTextRecordPrefix, StringComparison.InvariantCulture))
                {
                    did = new Did(text.Substring(didTextRecordPrefix.Length));
                }
            }
        }
    }

    if (!cancellationToken.IsCancellationRequested && did is null)
    {
        // Fall back to /well-known/did.json
        using (var httpClient = new HttpClient())
        {
            httpClient.Timeout = TimeSpan.FromSeconds(15);
            var httpResult = await httpClient.GetStringAsync($"https://{userName}/.well-known/atproto-did", cancellationToken);
            if (!string.IsNullOrEmpty(httpResult))
            {
                did = new Did(httpResult);
            }
        }
    }

    return did;
}

static async Task<Uri?> ResolvePds(Did did, CancellationToken cancellationToken = default)
{
    Uri? pds = null;

    if (!cancellationToken.IsCancellationRequested)
    {
        var directoryAgent = new DirectoryAgent();
        var didDocumentResult = await directoryAgent.ResolveDidDocument(did, cancellationToken: cancellationToken);

        if (didDocumentResult.Succeeded && didDocumentResult.Result is not null)
        {
            var didDocument = didDocumentResult.Result;

            if (didDocument.Services is not null)
            {
                pds = didDocument.Services!.Where(s => s.Id == @"#atproto_pds").FirstOrDefault()!.ServiceEndpoint;
            }
        }
    }

    return pds;
}

static async Task<Uri?> ResolveAuthorizationServer(Uri pds, CancellationToken cancellationToken = default)
{
    Uri? authorizationServer = null;

    if (!cancellationToken.IsCancellationRequested)
    {
        Uri oauthProtectedResourceUri = new($"https://{pds.Host}/.well-known/oauth-protected-resource");

        using (var httpClient = new HttpClient())
        {
            httpClient.Timeout = TimeSpan.FromSeconds(15);

            using (var protectedResourceMetadata =
                await JsonDocument.ParseAsync(
                    await httpClient.GetStreamAsync(oauthProtectedResourceUri, cancellationToken),
                    cancellationToken: cancellationToken))
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    var authorizationServers = protectedResourceMetadata.RootElement.GetProperty("authorization_servers").EnumerateArray();

                    if (!cancellationToken.IsCancellationRequested && authorizationServers.Any())
                    {
                        var serverUri = authorizationServers.First(s => !string.IsNullOrEmpty(s.GetString())).ToString();

                        if (!string.IsNullOrEmpty(serverUri))
                        {
                            authorizationServer = new Uri(serverUri);
                        }
                    }
                }
            }
        }
    }

    return authorizationServer;
}

static string GenerateDPopKey()
{
    RsaSecurityKey rsaKey = new(RSA.Create(2048));
    JsonWebKey jwkKey = JsonWebKeyConverter.ConvertFromSecurityKey(rsaKey);
    jwkKey.Alg = "PS256";
    return JsonSerializer.Serialize(jwkKey);
}
