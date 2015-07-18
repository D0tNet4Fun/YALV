using System;
using System.Collections;
using System.Collections.Generic;
using YALV.Core.Domain;

namespace YALV.Core.Providers
{
    public abstract class AbstractEntriesProvider
    {
        protected static readonly DateTime MinDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        public IEnumerable<LogItem> GetEntries(string dataSource)
        {
            var cachedLevels = new Dictionary<string, string>();
            var cachedLoggers = new Dictionary<string, string>();
            var cachedThreads = new Dictionary<string, string>();
            var cachedHostNames = new Dictionary<string, string>();
            var cachedMachineNames = new Dictionary<string, string>();
            var cachedUserNames = new Dictionary<string, string>();
            var cachedApps = new Dictionary<string, string>();
            var cachedClasses = new Dictionary<string, string>();
            foreach (var entry in this.GetEntries(dataSource, new FilterParams()))
            {
                entry.Level = CacheOrGetCached(entry.Level, cachedLevels);
                entry.Logger = CacheOrGetCached(entry.Logger, cachedLoggers);
                entry.Thread = CacheOrGetCached(entry.Thread, cachedThreads);
                entry.HostName = CacheOrGetCached(entry.HostName, cachedHostNames);
                entry.MachineName = CacheOrGetCached(entry.MachineName, cachedMachineNames);
                entry.UserName = CacheOrGetCached(entry.UserName, cachedUserNames);
                entry.App = CacheOrGetCached(entry.App, cachedApps);
                entry.Class = CacheOrGetCached(entry.Class, cachedClasses);
                yield return entry;
            }
        }

        public abstract IEnumerable<LogItem> GetEntries(string dataSource, FilterParams filter);

        private static T CacheOrGetCached<T>(T seed, Dictionary<T, T> cache)
        {
            if (seed == null)
            {
                return default(T);
            }

            T cachedItem;
            if (cache.TryGetValue(seed, out cachedItem))
            {
                return cache[seed];
            }
            cache.Add(seed, seed);
            return seed;
        }
    }
}