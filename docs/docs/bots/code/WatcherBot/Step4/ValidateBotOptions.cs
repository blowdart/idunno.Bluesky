using Microsoft.Extensions.Options;

namespace WatcherBot
{
    [OptionsValidator]
    public partial class ValidateBotOptions : IValidateOptions<BotOptions>
    {
    }
}
