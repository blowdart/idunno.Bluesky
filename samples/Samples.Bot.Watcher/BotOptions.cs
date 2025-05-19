using System.ComponentModel.DataAnnotations;
using idunno.AtProto;

namespace Samples.Bot.Watcher
{
    public sealed class BotOptions
    {
        public const string ConfigurationSectionName = "Bot";

        [Required]
        public required string Handle { get; set; }

        [Required]
        public required string AppPassword { get; set; }

        public string[]? WatchDids { get; set; }

        public string[]? ExcludeDids { get; set; }

        [Required]
        public required string[] WatchWords { get; set; }

        [Required]
        public required string[] Responses { get; set; }
    }
}
