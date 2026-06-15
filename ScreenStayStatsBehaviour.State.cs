using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JKMetricsLite
{
    public partial class ScreenStayStatsBehaviour
    {
        private void ResetStats()
        {
            _areaFrames.Clear();
            _areaFirstReachedMilliseconds.Clear();
            _areaAppearedOrder.Clear();
            _areaScreenAppearedOrder.Clear();
            _excludedAreas.Clear();

            _totalFrames = 0;
            _outputCounter = 0;
            _screenOrderSaveCounter = 0;
            _lastScreen = -1;
            _lastTimelineAppendFrames = -1;
            _lastArea = "Unknown";
            _screenOrderDirty = false;

            _pbArea = "";
            _pbAreaIndex = -1;
            _pbScreenInArea = -1;
            _pbScreen = -1;
            _attempt = null;
        }

        private void ResetTimelineFile()
        {
            try
            {
                if (File.Exists(_screenTimelinePath))
                {
                    File.Delete(_screenTimelinePath);
                }
            }
            catch (Exception ex)
            {
                LogError("Reset timeline file", ex);
            }
        }

        private void ReconcileLoadedStateWithGameTime()
        {
            TimeSpan? currentRunTime = TryGetCurrentRunTime();

            if (!currentRunTime.HasValue)
            {
                return;
            }

            double secondsPerFrame = GetSecondsPerFrame();

            if (secondsPerFrame <= 0)
            {
                return;
            }

            int gameFrames = (int)Math.Round(
                currentRunTime.Value.TotalSeconds / secondsPerFrame
            );
            int delta = gameFrames - _totalFrames;

            if (delta <= 0)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_lastArea) && _lastArea != "Unknown")
            {
                if (!_areaFrames.ContainsKey(_lastArea))
                {
                    _areaFrames[_lastArea] = 0;
                }

                _areaFrames[_lastArea] += delta;
            }

            _totalFrames = gameFrames;
        }

        private bool LoadRunDataIfSameAttempt(int? currentAttempt)
        {
            if (!File.Exists(_areaMetricsPath) ||
                !File.Exists(_screenOrderPath) ||
                !File.Exists(_currentProgressPath))
            {
                return false;
            }

            int? savedAttempt = ReadSavedAttempt();

            if (currentAttempt.HasValue &&
                savedAttempt.HasValue &&
                currentAttempt.Value != savedAttempt.Value)
            {
                return false;
            }

            try
            {
                ResetStats();
                LoadAreaMetrics();
                LoadScreenOrder();
                LoadCurrentProgress();

                _attempt = currentAttempt.HasValue
                    ? currentAttempt
                    : savedAttempt;
                _screenOrderDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                LogError("Load run data", ex);
                return false;
            }
        }

        private void LoadAreaMetrics()
        {
            List<Dictionary<string, string>> rows = ReadTsvRows(_areaMetricsPath);

            for (int i = 0; i < rows.Count; i++)
            {
                Dictionary<string, string> row = rows[i];
                string area = FormatAreaName(GetTsvValue(row, "area_name"));

                if (area == "Unknown" || _areaFrames.ContainsKey(area))
                {
                    continue;
                }

                long durationMilliseconds;

                if (!long.TryParse(
                    GetTsvValue(row, "duration_ms"),
                    out durationMilliseconds
                ))
                {
                    durationMilliseconds = 0;
                }

                _areaFrames[area] = MillisecondsToFrames(durationMilliseconds);
                _areaAppearedOrder.Add(area);

                long splitTimeMilliseconds;

                if (long.TryParse(
                    GetTsvValue(row, "split_time_ms"),
                    out splitTimeMilliseconds
                ))
                {
                    _areaFirstReachedMilliseconds[area] =
                        Math.Max(0, splitTimeMilliseconds);
                }

                if (GetTsvValue(row, "is_excluded") == "1")
                {
                    _excludedAreas.Add(area);
                }

                if (GetTsvValue(row, "is_current") == "1")
                {
                    _lastArea = area;
                }
            }
        }

        private void LoadScreenOrder()
        {
            List<Dictionary<string, string>> rows = ReadTsvRows(_screenOrderPath);

            for (int i = 0; i < rows.Count; i++)
            {
                Dictionary<string, string> row = rows[i];
                string area = FormatAreaName(GetTsvValue(row, "area_name"));
                int screen;

                if (!_areaFrames.ContainsKey(area) ||
                    !int.TryParse(GetTsvValue(row, "screen"), out screen) ||
                    screen < MinScreen ||
                    screen > MaxScreen)
                {
                    continue;
                }

                RegisterAreaScreenIfNeeded(area, screen);
            }
        }

        private void LoadCurrentProgress()
        {
            List<Dictionary<string, string>> rows = ReadTsvRows(_currentProgressPath);

            if (rows.Count == 0)
            {
                return;
            }

            Dictionary<string, string> row = rows[0];
            int savedAttempt;

            if (int.TryParse(GetTsvValue(row, "attempt"), out savedAttempt))
            {
                _attempt = savedAttempt;
            }

            long elapsedMilliseconds;

            if (long.TryParse(
                GetTsvValue(row, "elapsed_ms"),
                out elapsedMilliseconds
            ))
            {
                _totalFrames = MillisecondsToFrames(elapsedMilliseconds);
            }

            int currentScreen;

            if (int.TryParse(
                GetTsvValue(row, "current_screen"),
                out currentScreen
            ) &&
                currentScreen >= MinScreen &&
                currentScreen <= MaxScreen)
            {
                _lastScreen = currentScreen;
                _lastArea = GetAreaNameForScreen(currentScreen);
            }
        }

        private void RecalculatePb()
        {
            _pbArea = "";
            _pbAreaIndex = -1;
            _pbScreenInArea = -1;
            _pbScreen = -1;

            for (int i = 0; i < _areaAppearedOrder.Count; i++)
            {
                string area = _areaAppearedOrder[i];

                if (!IsAreaIncludedForMetrics(area) ||
                    !_areaScreenAppearedOrder.ContainsKey(area))
                {
                    continue;
                }

                List<int> screens = _areaScreenAppearedOrder[area];

                for (int j = 0; j < screens.Count; j++)
                {
                    UpdatePbIfNeeded(screens[j], area);
                }
            }
        }

        private int? ReadSavedAttempt()
        {
            try
            {
                List<Dictionary<string, string>> rows =
                    ReadTsvRows(_currentProgressPath);

                if (rows.Count == 0)
                {
                    return null;
                }

                int attempt;

                return int.TryParse(
                    GetTsvValue(rows[0], "attempt"),
                    out attempt
                )
                    ? (int?)attempt
                    : null;
            }
            catch (Exception ex)
            {
                LogError("Read saved attempt", ex);
                return null;
            }
        }

        private List<Dictionary<string, string>> ReadTsvRows(string path)
        {
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            var rows = new List<Dictionary<string, string>>();

            if (lines.Length <= 1)
            {
                return rows;
            }

            string[] headers = lines[0].Split('\t');

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                string[] values = lines[i].Split('\t');
                var row = new Dictionary<string, string>();

                for (int j = 0; j < headers.Length; j++)
                {
                    row[headers[j]] = j < values.Length ? values[j] : "";
                }

                rows.Add(row);
            }

            return rows;
        }

        private string GetTsvValue(
            Dictionary<string, string> row,
            string key
        )
        {
            string value;
            return row.TryGetValue(key, out value) ? value : "";
        }
    }
}
