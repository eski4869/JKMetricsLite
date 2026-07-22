using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace JKMetricsLite
{
    public partial class ScreenStayStatsBehaviour
    {
        private static readonly HashSet<string> _loggedErrorContexts = new HashSet<string>();
        private static string _logOutputDir;

        private static void SetLogOutputDir(string outputDir)
        {
            _logOutputDir = outputDir;
        }

        internal static void LogError(string context, Exception exception)
        {
            if (string.IsNullOrEmpty(context) || exception == null)
            {
                return;
            }

            try
            {
                if (!_loggedErrorContexts.Add(context))
                {
                    return;
                }

                string outputDir = _logOutputDir;

                if (string.IsNullOrEmpty(outputDir))
                {
                    string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    outputDir = Path.Combine(assemblyDir, DefaultOutputFolderName);
                }

                Directory.CreateDirectory(outputDir);

                string logPath = Path.Combine(outputDir, "error.log");
                string message =
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t" +
                    context + "\t" +
                    exception.GetType().FullName + "\t" +
                    exception.Message + Environment.NewLine +
                    exception.StackTrace + Environment.NewLine + Environment.NewLine;

                File.AppendAllText(logPath, message, Encoding.UTF8);
            }
            catch
            {
            }
        }

        internal static void RecordPerformanceTiming(string name, double milliseconds)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.AddPerformanceTiming(name, milliseconds);
        }

        private void AddPerformanceTiming(string name, double milliseconds)
        {
            if (string.IsNullOrEmpty(name) || milliseconds < 0)
            {
                return;
            }

            PerformanceTiming timing;

            if (!_performanceTimings.TryGetValue(name, out timing))
            {
                timing = new PerformanceTiming();
                _performanceTimings[name] = timing;
            }

            timing.Add(milliseconds);
        }

        private void MaybeWritePerformanceLog()
        {
            _performanceLogCounter++;

            if (_performanceLogCounter < PerformanceLogIntervalFrames)
            {
                return;
            }

            _performanceLogCounter = 0;
            WritePerformanceLog();
        }

        private void WritePerformanceLog()
        {
            if (_performanceTimings.Count == 0 || string.IsNullOrEmpty(_outputDir))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_outputDir);

                string logPath = Path.Combine(_outputDir, "performance_probe.tsv");
                bool needsHeader = !File.Exists(logPath) || new FileInfo(logPath).Length == 0;
                var sb = new StringBuilder();

                if (needsHeader)
                {
                    sb.AppendLine("sampled_at\tframe\tscreen\tarea\tmetric\tcount\tavg_ms\tmax_ms");
                }

                string sampledAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var names = new List<string>(_performanceTimings.Keys);
                names.Sort(StringComparer.Ordinal);

                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    PerformanceTiming timing = _performanceTimings[name];

                    if (timing.Count <= 0)
                    {
                        continue;
                    }

                    sb.Append(sampledAt).Append('\t')
                        .Append(_totalFrames).Append('\t')
                        .Append(_lastScreen).Append('\t')
                        .Append(EscapeTsv(_lastArea)).Append('\t')
                        .Append(name).Append('\t')
                        .Append(timing.Count).Append('\t')
                        .Append(timing.AverageMilliseconds.ToString("0.0000"))
                        .Append('\t')
                        .Append(timing.MaxMilliseconds.ToString("0.0000"))
                        .AppendLine();
                }

                File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
                _performanceTimings.Clear();
            }
            catch
            {
            }
        }

        private sealed class PerformanceTiming
        {
            public int Count;
            public double TotalMilliseconds;
            public double MaxMilliseconds;

            public double AverageMilliseconds
            {
                get
                {
                    return Count <= 0 ? 0 : TotalMilliseconds / Count;
                }
            }

            public void Add(double milliseconds)
            {
                Count++;
                TotalMilliseconds += milliseconds;

                if (milliseconds > MaxMilliseconds)
                {
                    MaxMilliseconds = milliseconds;
                }
            }
        }
    }
}

