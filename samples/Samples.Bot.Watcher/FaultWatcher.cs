// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Samples.Bot.Watcher
{
    internal sealed class FaultWatcher : IFaultWatcher, IDisposable
    {
        private readonly MemoryCache _faultCache;
        private volatile bool _disposed;

        public FaultWatcher(ILoggerFactory loggerFactory)
        {
            Console.WriteLine(value: "Initializing FaultWatcher");

            _faultCache = new MemoryCache(
                new MemoryCacheOptions()
                {
                    SizeLimit = 10,
                },
                loggerFactory: loggerFactory);
        }

        public int Count => _faultCache.Count;

        public void LogFault(string fault)
        {
            Console.WriteLine("Logging fault.");

            var cacheEntryOptions = new MemoryCacheEntryOptions()
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = new TimeSpan(0, 5, 0)
            };

            _faultCache.Set(
                key: DateTimeOffset.UtcNow.Ticks,
                value: fault,
                options: cacheEntryOptions);
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _faultCache.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Console.WriteLine("FaultWatcher.Dispose()");
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
