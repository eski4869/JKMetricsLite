using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JKMetricsLite
{
    public partial class ScreenStayStatsBehaviour
    {
        private void WriteAreaProgressTsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine(
                "area_name\tsplit_ms\tduration_ms\tis_current\tis_excluded"
            );

            foreach (string area in GetRawAreaFramesInAppearedOrder())
            {
                int frames = _areaFrames[area];
                string firstReachedMilliseconds = "";

                if (_areaFirstReachedMilliseconds.ContainsKey(area))
                {
                    firstReachedMilliseconds =
                        _areaFirstReachedMilliseconds[area].ToString();
                }

                sb.AppendLine(
                    EscapeTsv(area) + "\t" +
                    firstReachedMilliseconds + "\t" +
                    FramesToMilliseconds(frames) + "\t" +
                    (area == _lastArea ? "1" : "0") + "\t" +
                    (_excludedAreas.Contains(area) ? "1" : "0")
                );
            }

            File.WriteAllText(_areaProgressPath, sb.ToString(), Encoding.UTF8);
        }

        private Dictionary<string, string> BuildAreaIndexMap()
        {
            var map = new Dictionary<string, string>();
            int index = 1;

            foreach (string area in _areaAppearedOrder)
            {
                if (!IsAreaIncludedForMetrics(area))
                {
                    continue;
                }

                if (!_areaFrames.ContainsKey(area))
                {
                    continue;
                }

                if (!map.ContainsKey(area))
                {
                    map[area] = index.ToString();
                    index++;
                }
            }

            foreach (KeyValuePair<string, int> pair in _areaFrames)
            {
                string area = pair.Key;

                if (!IsAreaIncludedForMetrics(area))
                {
                    continue;
                }

                if (!map.ContainsKey(area))
                {
                    map[area] = index.ToString();
                    index++;
                }
            }

            return map;
        }

        private void WriteStateTsv()
        {
            try
            {
                int currentAreaOrder;
                int currentScreenOrder;
                GetCurrentProgress(out currentAreaOrder, out currentScreenOrder);

                var sb = new StringBuilder();
                sb.AppendLine(
                    "attempt\t" +
                    "elapsed_ms\tscreen\tarea_name\t" +
                    "current_area_order\tcurrent_screen_order\t" +
                    "pb_area_order\tpb_screen_order\t" +
                    "screen_order_revision"
                );
                sb.AppendLine(
                    (_attempt.HasValue ? _attempt.Value.ToString() : "UNKNOWN") + "\t" +
                    FramesToMilliseconds(_totalFrames) + "\t" +
                    _lastScreen + "\t" +
                    EscapeTsv(_lastArea) + "\t" +
                    Math.Max(0, currentAreaOrder) + "\t" +
                    Math.Max(0, currentScreenOrder) + "\t" +
                    Math.Max(0, _pbAreaIndex) + "\t" +
                    Math.Max(0, _pbScreenInArea) + "\t" +
                    Math.Max(0, _screenOrderRevision)
                );

                File.WriteAllText(_statePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogError("Write state TSV", ex);
            }
        }

        private void AppendScreenOrderTsv(string areaName, int screen)
        {
            try
            {
                EnsureScreenOrderTsvHeader();

                var sb = new StringBuilder();

                if (!File.Exists(_screenOrderPath) ||
                    new FileInfo(_screenOrderPath).Length == 0)
                {
                    sb.AppendLine("elapsed_ms\tscreen\tarea_name");
                }

                sb.AppendLine(
                    FramesToMilliseconds(_totalFrames) + "\t" +
                    screen + "\t" +
                    EscapeTsv(areaName)
                );
                File.AppendAllText(_screenOrderPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogError("Append screen order TSV", ex);
            }
        }

        private void EnsureScreenOrderTsvHeader()
        {
            if (string.IsNullOrEmpty(_screenOrderPath) ||
                !File.Exists(_screenOrderPath) ||
                new FileInfo(_screenOrderPath).Length == 0)
            {
                return;
            }

            string[] lines = File.ReadAllLines(_screenOrderPath, Encoding.UTF8);

            if (lines.Length == 0 ||
                lines[0] == "elapsed_ms\tscreen\tarea_name")
            {
                return;
            }

            var rows = ReadTsvRows(_screenOrderPath);
            var sb = new StringBuilder();
            sb.AppendLine("elapsed_ms\tscreen\tarea_name");

            for (int i = 0; i < rows.Count; i++)
            {
                string areaName = GetTsvValue(rows[i], "area_name");
                string screen = GetTsvValue(rows[i], "screen");

                if (string.IsNullOrEmpty(areaName) || string.IsNullOrEmpty(screen))
                {
                    continue;
                }

                sb.AppendLine("\t" + screen + "\t" + EscapeTsv(areaName));
            }

            File.WriteAllText(_screenOrderPath, sb.ToString(), Encoding.UTF8);
        }

        private void GetCurrentProgress(out int areaOrder, out int screenOrder)
        {
            areaOrder = 0;
            screenOrder = 0;

            if (_lastScreen < MinScreen || _lastScreen > MaxScreen)
            {
                return;
            }

            string areaName = GetAreaNameForScreen(_lastScreen);

            if (!IsAreaIncludedForMetrics(areaName))
            {
                return;
            }

            Dictionary<string, string> areaIndexMap = BuildAreaIndexMap();

            if (!areaIndexMap.ContainsKey(areaName))
            {
                return;
            }

            if (!int.TryParse(areaIndexMap[areaName], out areaOrder))
            {
                areaOrder = 0;
                return;
            }

            screenOrder = GetScreenInAreaOrder(areaName, _lastScreen);

            if (screenOrder < 0)
            {
                screenOrder = 0;
            }
        }

        private void AppendScreenMetricTsv()
        {
            if (_lastScreen < MinScreen || _lastScreen > MaxScreen)
            {
                return;
            }

            try
            {
                bool exists = File.Exists(_screenMetricsPath);

                var sb = new StringBuilder();

                if (!exists)
                {
                    sb.AppendLine("elapsed_ms\tscreen");
                }

                sb.AppendLine(
                    FramesToMilliseconds(_totalFrames) + "\t" +
                    _lastScreen
                );

                File.AppendAllText(_screenMetricsPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogError("Append screen metrics TSV", ex);
            }
        }

        private List<string> GetRawAreaFramesInAppearedOrder()
        {
            var list = new List<string>();

            for (int i = 0; i < _areaAppearedOrder.Count; i++)
            {
                string area = _areaAppearedOrder[i];

                if (area == "Unknown")
                {
                    continue;
                }

                if (_areaFrames.ContainsKey(area))
                {
                    list.Add(area);
                }
            }

            foreach (KeyValuePair<string, int> pair in _areaFrames)
            {
                string area = pair.Key;

                if (area == "Unknown")
                {
                    continue;
                }

                if (!list.Contains(area))
                {
                    list.Add(area);
                }
            }

            return list;
        }
    }
}

