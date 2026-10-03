// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BlueskyAgentOptions>(
    builder.Configuration.GetSection("BlueskyAgent"),
    options => options.ErrorOnUnknownConfiguration = true);

builder.Services
    .AddAuthentication(BlueskyAuthenticationDefaults.AuthenticationScheme)
    .AddBluesky(options =>
    {
        options.LoginPath = "/Bluesky/Login";
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    })
    .AddBlueskyAuthenticationUI();

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddBlueskyOAuthClientMetadata();
builder.Services.AddBlueskyAgentFactory();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToPage("/Privacy");
    options.Conventions.AllowAnonymousToPage("/Terms");
});

var app = builder.Build();

// The default trusted proxies are loopback addresses, where the tunnel connector runs.
app.UseForwardedHeaders();
app.UseBlueskyOAuthClientMetadata();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorPages().WithStaticAssets();
app.Run();
