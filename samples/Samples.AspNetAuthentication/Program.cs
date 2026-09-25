// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;

using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDataProtection()
    .SetApplicationName("Bluesky.AspNetAuthentication");

builder.Services
    .AddAuthentication(BlueskyAuthenticationDefaults.AuthenticationScheme)
    .AddBluesky()
    .AddBlueskyAuthenticationUI();

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
