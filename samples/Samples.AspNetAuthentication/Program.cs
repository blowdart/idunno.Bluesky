// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;
using idunno.Bluesky.AspNet.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedMemoryCache();
builder.Services.AddAuthentication(BlueskyAuthenticationDefaults.AuthenticationScheme)
    .AddBluesky(options =>
    {
    });

builder.Services.AddProfileClaimsTransformer(options => options.CacheTimeout = new TimeSpan(0, 5, 0));

// Adding the profile claims transformer requires an IDistributedCache.
builder.Services.AddTransient<IClaimsTransformation, ProfileClaimsTransformer>();

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
