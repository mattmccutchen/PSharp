//-----------------------------------------------------------------------
// <copyright file="PCTSchedulingStrategy.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
//      EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
//      OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
// ----------------------------------------------------------------------------------
//      The example companies, organizations, products, domain names,
//      e-mail addresses, logos, people, places, and events depicted
//      herein are fictitious.  No association with any real company,
//      organization, product, domain name, email address, logo, person,
//      places, or events is intended or should be inferred.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.PSharp.Scheduling;
using Microsoft.PSharp.Tooling;

namespace Microsoft.PSharp.DynamicAnalysis
{
    /// <summary>
    /// Class representing the Probabilistic Concurrency Testing strategy.
    /// http://research.microsoft.com/pubs/118655/asplos277-pct.pdf
    /// </summary>
    public sealed class PCTSchedulingStrategy : ISchedulingStrategy
    {
        // XXX Factor out common parts with RandomSchedulingStrategy to a superclass.
        /// <summary>
        /// Nondeterminitic seed.
        /// </summary>
        private int Seed;

        private int Iteration;

        /// <summary>
        /// Randomizer.
        /// </summary>
        private Random Random;

        // TODO: Can we use Configuration.DepthBound for k?
        // It's almost what we want, but it doesn't count a machine becoming
        // idle as a scheduling point, whereas we want to count it.  Maybe the
        // meaning of Configuration.DepthBound should change to match us.
        private int k, d;

        // Chosen at beginning of iteration:
        private Dictionary<int, int> PriorityChangePoints;  // instruction number => new priority

        // Updated as we go:
        private int SchedulingPoints;
        private MachineId RunningMachineId;
        // Add new machines as we see them.  This is equivalent to original PCT,
        // but the user doesn't have to specify n.
        private HashSet<MachineId> MachineIdsSeen;
        private List<MachineId> MachineIdsByOriginalPriority;
        private Dictionary<MachineId, int> ChangedMachinePriorities;  // machine id => new priority

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="seed">Seed</param>
        public PCTSchedulingStrategy(int seed)
        {
            string kStr, dStr;
            if (!Configuration.SchedulingParams.TryGetValue("k", out kStr))
                ErrorReporter.ReportAndExit("/sch-param:k must be specified to use the PCT strategy.");
            if (!int.TryParse(kStr, out k) && k > 0)
            {
                ErrorReporter.ReportAndExit("PCT parameter k must be a positive integer.");
            }
            if (!Configuration.SchedulingParams.TryGetValue("d", out dStr))
                ErrorReporter.ReportAndExit("/sch-param:d must be specified to use the PCT strategy.");
            if (!int.TryParse(dStr, out d) && d > 0)
            {
                ErrorReporter.ReportAndExit("PCT parameter d must be a positive integer.");
            }

            this.Seed = seed;
            this.Iteration = 0;
        }

        void MaybeInitIteration()
        {
            if (this.Random == null)
            {
                int iterationSeed = this.Seed + this.Iteration;
                Output.Debug(DebugType.Runtime,
                    "<StrategyLog> PCTSchedulingStrategy starting iteration with seed {0}.",
                    iterationSeed);
                this.Random = new Random(iterationSeed);

                PriorityChangePoints = new Dictionary<int, int>();
                for (int i = 0; i < d - 1; i++)
                {
                    // This may overwrite; we don't care.
                    PriorityChangePoints[Random.Next(k)] = d;
                }
                MachineIdsSeen = new HashSet<MachineId>();
                MachineIdsByOriginalPriority = new List<MachineId>();
                ChangedMachinePriorities = new Dictionary<MachineId, int>();
            }
        }

        /// <summary>
        /// Returns the next machine to schedule.
        /// </summary>
        /// <param name="next">Next</param>
        /// <param name="machines">Machines</param>
        /// <returns>Boolean value</returns>
        bool ISchedulingStrategy.TryGetNext(out TaskInfo next, List<TaskInfo> tasks)
        {
            MaybeInitIteration();

            if (RunningMachineId != null)
            {
                int newPriority;
                if (PriorityChangePoints.TryGetValue(SchedulingPoints, out newPriority))
                    ChangedMachinePriorities[RunningMachineId] = newPriority;
                SchedulingPoints++;
            }

            var enabledTasks = tasks.Where(task => task.IsEnabled).ToList();
            if (enabledTasks.Count == 0)
            {
                next = null;
                return false;
            }

            int highestPriority = -1;
            TaskInfo highestPriorityTask = null;
            foreach (TaskInfo task in enabledTasks)
            {
                MachineId mid = task.Machine.Id;
                if (!MachineIdsSeen.Contains(mid))
                {
                    MachineIdsSeen.Add(mid);
                    MachineIdsByOriginalPriority.Insert(Random.Next(MachineIdsByOriginalPriority.Count + 1), mid);
                }
                int priority;
                if (!ChangedMachinePriorities.TryGetValue(mid, out priority))
                    // XXX Calling IndexOf for each machine at each scheduling point
                    // is potentially slow.  We can do better, but not bothering yet.
                    priority = (d - 1) + MachineIdsByOriginalPriority.IndexOf(mid);
                if (priority > highestPriority)
                {
                    highestPriority = priority;
                    highestPriorityTask = task;
                }
            }

            next = highestPriorityTask;
            RunningMachineId = next.Machine.Id;
            return true;
        }

        /// <summary>
        /// Returns the next choice.
        /// </summary>
        /// <param name="next">Next</param>
        /// <returns>Boolean value</returns>
        bool ISchedulingStrategy.GetNextChoice(out bool next)
        {
            MaybeInitIteration();
            next = false;
            if (this.Random.Next(2) == 1)
            {
                next = true;
            }

            return true;
        }

        /// <summary>
        /// Returns true if the scheduling has finished.
        /// </summary>
        /// <returns>Boolean value</returns>
        bool ISchedulingStrategy.HasFinished()
        {
            return false;
        }

        /// <summary>
        /// Returns a textual description of the scheduling strategy.
        /// </summary>
        /// <returns>String</returns>
        string ISchedulingStrategy.GetDescription()
        {
            return "PCT (seed is " + this.Seed + ")";
        }

        /// <summary>
        /// Resets the scheduling strategy.
        /// </summary>
        void ISchedulingStrategy.Reset()
        {
            this.Iteration++;
            this.Random = null;

            // XXX This is silly; combine these in an object we can recreate?
            PriorityChangePoints = null;
            SchedulingPoints = 0;
            RunningMachineId = null;
            MachineIdsSeen = null;
            MachineIdsByOriginalPriority = null;
            ChangedMachinePriorities = null;
        }
    }
}
