// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.Authentication;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

using Samples.BlazorAuthentication;
using Samples.BlazorAuthentication.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataProtection().SetApplicationName("Bluesky.BlazorAuthentication");
builder.Services
    .AddAuthentication(BlueskyAuthenticationDefaults.AuthenticationScheme)
    .AddBluesky(options =>
    {
        options.LoginPath = "/Bluesky/Login";
        options.LogoutPath = "/Bluesky/Logout";
        options.AccessDeniedPath = "/Bluesky/Error";
        options.Cookie.Name = ".AspNetCore.Bluesky.Blazor";
        options.CorrelationCookie.Name = ".AspNetCore.Bluesky.Blazor.Correlation";
        options.Events.OnCheckSlidingExpiration = SessionRenewal.CheckSlidingExpiration;
    })
    .AddBlueskyAuthenticationUI();

builder.Services.PostConfigure<BlueskyAuthenticationOptions>(BlueskyAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.IdentityStore = new SessionIdentityStore(options.IdentityStore ??
        throw new InvalidOperationException("An identity store is required."));
});

builder.Services.PostConfigure<BlueskyAgentOptions>(options =>
{
    ArgumentNullException.ThrowIfNull(options.OAuthOptions);
    options.OAuthOptions.PermissionSets = [BlueskyOAuthPermissionSets.FullApp];
});

builder.Services
    .AddBlueskyOAuthClientMetadata()
    .AddBlueskyClaimsTransformer();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddScoped<AuthenticationStateProvider, CircuitAuthenticationStateProvider>();
builder.Services.AddScoped<CircuitBlueskyClient>();
builder.Services.AddSingleton<SessionRenewal>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHttpsRedirection();
    app.UseHsts();
}

var oauthClientId = new Uri(app.Services.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value.OAuthOptions!.ClientId);
if (oauthClientId.Scheme != Uri.UriSchemeHttp || oauthClientId.Host != "localhost")
{
    app.UseBlueskyOAuthClientMetadata();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.Use(async (context, next) =>
{
    // Authenticated HTML contains account data and antiforgery tokens, including after logout in another tab.
    context.Response.Headers.CacheControl = "no-cache, no-store";
    context.Response.Headers.Pragma = "no-cache";
    await next(context);
});

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapPost("/session/renew", (HttpContext context, IAntiforgery antiforgery, SessionRenewal renewal) =>
    renewal.RenewAsync(context, antiforgery));

app.Run();
