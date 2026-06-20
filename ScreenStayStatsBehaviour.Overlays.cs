using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace JKMetricsLite
{
    public partial class ScreenStayStatsBehaviour
    {
        private const string AreaNameSplitTemplateName = "area_name_splits.html";
        private const string AreaNumberSplitTemplateName = "area_number_splits.html";
        private const string AreaNameSplitSpeedrunTemplateName = "area_name_splits_speedrun.html";
        private const string AreaNumberSplitSpeedrunTemplateName = "area_number_splits_speedrun.html";
        private const string ScreenGraphTemplateName = "progress_graph.html";
        private const string RecapTemplateName = "recap.html";
        private const string TenTimesMetricsTemplateName = "ten_times_metrics.html";

        private static void WriteOverlayHtmlIfMissing(string outputDir, string fileName, string html)
        {
            string path = Path.Combine(outputDir, fileName);

            if (File.Exists(path))
            {
                return;
            }

            File.WriteAllText(path, html, Encoding.UTF8);
        }

        private static string LoadOverlayTemplate(string fileName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string suffix = ".Templates." + fileName;
            string resourceName = null;

            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    resourceName = name;
                    break;
                }
            }

            if (resourceName == null)
            {
                throw new InvalidOperationException("Overlay template resource not found: " + fileName);
            }

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Overlay template resource could not be opened: " + fileName);
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
