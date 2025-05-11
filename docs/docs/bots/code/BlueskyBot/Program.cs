using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Coravel;
using Coravel.Invocable;

using idunno.Bluesky;

using BlueskyBot;

// Setup an application host then
// * register and Options provider for BotSettings and bind to the config
// * register Coravel for scheduling
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<BotSettings>()
    .Bind(builder.Configuration.GetSection(BotSettings.ConfigurationSectionName));

builder.Services
    .AddScheduler()
    .AddTransient<Post>();

IHost app = builder.Build();

// As tasks run in the background and will swallow exceptions check app settings are
// available before starting the scheduler
var botSettingsService = app.Services.GetRequiredService<IOptions<BotSettings>>();
ArgumentException.ThrowIfNullOrWhiteSpace(botSettingsService.Value.Handle);
ArgumentException.ThrowIfNullOrWhiteSpace(botSettingsService.Value.AppPassword);

// Configure the schedule for posting
app.Services.UseScheduler(s =>
{
    s.Schedule<Post>()
        .EveryFiveMinutes()
        .RunOnceAtStart();
});

// And run
app.Run();


sealed class Post(IOptions<BotSettings> botSettings, ILoggerFactory loggerFactory) : IInvocable
{
    public async Task Invoke()
    {
        using (var agent = new BlueskyAgent(
            options: new BlueskyAgentOptions()
            {
                LoggerFactory = loggerFactory
            }))
        {
            await agent.Login(botSettings.Value.Handle, botSettings.Value.AppPassword);
            await agent.Post("I'm a scheduled bot post.");
            Console.WriteLine("Just posted");
        }

        return;
    }
}
