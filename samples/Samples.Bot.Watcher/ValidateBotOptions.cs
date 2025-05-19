// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Options;

namespace Samples.Bot.Watcher
{
    [OptionsValidator]
    public partial class ValidateBotOptions : IValidateOptions<BotOptions>
    {
    }
}
