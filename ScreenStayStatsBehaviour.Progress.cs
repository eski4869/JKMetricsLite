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
        internal static bool IsCurrentAreaExcludedFromMetrics()
        {
            if (_instance == null)
            {
                return false;
            }

            return !_instance.IsAreaIncludedForMetrics(_instance._lastArea);
        }

        internal static bool CanChangeCurrentAreaMetricsExclusion()
        {
            return _instance != null &&
                !string.IsNullOrEmpty(_instance._lastArea) &&
                _instance._lastArea != "Unknown";
        }

        internal static void SetCurrentAreaExcludedFromMetrics(bool isExcluded)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.SetCurrentAreaExcludedFromMetricsInstance(isExcluded);
        }

        private void SetCurrentAreaExcludedFromMetricsInstance(bool isExcluded)
        {
            if (string.IsNullOrEmpty(_lastArea) || _lastArea == "Unknown")
            {
                return;
            }

            bool changed = isExcluded
                ? _excludedAreas.Add(_lastArea)
                : _excludedAreas.Remove(_lastArea);

            if (!changed)
            {
                return;
            }

            RecalculatePb();
            WriteOutputFiles(true);
            SaveState();
        }

        private bool IsAreaIncludedForMetrics(string areaName)
        {
            if (string.IsNullOrEmpty(areaName) || areaName == "Unknown")
            {
                return false;
            }

            return !_excludedAreas.Contains(areaName);
        }

        private string GetDisplayAreaName(string areaName)
        {
            return IsAreaIncludedForMetrics(areaName) ? areaName : "Unknown";
        }

        private void RecordAreaFirstReach(string areaName)
        {
            int firstReachedFrames = _totalFrames;
            long firstReachedMilliseconds = FramesToMilliseconds(firstReachedFrames);

            TimeSpan? currentRunTime = TryGetCurrentRunTime();

            if (currentRunTime.HasValue && currentRunTime.Value.TotalMilliseconds >= 0)
            {
                firstReachedMilliseconds = (long)Math.Round(currentRunTime.Value.TotalMilliseconds);

                double secondsPerFrame = GetSecondsPerFrame();

                if (secondsPerFrame > 0)
                {
                    firstReachedFrames = (int)Math.Round(
                        currentRunTime.Value.TotalSeconds / secondsPerFrame
                    );
                }
            }

            _areaFirstReachedFrames[areaName] = firstReachedFrames;
            _areaFirstReachedMilliseconds[areaName] = firstReachedMilliseconds;
        }

        private void RegisterAreaScreenIfNeeded(string areaName, int screen)
        {
            if (areaName == "Unknown")
            {
                return;
            }

            if (!_areaScreenAppearedOrder.ContainsKey(areaName))
            {
                _areaScreenAppearedOrder[areaName] = new List<int>();
            }

            if (!_areaScreenAppearedOrder[areaName].Contains(screen))
            {
                _areaScreenAppearedOrder[areaName].Add(screen);
            }
        }

        private void UpdatePbIfNeeded(int screen, string areaName)
        {
            if (!IsAreaIncludedForMetrics(areaName))
            {
                return;
            }

            Dictionary<string, string> areaIndexMap = BuildAreaIndexMap();

            if (!areaIndexMap.ContainsKey(areaName))
            {
                return;
            }

            int areaIndex;

            if (!int.TryParse(areaIndexMap[areaName], out areaIndex))
            {
                return;
            }

            int screenInArea = GetScreenInAreaOrder(areaName, screen);

            if (screenInArea <= 0)
            {
                return;
            }

            bool shouldUpdate =
                areaIndex > _pbAreaIndex ||
                (areaIndex == _pbAreaIndex && screenInArea > _pbScreenInArea);

            if (shouldUpdate)
            {
                _pbArea = areaName;
                _pbAreaIndex = areaIndex;
                _pbScreenInArea = screenInArea;
                _pbScreen = screen;
            }
        }

        private int GetScreenInAreaOrder(string areaName, int screen)
        {
            if (!_areaScreenAppearedOrder.ContainsKey(areaName))
            {
                return -1;
            }

            List<int> screens = _areaScreenAppearedOrder[areaName];

            int index = screens.IndexOf(screen);

            if (index < 0)
            {
                return -1;
            }

            return index + 1;
        }

        private int? TryGetCurrentAttempt()
        {
            PlayerStats? stats = TryGetPlayerStats("PlayerStatsAttemptSnapshot");

            if (stats.HasValue)
            {
                return stats.Value.attempts;
            }

            return null;
        }

        private TimeSpan? TryGetCurrentRunTime()
        {
            try
            {
                Type managerType = typeof(PlayerStats).Assembly.GetType(
                    "JumpKing.MiscSystems.Achievements.AchievementManager"
                );

                if (managerType == null)
                {
                    return null;
                }

                FieldInfo instanceField = managerType.GetField(
                    "instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                );

                object manager = instanceField == null ? null : instanceField.GetValue(null);

                if (manager == null)
                {
                    return null;
                }

                MethodInfo getCurrentStatsMethod = managerType.GetMethod(
                    "GetCurrentStats",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (getCurrentStatsMethod == null)
                {
                    return null;
                }

                object statsObject = getCurrentStatsMethod.Invoke(manager, null);

                if (statsObject is PlayerStats)
                {
                    TimeSpan time = ((PlayerStats)statsObject).timeSpan;

                    if (time.TotalMilliseconds >= 0)
                    {
                        return time;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Get current run time", ex);
            }

            return null;
        }

        private PlayerStats? TryGetPlayerStats(string propertyName)
        {
            try
            {
                Type saveLubeType = typeof(PlayerStats).Assembly.GetType(
                    "JumpKing.SaveThread.SaveLube"
                );

                if (saveLubeType == null)
                {
                    return null;
                }

                PropertyInfo prop = saveLubeType.GetProperty(
                    propertyName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (prop != null)
                {
                    object statsObject = prop.GetValue(null, null);

                    if (statsObject is PlayerStats)
                    {
                        return (PlayerStats)statsObject;
                    }
                }

                FieldInfo attemptStatsField = saveLubeType.GetField(
                    "_attempt_stats",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (propertyName == "PlayerStatsAttemptSnapshot" && attemptStatsField != null)
                {
                    object statsObject = attemptStatsField.GetValue(null);

                    if (statsObject is PlayerStats)
                    {
                        return (PlayerStats)statsObject;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Get player stats", ex);
            }

            return null;
        }

        private static Location[] LoadLocations()
        {
            try
            {
                Type managerType = typeof(LocationSettings).Assembly.GetType(
                    "JumpKing.MiscSystems.LocationText.LocationTextManager"
                );

                if (managerType == null)
                {
                    return new Location[0];
                }

                object settingsObject = null;

                PropertyInfo settingsProperty = managerType.GetProperty(
                    "SETTINGS",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (settingsProperty != null)
                {
                    settingsObject = settingsProperty.GetValue(null, null);
                }

                if (settingsObject == null)
                {
                    FieldInfo settingsField = managerType.GetField(
                        "_settings",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                    );

                    if (settingsField != null)
                    {
                        settingsObject = settingsField.GetValue(null);
                    }
                }

                if (settingsObject is LocationSettings)
                {
                    LocationSettings settings = (LocationSettings)settingsObject;

                    if (settings.locations != null)
                    {
                        return settings.locations;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Load locations", ex);
            }

            return new Location[0];
        }

        private string GetAreaNameForScreen(int screen)
        {
            Location location;

            if (TryGetLocationForScreen(screen, out location))
            {
                return FormatAreaName(location.name);
            }

            return "Unknown";
        }

        private bool TryGetLocationForScreen(int screen, out Location matchedLocation)
        {
            matchedLocation = default(Location);

            if (_locations == null || _locations.Length == 0)
            {
                return false;
            }

            bool found = false;
            int bestStart = int.MinValue;

            for (int i = 0; i < _locations.Length; i++)
            {
                Location location = _locations[i];

                if (screen >= location.start && screen <= location.end)
                {
                    if (location.start > bestStart)
                    {
                        matchedLocation = location;
                        bestStart = location.start;
                        found = true;
                    }
                }
            }

            return found;
        }

        private string FormatAreaName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
            {
                return "Unknown";
            }

            string name = RemoveAreaFormattingTags(rawName).Trim();

            if (name.StartsWith("LOCATION_"))
            {
                name = name.Substring("LOCATION_".Length);
            }

            name = name.Replace('_', ' ').Trim();

            return name.Length == 0 ? "Unknown" : name;
        }

        private string RemoveAreaFormattingTags(string value)
        {
            var result = new StringBuilder();
            int index = 0;

            while (index < value.Length)
            {
                if (value[index] != '{')
                {
                    result.Append(value[index]);
                    index++;
                    continue;
                }

                int closingBraceIndex = value.IndexOf('}', index + 1);

                if (closingBraceIndex < 0)
                {
                    result.Append(value.Substring(index));
                    break;
                }

                index = closingBraceIndex + 1;
            }

            return result.ToString();
        }
    }
}
