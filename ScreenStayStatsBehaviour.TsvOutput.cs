using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JumpKing.MiscSystems.Achievements;

namespace JKMetricsLite
{
    public partial class ScreenStayStatsBehaviour
    {
        private void WriteAreaProgressTsv()
        {
            var buildStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var sb = new StringBuilder();
            sb.AppendLine(
                "area_name\tentry_ms\tlanding_ms\tduration_ms\tcurrent\texcluded\tunlocked"
            );

            foreach (string area in GetRawAreaFramesInAppearedOrder())
            {
                int frames = _areaFrames[area];
                string entryMilliseconds = "";
                string firstLandedMilliseconds = "";

                if (_areaFirstReachedMilliseconds.ContainsKey(area))
                {
                    entryMilliseconds =
                        _areaFirstReachedMilliseconds[area].ToString();
                }

                if (_areaFirstLandedMilliseconds.ContainsKey(area))
                {
                    firstLandedMilliseconds =
                        _areaFirstLandedMilliseconds[area].ToString();
                }

                sb.AppendLine(
                    EscapeTsv(area) + "\t" +
                    entryMilliseconds + "\t" +
                    firstLandedMilliseconds + "\t" +
                    FramesToMilliseconds(frames) + "\t" +
                    (area == _lastArea ? "1" : "0") + "\t" +
                    (_excludedAreas.Contains(area) ? "1" : "0") + "\t" +
                    (IsAreaUnlocked(area) ? "1" : "0")
                );
            }

            if (_babeClearTimeMilliseconds.HasValue)
            {
                string clearTimeMilliseconds = _babeClearTimeMilliseconds.Value.ToString();

                sb.AppendLine(
                    CompletionAreaName + "\t" +
                    clearTimeMilliseconds + "\t" +
                    clearTimeMilliseconds + "\t\t0\t0\t1"
                );
            }

            string output = sb.ToString();
            buildStopwatch.Stop();
            AddPerformanceTiming("area_progress_build", buildStopwatch.Elapsed.TotalMilliseconds);

            var ioStopwatch = System.Diagnostics.Stopwatch.StartNew();
            File.WriteAllText(_areaProgressPath, output, Encoding.UTF8);
            ioStopwatch.Stop();
            AddPerformanceTiming("area_progress_io", ioStopwatch.Elapsed.TotalMilliseconds);
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
                var buildStopwatch = System.Diagnostics.Stopwatch.StartNew();
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

                string output = sb.ToString();
                buildStopwatch.Stop();
                AddPerformanceTiming("current_state_build", buildStopwatch.Elapsed.TotalMilliseconds);

                var ioStopwatch = System.Diagnostics.Stopwatch.StartNew();
                File.WriteAllText(_statePath, output, Encoding.UTF8);
                ioStopwatch.Stop();
                AddPerformanceTiming("current_state_io", ioStopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                LogError("Write state TSV", ex);
            }
        }

        private void AppendScreenEventTsv(string areaName, int screen, string eventName)
        {
            try
            {
                var prepareStopwatch = System.Diagnostics.Stopwatch.StartNew();
                bool needsHeader = !File.Exists(_screenEventsPath) ||
                    new FileInfo(_screenEventsPath).Length == 0;
                prepareStopwatch.Stop();
                AddPerformanceTiming("screen_events_prepare", prepareStopwatch.Elapsed.TotalMilliseconds);

                var sb = new StringBuilder();

                if (needsHeader)
                {
                    sb.AppendLine("screen\tarea_name\tevent\telapsed_ms");
                }

                sb.AppendLine(
                    screen + "\t" +
                    EscapeTsv(areaName) + "\t" +
                    eventName + "\t" +
                    GetScreenEventMilliseconds()
                );

                string output = sb.ToString();
                var ioStopwatch = System.Diagnostics.Stopwatch.StartNew();
                File.AppendAllText(_screenEventsPath, output, Encoding.UTF8);
                ioStopwatch.Stop();
                AddPerformanceTiming("screen_events_io", ioStopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                LogError("Append screen event TSV", ex);
            }
        }

        private long GetScreenEventMilliseconds()
        {
            return _totalFrames == 0 ? 0 : GetCurrentRunMilliseconds();
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
                var prepareStopwatch = System.Diagnostics.Stopwatch.StartNew();
                EnsureScreenMetricsTsvHeader();
                bool exists = File.Exists(_screenMetricsPath);
                prepareStopwatch.Stop();
                AddPerformanceTiming("screen_metrics_prepare", prepareStopwatch.Elapsed.TotalMilliseconds);

                var statsStopwatch = System.Diagnostics.Stopwatch.StartNew();
                PlayerStats? stats = PlayerStatsReader.TryGetCurrentStats();
                statsStopwatch.Stop();
                AddPerformanceTiming("screen_metrics_stats", statsStopwatch.Elapsed.TotalMilliseconds);

                string jumps = stats.HasValue ? stats.Value.jumps.ToString() : "";
                string falls = stats.HasValue ? stats.Value.falls.ToString() : "";

                var sb = new StringBuilder();

                if (!exists)
                {
                    sb.AppendLine("elapsed_ms\tscreen\tjumps\tfalls");
                }

                sb.AppendLine(
                    FramesToMilliseconds(_totalFrames) + "\t" +
                    _lastScreen + "\t" +
                    jumps + "\t" +
                    falls
                );

                string output = sb.ToString();
                var ioStopwatch = System.Diagnostics.Stopwatch.StartNew();
                File.AppendAllText(_screenMetricsPath, output, Encoding.UTF8);
                ioStopwatch.Stop();
                AddPerformanceTiming("screen_metrics_io", ioStopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                LogError("Append screen metrics TSV", ex);
            }
        }

        private void EnsureScreenMetricsTsvHeader()
        {
            if (_screenMetricsHeaderChecked)
            {
                return;
            }

            if (string.IsNullOrEmpty(_screenMetricsPath) ||
                !File.Exists(_screenMetricsPath) ||
                new FileInfo(_screenMetricsPath).Length == 0)
            {
                _screenMetricsHeaderChecked = true;
                return;
            }

            string header;

            using (var reader = new StreamReader(_screenMetricsPath, Encoding.UTF8))
            {
                header = reader.ReadLine();
            }

            if (string.IsNullOrEmpty(header) ||
                header == "elapsed_ms\tscreen\tjumps\tfalls")
            {
                _screenMetricsHeaderChecked = true;
                return;
            }

            File.Delete(_screenMetricsPath);
            _screenMetricsHeaderChecked = true;
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
