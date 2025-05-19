// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace Samples.Bot.Watcher
{
    internal interface IFaultWatcher
    {
        public void LogFault(string fault);

        public int Count { get; }
    }
}
