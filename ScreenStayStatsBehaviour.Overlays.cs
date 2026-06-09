using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace JKMetricsLite
{
    public partial class ScreenStayStatsBehaviour
    {
        private const string AreaNameTemplateName = "AreaName.html";
        private const string AreaNoTemplateName = "AreaNo.html";
        private const string AreaNameSpeedrunTemplateName = "AreaNameSpeedrun.html";
        private const string ScreenTimelineTemplateName = "ScreenTimeline.html";

        private void WriteAreaNameOverlayHtml()
        {
            WriteOverlayHtml("area_name.html", LoadOverlayTemplate(AreaNameTemplateName));
        }

        private void WriteAreaNoOverlayHtml()
        {
            WriteOverlayHtml("area_no.html", LoadOverlayTemplate(AreaNoTemplateName));
        }

        private void WriteAreaNameSpeedrunOverlayHtml()
        {
            WriteOverlayHtml("area_name_speedrun.html", LoadOverlayTemplate(AreaNameSpeedrunTemplateName));
        }

        private void WriteScreenTimelineOverlayHtml()
        {
            WriteOverlayHtml("screen_timeline.html", LoadOverlayTemplate(ScreenTimelineTemplateName));
        }

        private void WriteOverlayHtml(string fileName, string html)
        {
            string path = Path.Combine(_outputDir, fileName);

            if (File.Exists(path))
            {
                return;
            }

            File.WriteAllText(path, html, Encoding.UTF8);
        }

        private string LoadOverlayTemplate(string fileName)
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
