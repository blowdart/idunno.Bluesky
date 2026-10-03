// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BlueskyAgentOptions>(
    builder.Configuration.GetSection("BlueskyAgent"),
    options => options.ErrorOnUnknownConfiguration = true);

builder.Services.AddBlueskyOAuthClientMetadata();

var app = builder.Build();

app.UseBlueskyOAuthClientMetadata();
app.MapGet("/", () => Results.Redirect("/oauth-client-metadata.json"));

app.Run();
