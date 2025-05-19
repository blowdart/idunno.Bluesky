// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Coravel.Invocable;

namespace Samples.Bot.Watcher
{
    internal interface IHeartBeat : IInvocable
    {
        public void Beat();
    }

    internal sealed class HeartBeat : IHeartBeat
    {
        public void Beat()
        {
            Console.Write("[");
            Console.Write(DateTimeOffset.UtcNow.ToString("s"));
            Console.Write("] ");
            Console.WriteLine("💓");
        }

        public Task Invoke()
        {
            Beat();
            return Task.CompletedTask;
        }
    }
}
