using System.ComponentModel.DataAnnotations;

namespace BlueskyBot
{
    public sealed class BotSettings
    {
        public const string ConfigurationSectionName = "Bot";

        [Required]
        public required string Handle { get; set; }

        [Required]
        public required string AppPassword { get; set; }
    }
}