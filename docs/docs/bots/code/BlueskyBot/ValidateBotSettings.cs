// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Options;

namespace BlueskyBot
{
    [OptionsValidator]
    public partial class ValidateBotSettings : IValidateOptions<BotSettings>
    {
    }
}
