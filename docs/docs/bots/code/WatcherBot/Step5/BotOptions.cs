using System.ComponentModel.DataAnnotations;

namespace WatcherBot
{
    public sealed class BotOptions
    {
        public const string ConfigurationSectionName = "Bot";

        [Required]
        public required string[] WatchWords { get; set; }
    }
}
