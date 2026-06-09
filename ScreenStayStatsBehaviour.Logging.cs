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
                    outputDir = Path.Combine(assemblyDir, OutputFolderName);
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
    }
}
