using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

using SpaceEngineers.Game.ModAPI;

using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using System.Collections.Concurrent;

//profiler code based on the profiler utilies from Weapon Core https://github.com/sstixrud/WeaponCore 
namespace Zkillerproxy.SmartTurretMod
{
    internal class SmartTurretsProfiler
    {
        //set up a singleton for easy access
        public static SmartTurretsProfiler Instance { get; private set; } = new SmartTurretsProfiler();

        private ConcurrentQueue<Stopwatch> stopwatchCache = new ConcurrentQueue<Stopwatch>();
        private ConcurrentDictionary<Guid, Stopwatch> currentStopwatches = new ConcurrentDictionary<Guid, Stopwatch>();
        private ConcurrentDictionary<Guid, string> currentRunningNames = new ConcurrentDictionary<Guid, string>();
        private ConcurrentDictionary<string, SmartTurretsProfilerStats> profilerStats = new ConcurrentDictionary<string, SmartTurretsProfilerStats>();

        public Guid Start(string name)
        {
            Guid id = Guid.NewGuid();

            Stopwatch currentStopwatch = null;
            //do we have a stopwatch available?
            if(stopwatchCache.TryDequeue(out currentStopwatch) == false)
            {
                currentStopwatch = new Stopwatch();
            }

            if(profilerStats.ContainsKey(name) == false)
            {
                profilerStats.TryAdd(name, new SmartTurretsProfilerStats());
            }
            currentStopwatches.TryAdd(id, currentStopwatch);
            currentRunningNames.TryAdd(id, name);
            currentStopwatch.Start();
            return id;
        }

        public void Stop(Guid id)
        {
            
            string name = null;
            Stopwatch currentStopwatch = null;
            if ((currentStopwatches.TryRemove(id, out currentStopwatch) == true) &&
                (currentRunningNames.TryRemove(id, out name) == true) &&
                (profilerStats.ContainsKey(name) == true))
            {
                long elapsed = currentStopwatch.ElapsedMilliseconds;
                currentStopwatch.Reset();

                lock(profilerStats[name])
                {
                    profilerStats[name].Count++;
                    if (profilerStats[name].Min == -1 || profilerStats[name].Min > elapsed)
                    {
                        profilerStats[name].Min = elapsed;
                    }

                    if (profilerStats[name].Max == -1 || profilerStats[name].Max < elapsed)
                    {
                        profilerStats[name].Max = elapsed;
                    }
                    profilerStats[name].Total += elapsed;
                }
                
                //add the stopwatch to the cache
                stopwatchCache.Enqueue(currentStopwatch);

            }
        }

        public void LogStats()
        {
            foreach(string name in profilerStats.Keys)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"Profiler Stats for {name}");
                stringBuilder.AppendLine($"Min {profilerStats[name].Min}ms");
                stringBuilder.AppendLine($"Max {profilerStats[name].Max}ms");
                stringBuilder.AppendLine($"Total {profilerStats[name].Total}ms");
                //stringBuilder.AppendLine($"Average {stats.Total / stats.Count}ms");
                stringBuilder.AppendLine($"Count {profilerStats[name].Count} runs");

                MyLog.Default.WriteLine(stringBuilder.ToString());
            }
            //profilerStats.Clear();
        }

    }

    internal class SmartTurretsProfilerStats
    {
        public long Count = 0;
        public long Min = -1;
        public long Max = -1;
        public long Total = 0;
    }
}
