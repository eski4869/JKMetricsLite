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
            if (areaName == "Unknown")
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
            PlayerStats? permanent = TryGetPlayerStats("PermanentPlayerStats");
            PlayerStats? snapshot = TryGetPlayerStats("PlayerStatsAttemptSnapshot");

            if (permanent.HasValue && snapshot.HasValue)
            {
                TimeSpan time = permanent.Value.timeSpan - snapshot.Value.timeSpan;

                if (time.TotalMilliseconds >= 0)
                {
                    return time;
                }
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

        private Location[] LoadLocations()
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

            string name = rawName;

            if (name.StartsWith("LOCATION_"))
            {
                name = name.Substring("LOCATION_".Length);
            }

            return name.Replace('_', ' ');
        }
    }
}
