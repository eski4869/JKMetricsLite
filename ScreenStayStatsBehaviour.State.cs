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
            _areaFirstLandedMilliseconds.Clear();
            _areaAppearedOrder.Clear();
            _areaScreenAppearedOrder.Clear();
            _excludedAreas.Clear();
            _endingScreens.Clear();

            _totalFrames = 0;
            _outputCounter = 0;
            _lastScreen = -1;
            _lastArea = "Unknown";
            _screenOrderRevision = 0;
            _screenMetricsHeaderChecked = false;
            _babeClearTimeMilliseconds = null;

            _pbArea = "";
            _pbAreaIndex = -1;
            _pbScreenInArea = -1;
            _pbScreen = -1;
            _attempt = null;
        }

        private void ResetScreenMetricsFile()
        {
            try
            {
                if (File.Exists(_screenMetricsPath))
                {
                    File.Delete(_screenMetricsPath);
                }
            }
            catch (Exception ex)
            {
                LogError("Reset screen metrics file", ex);
            }
        }

        private void SyncLoadedElapsedWithGameTime()
        {
            TimeSpan? currentRunTime = PlayerStatsReader.TryGetCurrentRunTime();

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

            if (gameFrames > _totalFrames)
            {
                _totalFrames = gameFrames;
            }
        }

        private bool LoadRunDataIfSameAttempt(int? currentAttempt)
        {
            if (!File.Exists(_areaProgressPath) ||
                !File.Exists(_screenOrderPath) ||
                !File.Exists(GetStatePathForRead()))
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
                LoadAreaProgress();
                LoadScreenOrder();
                LoadScreenMetrics();
                LoadState();

                _attempt = currentAttempt.HasValue
                    ? currentAttempt
                    : savedAttempt;
                return true;
            }
            catch (Exception ex)
            {
                LogError("Load run data", ex);
                return false;
            }
        }

        private void LoadAreaProgress()
        {
            List<Dictionary<string, string>> rows = ReadTsvRows(_areaProgressPath);

            for (int i = 0; i < rows.Count; i++)
            {
                Dictionary<string, string> row = rows[i];
                string area = FormatAreaName(GetTsvValue(row, "area_name"));

                if (area == CompletionAreaName)
                {
                    long clearTimeMilliseconds;

                    if (long.TryParse(
                        GetTsvValue(row, "entry_based_split_ms"),
                        out clearTimeMilliseconds
                    ))
                    {
                        _babeClearTimeMilliseconds =
                            Math.Max(0, clearTimeMilliseconds);
                    }

                    continue;
                }

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

                long firstReachedMilliseconds;

                if (long.TryParse(
                    GetTsvValue(row, "entry_based_split_ms"),
                    out firstReachedMilliseconds
                ))
                {
                    _areaFirstReachedMilliseconds[area] =
                        Math.Max(0, firstReachedMilliseconds);
                }

                long firstLandedMilliseconds;

                if (long.TryParse(
                    GetTsvValue(row, "landing_based_split_ms"),
                    out firstLandedMilliseconds
                ))
                {
                    _areaFirstLandedMilliseconds[area] =
                        Math.Max(0, firstLandedMilliseconds);
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

                if (!_areaScreenAppearedOrder.ContainsKey(area))
                {
                    _areaScreenAppearedOrder[area] = new List<int>();
                }

                if (!_areaScreenAppearedOrder[area].Contains(screen))
                {
                    _areaScreenAppearedOrder[area].Add(screen);
                }
            }
        }

        private void LoadScreenMetrics()
        {
            Dictionary<string, string> row = ReadLastTsvRow(_screenMetricsPath);

            if (row == null)
            {
                return;
            }

            int screen;

            if (int.TryParse(GetTsvValue(row, "screen"), out screen) &&
                screen >= MinScreen &&
                screen <= MaxScreen)
            {
                _lastScreen = screen;
            }
        }

        private void LoadState()
        {
            List<Dictionary<string, string>> rows = ReadTsvRows(GetStatePathForRead());

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

            int screenOrderRevision;

            if (int.TryParse(
                GetTsvValue(row, "screen_order_revision"),
                out screenOrderRevision
            ))
            {
                _screenOrderRevision = Math.Max(0, screenOrderRevision);
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
                    ReadTsvRows(GetStatePathForRead());

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
            if (string.IsNullOrEmpty(path))
            {
                return new List<Dictionary<string, string>>();
            }

            if (!File.Exists(path))
            {
                return new List<Dictionary<string, string>>();
            }

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

        private Dictionary<string, string> ReadLastTsvRow(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (!File.Exists(path))
            {
                return null;
            }

            string[] lines = File.ReadAllLines(path, Encoding.UTF8);

            if (lines.Length <= 1)
            {
                return null;
            }

            string[] headers = lines[0].Split('\t');

            for (int i = lines.Length - 1; i >= 1; i--)
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

                return row;
            }

            return null;
        }

        private string GetTsvValue(
            Dictionary<string, string> row,
            string key
        )
        {
            string value;
            return row.TryGetValue(key, out value) ? value : "";
        }

        private string GetStatePathForRead()
        {
            if (!string.IsNullOrEmpty(_statePath) && File.Exists(_statePath))
            {
                return _statePath;
            }

            if (!string.IsNullOrEmpty(_currentAttemptDir))
            {
                string legacyPath = Path.Combine(_currentAttemptDir, "state.tsv");

                if (File.Exists(legacyPath))
                {
                    return legacyPath;
                }
            }

            return _statePath;
        }
    }
}
