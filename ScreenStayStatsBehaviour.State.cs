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
        private void ResetStats()
        {
            for (int i = 0; i < _screenFrames.Length; i++)
            {
                _screenFrames[i] = 0;
            }

            _areaFrames.Clear();
            _areaFirstReachedFrames.Clear();
            _areaAppearedOrder.Clear();
            _areaScreenAppearedOrder.Clear();

            _totalFrames = 0;
            _outputCounter = 0;
            _lastScreen = -1;
            _lastArea = "Unknown";

            _pbArea = "";
            _pbAreaIndex = -1;
            _pbScreenInArea = -1;
            _pbScreen = -1;
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

            int gameFrames = (int)Math.Round(currentRunTime.Value.TotalSeconds / secondsPerFrame);
            int delta = gameFrames - _totalFrames;

            if (delta <= 0)
            {
                return;
            }

            if (_lastScreen >= MinScreen && _lastScreen <= MaxScreen)
            {
                _screenFrames[_lastScreen] += delta;
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

        private bool LoadStateIfSameAttempt(int? currentAttempt)
        {
            if (!File.Exists(_statePath))
            {
                return false;
            }

            int? savedAttempt = ReadSavedAttempt();

            if (currentAttempt.HasValue && savedAttempt.HasValue && currentAttempt.Value != savedAttempt.Value)
            {
                return false;
            }

            try
            {
                ResetStats();

                string[] lines = File.ReadAllLines(_statePath, Encoding.UTF8);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    string[] parts = line.Split('\t');

                    if (parts.Length == 0)
                    {
                        continue;
                    }

                    if (parts[0] == "ATTEMPT" && parts.Length >= 2)
                    {
                        int parsedAttempt;
                        if (int.TryParse(parts[1], out parsedAttempt))
                        {
                            _stateAttempt = parsedAttempt;
                        }
                    }
                    else if (parts[0] == "TOTAL" && parts.Length >= 2)
                    {
                        int.TryParse(parts[1], out _totalFrames);
                    }
                    else if (parts[0] == "LAST" && parts.Length >= 3)
                    {
                        int.TryParse(parts[1], out _lastScreen);
                        _lastArea = DecodeText(parts[2]);
                    }
                    else if (parts[0] == "PB" && parts.Length >= 5)
                    {
                        _pbArea = DecodeText(parts[1]);
                        int.TryParse(parts[2], out _pbAreaIndex);
                        int.TryParse(parts[3], out _pbScreenInArea);
                        int.TryParse(parts[4], out _pbScreen);
                    }
                    else if (parts[0] == "SCREEN" && parts.Length >= 3)
                    {
                        int screen;
                        int frames;

                        if (int.TryParse(parts[1], out screen) &&
                            int.TryParse(parts[2], out frames) &&
                            screen >= MinScreen &&
                            screen <= MaxScreen)
                        {
                            _screenFrames[screen] = frames;
                        }
                    }
                    else if (parts[0] == "AREA" && parts.Length >= 4)
                    {
                        string area = DecodeText(parts[1]);

                        if (area == "Unknown")
                        {
                            continue;
                        }

                        int frames;
                        int firstReachedFrames;

                        if (int.TryParse(parts[2], out frames) &&
                            int.TryParse(parts[3], out firstReachedFrames))
                        {
                            _areaFrames[area] = frames;
                            _areaFirstReachedFrames[area] = firstReachedFrames;
                        }
                    }
                    else if (parts[0] == "ORDER" && parts.Length >= 2)
                    {
                        string area = DecodeText(parts[1]);

                        if (area == "Unknown")
                        {
                            continue;
                        }

                        if (!_areaAppearedOrder.Contains(area))
                        {
                            _areaAppearedOrder.Add(area);
                        }
                    }
                    else if (parts[0] == "AREA_SCREEN_ORDER" && parts.Length >= 3)
                    {
                        string area = DecodeText(parts[1]);

                        if (area == "Unknown")
                        {
                            continue;
                        }

                        int screen;

                        if (int.TryParse(parts[2], out screen) &&
                            screen >= MinScreen &&
                            screen <= MaxScreen)
                        {
                            RegisterAreaScreenIfNeeded(area, screen);
                        }
                    }
                }

                // For old state files without AREA_SCREEN_ORDER, rebuild a fallback order.
                RebuildAreaScreenOrderFallbackIfNeeded();

                if (currentAttempt.HasValue)
                {
                    _stateAttempt = currentAttempt;
                }
                else if (savedAttempt.HasValue)
                {
                    _stateAttempt = savedAttempt;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError("Load state", ex);
                return false;
            }
        }

        private void RebuildAreaScreenOrderFallbackIfNeeded()
        {
            foreach (string area in GetAreaFramesInAppearedOrder())
            {
                if (!_areaScreenAppearedOrder.ContainsKey(area))
                {
                    _areaScreenAppearedOrder[area] = new List<int>();
                }
            }

            for (int screen = MinScreen; screen <= MaxScreen; screen++)
            {
                if (_screenFrames[screen] <= 0)
                {
                    continue;
                }

                string area = GetAreaNameForScreen(screen);

                if (area == "Unknown")
                {
                    continue;
                }

                RegisterAreaScreenIfNeeded(area, screen);
            }
        }

        private void RepairPbIfNeeded()
        {
            if (IsValidPb())
            {
                return;
            }

            _pbArea = "";
            _pbAreaIndex = -1;
            _pbScreenInArea = -1;
            _pbScreen = -1;

            foreach (string area in _areaAppearedOrder)
            {
                if (area == "Unknown")
                {
                    continue;
                }

                if (!_areaScreenAppearedOrder.ContainsKey(area))
                {
                    continue;
                }

                List<int> screens = _areaScreenAppearedOrder[area];

                for (int i = 0; i < screens.Count; i++)
                {
                    int screen = screens[i];

                    if (_screenFrames[screen] <= 0)
                    {
                        continue;
                    }

                    UpdatePbIfNeeded(screen, area);
                }
            }
        }

        private bool IsValidPb()
        {
            if (_pbAreaIndex <= 0 || _pbScreenInArea <= 0)
            {
                return false;
            }

            if (string.IsNullOrEmpty(_pbArea) || _pbArea == "Unknown")
            {
                return false;
            }

            if (!_areaScreenAppearedOrder.ContainsKey(_pbArea))
            {
                return false;
            }

            int screenInArea = GetScreenInAreaOrder(_pbArea, _pbScreen);

            return screenInArea == _pbScreenInArea;
        }

        private int? ReadSavedAttempt()
        {
            try
            {
                if (!File.Exists(_statePath))
                {
                    return null;
                }

                string[] lines = File.ReadAllLines(_statePath, Encoding.UTF8);

                foreach (string line in lines)
                {
                    string[] parts = line.Split('\t');

                    if (parts.Length >= 2 && parts[0] == "ATTEMPT")
                    {
                        int attempt;
                        if (int.TryParse(parts[1], out attempt))
                        {
                            return attempt;
                        }

                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Read saved attempt", ex);
            }

            return null;
        }

        private void SaveState()
        {
            try
            {
                var sb = new StringBuilder();

                sb.AppendLine("VERSION\t3");
                sb.AppendLine("ATTEMPT\t" + (_stateAttempt.HasValue ? _stateAttempt.Value.ToString() : "UNKNOWN"));
                sb.AppendLine("TOTAL\t" + _totalFrames);
                sb.AppendLine("LAST\t" + _lastScreen + "\t" + EncodeText(_lastArea));
                sb.AppendLine(
                    "PB\t" +
                    EncodeText(_pbArea) + "\t" +
                    _pbAreaIndex + "\t" +
                    _pbScreenInArea + "\t" +
                    _pbScreen
                );

                for (int screen = MinScreen; screen <= MaxScreen; screen++)
                {
                    if (_screenFrames[screen] > 0)
                    {
                        sb.AppendLine("SCREEN\t" + screen + "\t" + _screenFrames[screen]);
                    }
                }

                foreach (KeyValuePair<string, int> pair in _areaFrames)
                {
                    string area = pair.Key;

                    if (area == "Unknown")
                    {
                        continue;
                    }

                    int frames = pair.Value;
                    int firstReachedFrames = 0;

                    if (_areaFirstReachedFrames.ContainsKey(area))
                    {
                        firstReachedFrames = _areaFirstReachedFrames[area];
                    }

                    sb.AppendLine(
                        "AREA\t" +
                        EncodeText(area) + "\t" +
                        frames + "\t" +
                        firstReachedFrames
                    );
                }

                for (int i = 0; i < _areaAppearedOrder.Count; i++)
                {
                    string area = _areaAppearedOrder[i];

                    if (area == "Unknown")
                    {
                        continue;
                    }

                    sb.AppendLine("ORDER\t" + EncodeText(area));
                }

                foreach (KeyValuePair<string, List<int>> pair in _areaScreenAppearedOrder)
                {
                    string area = pair.Key;

                    if (area == "Unknown")
                    {
                        continue;
                    }

                    List<int> screens = pair.Value;

                    for (int i = 0; i < screens.Count; i++)
                    {
                        sb.AppendLine(
                            "AREA_SCREEN_ORDER\t" +
                            EncodeText(area) + "\t" +
                            screens[i]
                        );
                    }
                }

                File.WriteAllText(_statePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogError("Save state", ex);
            }
        }
    }
}

