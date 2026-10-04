// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;

using Samples.AspNetProgressiveAuthentication;
using Samples.AspNetProgressiveAuthentication.Areas.Bluesky.Pages;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDataProtection()
    .SetApplicationName("Bluesky.AspNetProgressiveAuthentication");

builder.Services
    .AddAuthentication(BlueskyAuthenticationDefaults.AuthenticationScheme)
    .AddBluesky(options =>
    {
        options.Cookie.Name = ".AspNetCore.Bluesky.Progressive";
        options.CorrelationCookie.Name = ".AspNetCore.Bluesky.Progressive.Correlation";
        options.Events.OnSigningIn = async context =>
        {
            var previous = await context.HttpContext.AuthenticateAsync(BlueskyAuthenticationDefaults.AuthenticationScheme);
            var owner = CallbackModel.GetOwner(previous);
            context.Properties.Items.TryGetValue(ProfilePermissions.SessionKey, out string? newSession);
            if (owner is not null && owner.Session != newSession)
            {
                context.HttpContext.RequestServices.GetRequiredService<ProfileEditStore>().Invalidate(owner);
            }
        };
        options.Events.OnSigningOut = async context =>
        {
            var session = await context.HttpContext.AuthenticateAsync(BlueskyAuthenticationDefaults.AuthenticationScheme);
            var owner = CallbackModel.GetOwner(session);
            if (owner is not null)
            {
                context.HttpContext.RequestServices.GetRequiredService<ProfileEditStore>().Invalidate(owner);
            }
        };
    })
    .AddBlueskyAuthenticationUI();

builder.Services.PostConfigure<idunno.Bluesky.BlueskyAgentOptions>(ProfilePermissions.Configure);
builder.Services.AddBlueskyOAuthClientMetadata(options =>
{
    foreach (string scope in ProfilePermissions.WriteScopes)
    {
        options.AdditionalScopes.Add(scope);
    }
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ProfileEditStore>();
builder.Services.AddScoped<ProfileOAuthClient>();

builder.Services
    .AddBlueskyClaimsTransformer()
    .AddTransient<IClaimsTransformation, BlueskyClaimsTransformer>()
    .AddBlueskyAgentFactory()
    .AddOpenTelemetry()
        .WithMetrics(metrics =>
        {
            metrics
            .AddAtProtoDirectoryMetrics()
            .AddAtProtoHttpClientMetrics()
            .AddBlueskyAuthenticationMetrics();
        });

builder.Services.AddRazorPages();

var app = builder.Build();

var oauthClientId = new Uri(app.Services.GetRequiredService<IOptions<idunno.Bluesky.BlueskyAgentOptions>>().Value.OAuthOptions!.ClientId);
if (oauthClientId.Scheme != Uri.UriSchemeHttp || oauthClientId.Host != "localhost")
{
    app.UseBlueskyOAuthClientMetadata();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

/// <summary>
/// Exposes the sample entry point to the test host.
/// </summary>
public partial class Program;
