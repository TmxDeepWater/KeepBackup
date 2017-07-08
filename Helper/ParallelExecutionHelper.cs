using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KeepBackup.Storage;

namespace KeepBackup.Helper
{
    static class ParallelExecutionHelper
    {
        public static void ParallelExecute<T>(this IList<T> jobs, Func<T, long> size, Func<T, string> pathLog, Func<T, string> hashLog, Action<T> perform, int degreeofParallelism)
        {
            jobs.ParallelExecute<T>(size, pathLog, hashLog, perform, degreeofParallelism, jobs.Count(), 0, jobs.Sum(x => size(x)), 0);
        }

        public static void ParallelExecute<T>(
            this IList<T> jobs,
            Func<T, long> size,
            Func<T, string> pathLog,
            Func<T, string> hashLog,
            Action<T> perform,
            int degreeofParallelism,
            int countTotal,
            int countOffset,
            long sizeTotal,
            long sizeOffset)
        {
            int countDone = 0;
            long sizeDone = 0;

            Stopwatch sw = new Stopwatch();
            Stopwatch sw2 = new Stopwatch();

            Action<T, bool> performLogged = delegate (T job, bool logpace)
            {
                perform(job);

                int countDone2 = Interlocked.Increment(ref countDone);
                long sizeDone2 = Interlocked.Add(ref sizeDone, size(job));

                double percentFiles = ((double)(countDone2 + countOffset) / (double)countTotal) * 100.0;
                double percentSize = ((double)(sizeDone2 + sizeOffset) / (double)sizeTotal) * 100.0;

                Program.log.InfoFormat("{0} / {1} ({2:0.00}%) size: {3:0.00} / {4:0.00} MB ({5:0.00}%) \"{6}\" {7}",
                    countDone2 + countOffset,
                    countTotal,
                    percentFiles,
                    (sizeDone2 + sizeOffset) / 1048576.0,
                    sizeTotal / 1048576.0,
                    percentSize,
                    pathLog(job),
                    hashLog(job)
                );

                if (logpace)
                {
                    long timeMs = sw.ElapsedMilliseconds;

                    long remainingFiles = countTotal - (countDone2 + countOffset);
                    double remainingMb = (sizeTotal - (sizeDone2 + sizeOffset)) / 1048576.0;

                    Program.log.InfoFormat("  | todo  | {0:0} files        | {1:0.00} MB",
                        remainingFiles,
                        remainingMb
                        );

                    double filesPerSec = countDone2 / (timeMs / 1000.0);
                    double mbPerSec = (sizeDone2 / 1048576.0) / (timeMs / 1000.0);                  

                    Program.log.InfoFormat("  | speed | {0:0.00} files/sec   | {1:0.00} MB/sec",
                        filesPerSec,
                        mbPerSec
                        );

                    double remainingMinutesByFiles = (remainingFiles / filesPerSec) / 60.0;
                    double remainingMinutesByMb = (remainingMb / mbPerSec) / 60.0;

                    Program.log.InfoFormat("  | time  | {0:00}h:{1:00}min by files | {2:00}h:{3:00}min by size",
                        (int)remainingMinutesByFiles / 60,
                        (int)remainingMinutesByFiles % 60,
                        (int)remainingMinutesByMb / 60,
                        (int)remainingMinutesByMb % 60
                        );
                }
            };

            Action<int> performLoggedByIndex = delegate (int x)
            {
                bool pace = false;
                if (sw2.ElapsedMilliseconds > 15000)
                {
                    pace = true;
                    sw2.Restart();
                }

                T job = jobs[x];
                performLogged(job, pace);
            };

            ParallelOptions pl = new ParallelOptions() { MaxDegreeOfParallelism = degreeofParallelism };

            Program.log.InfoFormat("starting - {0} in parallel", degreeofParallelism);

            sw.Start();
            sw2.Start();
            Parallel.For(0, jobs.Count, pl, performLoggedByIndex);
        }


    }
}
