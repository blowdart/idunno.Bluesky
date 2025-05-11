// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Coravel;
using Coravel.Invocable;

using idunno.Bluesky;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddScheduler()
    .AddTransient<Post>();

var app = builder.Build();

app.Services.UseScheduler(s =>
{
    s.Schedule<Post>()
        .EveryFiveMinutes()
        .RunOnceAtStart();
});

app.Run();


sealed class Post(ILoggerFactory loggerFactory) : IInvocable
{
    public async Task Invoke()
    {
        using (var agent = new BlueskyAgent(
            options: new BlueskyAgentOptions()
            {
                LoggerFactory = loggerFactory
            }))
        {
            await agent.Login(
                identifier: Environment.GetEnvironmentVariable("_BlueskyHandle")!,
                password: Environment.GetEnvironmentVariable("_BlueskyPassword")!);
            await agent.Post("Beep beep, boop boop. 🤖");
        }

        return ;
    }
}
