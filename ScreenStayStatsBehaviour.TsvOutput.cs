using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using JumpKing.MiscSystems.Achievements;
using JumpKing.MiscSystems.LocationText;

namespace JKMetricsLite
{
    public partial class ScreenStayStatsBehaviour
    {
        private void WriteAreaBarGraphTsv()
        {
            int maxFrames = GetMaxAreaFrames();
            Dictionary<string, string> areaIndexMap = BuildAreaIndexMap();

            var sb = new StringBuilder();
            sb.AppendLine("area_index\tarea_name\tfirst_reach\tstay_graph\tstay_time\tis_current\tfirst_reach_speedrun\tstay_time_speedrun");

            foreach (string area in GetAreaFramesInAppearedOrder())
            {
                if (area == "Unknown")
                {
                    continue;
                }

                int frames = _areaFrames[area];

                string firstReachedTime = "-";
                string firstReachedSpeedrunTime = "-";

                if (_areaFirstReachedFrames.ContainsKey(area))
                {
                    int firstFrames = _areaFirstReachedFrames[area];
                    firstReachedTime = FormatFramesAsTime(firstFrames);
                    firstReachedSpeedrunTime = FormatFramesAsSpeedrunTime(firstFrames);
                }

                string areaIndex = "Unknown";

                if (areaIndexMap.ContainsKey(area))
                {
                    areaIndex = areaIndexMap[area];
                }

                sb.AppendLine(
                    EscapeTsv(areaIndex) + "\t" +
                    EscapeTsv(area) + "\t" +
                    EscapeTsv(firstReachedTime) + "\t" +
                    EscapeTsv(BuildBar(frames, maxFrames)) + "\t" +
                    EscapeTsv(FormatFramesAsTime(frames)) + "\t" +
                    (area == _lastArea ? "1" : "0") + "\t" +
                    EscapeTsv(firstReachedSpeedrunTime) + "\t" +
                    EscapeTsv(FormatFramesAsSpeedrunTime(frames))
                );
            }

            File.WriteAllText(_areaBarGraphPath, sb.ToString(), Encoding.UTF8);
        }

        private Dictionary<string, string> BuildAreaIndexMap()
        {
            var map = new Dictionary<string, string>();
            int index = 1;

            foreach (string area in _areaAppearedOrder)
            {
                if (area == "Unknown")
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

                if (area == "Unknown")
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

        private void WriteScreenBarGraphTsv()
        {
            int maxFrames = 0;

            for (int screen = MinScreen; screen <= MaxScreen; screen++)
            {
                string area = GetAreaNameForScreen(screen);

                if (area == "Unknown")
                {
                    continue;
                }

                if (_screenFrames[screen] > maxFrames)
                {
                    maxFrames = _screenFrames[screen];
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("screen\tstay_graph\tstay_time\tarea\tis_current");

            for (int screen = MinScreen; screen <= MaxScreen; screen++)
            {
                string area = GetAreaNameForScreen(screen);

                if (area == "Unknown")
                {
                    continue;
                }

                int frames = _screenFrames[screen];

                sb.AppendLine(
                    screen + "\t" +
                    EscapeTsv(BuildBar(frames, maxFrames)) + "\t" +
                    EscapeTsv(FormatFramesAsTime(frames)) + "\t" +
                    EscapeTsv(area) + "\t" +
                    (screen == _lastScreen ? "1" : "0")
                );
            }

            File.WriteAllText(_screenBarGraphPath, sb.ToString(), Encoding.UTF8);
        }

        private void WriteProgressStatusTsv()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("key\tvalue");
                sb.AppendLine("pb\t" + EscapeTsv(GetPbText()));
                sb.AppendLine("current\t" + EscapeTsv(GetCurrentText()));
                sb.AppendLine("pb_area\t" + EscapeTsv(_pbArea));
                sb.AppendLine("pb_area_index\t" + _pbAreaIndex);
                sb.AppendLine("pb_screen_in_area\t" + _pbScreenInArea);
                sb.AppendLine("pb_screen\t" + _pbScreen);
                sb.AppendLine("current_screen\t" + _lastScreen);

                File.WriteAllText(_progressStatusPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogError("Write progress status TSV", ex);
            }
        }

        private string GetPbText()
        {
            if (_pbAreaIndex <= 0 || _pbScreenInArea <= 0)
            {
                return "PB: -";
            }

            return "PB: " + _pbAreaIndex + "-" + _pbScreenInArea;
        }

        private string GetCurrentText()
        {
            return "Current: " + FormatCurrentProgressScreen(_lastScreen);
        }

        private string FormatCurrentProgressScreen(int screen)
        {
            if (screen < MinScreen || screen > MaxScreen)
            {
                return "-";
            }

            string areaName = GetAreaNameForScreen(screen);

            if (areaName == "Unknown")
            {
                return "Unknown";
            }

            Dictionary<string, string> areaIndexMap = BuildAreaIndexMap();

            if (!areaIndexMap.ContainsKey(areaName))
            {
                return "Unknown";
            }

            int screenInArea = GetScreenInAreaOrder(areaName, screen);

            if (screenInArea <= 0)
            {
                return "Unknown";
            }

            return areaIndexMap[areaName] + "-" + screenInArea;
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
                    sb.AppendLine("elapsed_frames\telapsed_time\tscreen\tarea\tattempt");
                }

                sb.AppendLine(
                    _totalFrames + "\t" +
                    EscapeTsv(FormatFramesAsTimeWithMs(_totalFrames)) + "\t" +
                    _lastScreen + "\t" +
                    EscapeTsv(_lastArea) + "\t" +
                    (_stateAttempt.HasValue ? _stateAttempt.Value.ToString() : "UNKNOWN")
                );

                File.AppendAllText(_screenTimelinePath, sb.ToString(), Encoding.UTF8);
                _lastTimelineAppendFrames = _totalFrames;
            }
            catch (Exception ex)
            {
                LogError("Append screen timeline TSV", ex);
            }
        }

        private int GetMaxAreaFrames()
        {
            int maxFrames = 0;

            foreach (KeyValuePair<string, int> pair in _areaFrames)
            {
                if (pair.Key == "Unknown")
                {
                    continue;
                }

                if (pair.Value > maxFrames)
                {
                    maxFrames = pair.Value;
                }
            }

            return maxFrames;
        }

        private string BuildBar(int frames, int maxFrames)
        {
            int barWidth = 0;

            if (maxFrames > 0)
            {
                barWidth = (int)Math.Round(frames * MaxBarWidth / (double)maxFrames);
            }

            return new string('#', barWidth);
        }

        private List<string> GetAreaFramesInAppearedOrder()
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

