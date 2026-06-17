using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace JKMetricsLite
{
    public partial class ScreenStayStatsBehaviour
    {
        private const string AreaNameSplitTemplateName = "AreaNameSplit.html";
        private const string AreaNumberSplitTemplateName = "AreaNumberSplit.html";
        private const string AreaNameSplitSpeedrunTemplateName = "AreaNameSplitSpeedrun.html";
        private const string AreaNumberSplitSpeedrunTemplateName = "AreaNumberSplitSpeedrun.html";
        private const string ScreenGraphTemplateName = "ScreenGraph.html";
        private const string RecapTemplateName = "Recap.html";

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
