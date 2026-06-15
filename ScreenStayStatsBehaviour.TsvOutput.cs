using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JKMetricsLite
{
    public partial class ScreenStayStatsBehaviour
    {
        private void WriteAreaMetricsTsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine(
                "area_name\tsplit_time_ms\tduration_ms\tis_current\tis_excluded"
            );

            foreach (string area in GetRawAreaFramesInAppearedOrder())
            {
                int frames = _areaFrames[area];
                string splitTimeMilliseconds = "";

                if (_areaFirstReachedMilliseconds.ContainsKey(area))
                {
                    splitTimeMilliseconds =
                        _areaFirstReachedMilliseconds[area].ToString();
                }

                sb.AppendLine(
                    EscapeTsv(area) + "\t" +
                    splitTimeMilliseconds + "\t" +
                    FramesToMilliseconds(frames) + "\t" +
                    (area == _lastArea ? "1" : "0") + "\t" +
                    (_excludedAreas.Contains(area) ? "1" : "0")
                );
            }

            File.WriteAllText(_areaMetricsPath, sb.ToString(), Encoding.UTF8);
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

        private void WriteCurrentProgressTsv()
        {
            try
            {
                int currentAreaIndex;
                int currentScreenInArea;
                GetCurrentProgress(out currentAreaIndex, out currentScreenInArea);

                var sb = new StringBuilder();
                sb.AppendLine(
                    "attempt\telapsed_ms\tcurrent_screen\t" +
                    "current_area_index\tcurrent_screen_in_area\t" +
                    "pb_area_index\tpb_screen_in_area"
                );
                sb.AppendLine(
                    (_attempt.HasValue ? _attempt.Value.ToString() : "UNKNOWN") + "\t" +
                    FramesToMilliseconds(_totalFrames) + "\t" +
                    Math.Max(0, _lastScreen) + "\t" +
                    Math.Max(0, currentAreaIndex) + "\t" +
                    Math.Max(0, currentScreenInArea) + "\t" +
                    Math.Max(0, _pbAreaIndex) + "\t" +
                    Math.Max(0, _pbScreenInArea)
                );

                File.WriteAllText(_currentProgressPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogError("Write current progress TSV", ex);
            }
        }

        private void WriteScreenOrderTsv(bool force)
        {
            if (!force && !_screenOrderDirty)
            {
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("area_name\tscreen");

                foreach (string area in GetRawAreaFramesInAppearedOrder())
                {
                    if (!_areaScreenAppearedOrder.ContainsKey(area))
                    {
                        continue;
                    }

                    List<int> screens = _areaScreenAppearedOrder[area];

                    for (int i = 0; i < screens.Count; i++)
                    {
                        sb.AppendLine(EscapeTsv(area) + "\t" + screens[i]);
                    }
                }

                File.WriteAllText(_screenOrderPath, sb.ToString(), Encoding.UTF8);
                _screenOrderDirty = false;
            }
            catch (Exception ex)
            {
                LogError("Write screen order TSV", ex);
            }
        }

        private void GetCurrentProgress(out int areaIndex, out int screenInArea)
        {
            areaIndex = 0;
            screenInArea = 0;

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

            if (!int.TryParse(areaIndexMap[areaName], out areaIndex))
            {
                areaIndex = 0;
                return;
            }

            screenInArea = GetScreenInAreaOrder(areaName, _lastScreen);

            if (screenInArea < 0)
            {
                screenInArea = 0;
            }
        }

        private void AppendScreenTimelineTsv()
        {
            if (_lastTimelineAppendFrames == _totalFrames)
            {
                return;
            }

            try
            {
                bool exists = File.Exists(_screenTimelinePath);

                var sb = new StringBuilder();

                if (!exists)
                {
                    sb.AppendLine("elapsed_ms\tscreen");
                }

                sb.AppendLine(
                    FramesToMilliseconds(_totalFrames) + "\t" +
                    _lastScreen
                );

                File.AppendAllText(_screenTimelinePath, sb.ToString(), Encoding.UTF8);
                _lastTimelineAppendFrames = _totalFrames;
            }
            catch (Exception ex)
            {
                LogError("Append screen timeline TSV", ex);
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

