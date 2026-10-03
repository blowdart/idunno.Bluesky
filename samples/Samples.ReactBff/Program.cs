// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;

using Samples.ReactBff;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);

// Request URLs can contain OAuth codes; HTTP client diagnostics can contain token endpoint data.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.None);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.None);
builder.Logging.AddFilter("idunno", LogLevel.None);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BffSessions>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<BffSessions>());
builder.Services.Configure<BlueskyAgentOptions>(
    builder.Configuration.GetSection("BlueskyAgent"), options => options.ErrorOnUnknownConfiguration = true);
builder.Services.AddAuthentication(BffEndpoints.CookieScheme)
    .AddCookie(BffEndpoints.CookieScheme, options =>
    {
        options.Cookie.Name = "ReactBff";
        options.Cookie.HttpOnly = true;
        // HTTP loopback development only. A production app should use HTTPS and CookieSecurePolicy.Always.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = BffSessions.s_lifetime;
        options.SlidingExpiration = false;
        options.Events.OnValidatePrincipal = async context =>
        {
            var sessions = context.HttpContext.RequestServices.GetRequiredService<BffSessions>();
            BffSession? session = sessions.Find(context.Principal?.FindFirst(BffEndpoints.SessionClaim)?.Value);
            if (session is null || !sessions.IsValid(session))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(BffEndpoints.CookieScheme);
            }
        };
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    })
    .AddBluesky(options =>
    {
        options.CorrelationCookie.Name = "ReactBff-Correlation";
        // The localhost development client returns to HTTP loopback. Require Secure cookies in production.
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = BffSessions.s_lifetime;
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "ReactBff-Csrf";
    options.Cookie.HttpOnly = true;
    // Keep antiforgery protection on HTTP loopback; use CookieSecurePolicy.Always with HTTPS in production.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, BffJsonContext.Default));

var app = builder.Build();
// A production app should require HTTPS, enable HSTS and publish HTTPS OAuth client metadata.
// This sample uses the localhost development client, which needs no hosted metadata or reverse proxy.

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    SuppressDiagnosticsCallback = _ => true,
    ExceptionHandler = async context =>
    {
        Exception? exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        int status = exception is BffException failure ? failure.StatusCode : StatusCodes.Status502BadGateway;
        string message = exception is BffException known
            ? known.Message
            : "The request failed. If a write was in progress, check your account before retrying.";
        BffLog.RequestFailed(app.Logger, status);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ApiError(message));
    }
});
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    await next(context);
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapBffEndpoints();
app.MapGet("/", () => Results.File(Path.Combine(app.Environment.WebRootPath, "lib", "app", "index.html"), "text/html"));
app.Run();
