using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using EntityComponent;
using JumpKing.MiscSystems.Achievements;
using JumpKing.MiscSystems.LocationText;
using JumpKing.Player;

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

            _screenOrderRevision++;
            RecalculatePb();
            WriteOutputFiles();
        }

        private bool IsAreaIncludedForMetrics(string areaName)
        {
            if (string.IsNullOrEmpty(areaName) || areaName == "Unknown")
            {
                return false;
            }

            return !_excludedAreas.Contains(areaName);
        }

        private void RecordAreaFirstReach(string areaName)
        {
            long firstReachedMilliseconds = FramesToMilliseconds(_totalFrames);

            if (_totalFrames > 0 || _areaFirstReachedMilliseconds.Count > 0)
            {
                TimeSpan? currentRunTime = PlayerStatsReader.TryGetCurrentRunTime();

                if (currentRunTime.HasValue && currentRunTime.Value.TotalMilliseconds >= 0)
                {
                    firstReachedMilliseconds = (long)Math.Round(currentRunTime.Value.TotalMilliseconds);
                }
            }

            _areaFirstReachedMilliseconds[areaName] = firstReachedMilliseconds;
        }

        private void RecordAreaFirstLanding(string areaName)
        {
            long firstLandedMilliseconds = _totalFrames == 0
                ? 0
                : GetCurrentRunMilliseconds();

            _areaFirstLandedMilliseconds[areaName] = firstLandedMilliseconds;
        }

        private void RecordBabeClearTimeIfNeeded(int screen)
        {
            if (_babeClearTimeMilliseconds.HasValue ||
                !_endingScreens.Contains(screen) ||
                !IsPlayerOnGround())
            {
                return;
            }

            _babeClearTimeMilliseconds = GetCurrentRunMilliseconds();
        }

        private void LoadEndingScreens()
        {
            _endingScreens.Clear();

            if (!TryLoadCustomEndingScreens())
            {
                LoadOfficialEndingScreens();
            }
        }

        private bool TryLoadCustomEndingScreens()
        {
            try
            {
                object contentManager = JumpKing.Game1.instance.contentManager;
                FieldInfo rootField = contentManager.GetType().GetField(
                    "root",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                string root = rootField == null
                    ? ""
                    : rootField.GetValue(contentManager) as string;

                if (root == "Content")
                {
                    return false;
                }

                FieldInfo levelField = contentManager.GetType().GetField(
                    "level",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                object level = levelField == null
                    ? null
                    : levelField.GetValue(contentManager);

                if (level == null)
                {
                    return false;
                }

                PropertyInfo infoProperty = level.GetType().GetProperty(
                    "Info",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                object info = infoProperty == null
                    ? null
                    : infoProperty.GetValue(level, null);

                FieldInfo aboutField = info == null
                    ? null
                    : info.GetType().GetField(
                        "About",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                    );

                object about = aboutField == null
                    ? null
                    : aboutField.GetValue(info);

                if (about == null)
                {
                    return false;
                }

                AddEndingScreenFromField(about, "ending_screen");
                AddEndingScreenFromField(about, "ending_screen_second");
                AddEndingScreenFromField(about, "ending_screen_third");

                return _endingScreens.Count > 0;
            }
            catch (Exception ex)
            {
                LogError("Load custom ending screens", ex);
                return false;
            }
        }

        private void AddEndingScreenFromField(object about, string fieldName)
        {
            FieldInfo field = about.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (field == null)
            {
                return;
            }

            object value = field.GetValue(about);

            if (value is int)
            {
                AddEndingScreen((int)value);
            }
            else if (value != null)
            {
                Type valueType = value.GetType();
                PropertyInfo hasValueProperty = valueType.GetProperty("HasValue");
                PropertyInfo valueProperty = valueType.GetProperty("Value");

                if (hasValueProperty != null &&
                    valueProperty != null &&
                    (bool)hasValueProperty.GetValue(value, null))
                {
                    AddEndingScreen((int)valueProperty.GetValue(value, null));
                }
            }
        }

        private void LoadOfficialEndingScreens()
        {
            AddOfficialEndingScreen(
                "JumpKing.GameManager.MultiEnding.NormalEnding.NormalEnding"
            );
            AddOfficialEndingScreen(
                "JumpKing.GameManager.MultiEnding.NewBabePlusEnding.NewBabePlusEnding"
            );
            AddOfficialEndingScreen(
                "JumpKing.GameManager.MultiEnding.OwlEnding.OwlEnding"
            );
        }

        private void AddOfficialEndingScreen(string typeName)
        {
            Type type = typeof(JumpKing.Game1).Assembly.GetType(typeName);

            if (type == null)
            {
                return;
            }

            FieldInfo field = type.GetField(
                "ENDING_SCREEN0",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (field != null && field.GetValue(null) is int)
            {
                AddEndingScreen((int)field.GetValue(null) + 1);
            }
        }

        private void AddEndingScreen(int screen)
        {
            if (screen >= MinScreen && screen <= MaxScreen)
            {
                _endingScreens.Add(screen);
            }
        }

        private long GetCurrentRunMilliseconds()
        {
            TimeSpan? currentRunTime = PlayerStatsReader.TryGetCurrentRunTime();

            if (currentRunTime.HasValue && currentRunTime.Value.TotalMilliseconds >= 0)
            {
                return (long)Math.Round(currentRunTime.Value.TotalMilliseconds);
            }

            return FramesToMilliseconds(_totalFrames);
        }

        private bool IsPlayerOnGround()
        {
            if (_playerBody == null)
            {
                _playerBody = TryGetPlayerBody();
            }

            return _playerBody != null && _playerBody.IsOnGround;
        }

        private BodyComp TryGetPlayerBody()
        {
            try
            {
                PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();

                return player != null ? player.m_body : null;
            }
            catch (Exception ex)
            {
                LogError("Get player body", ex);
                return null;
            }
        }

        private bool RegisterAreaScreenIfNeeded(string areaName, int screen)
        {
            if (areaName == "Unknown")
            {
                return false;
            }

            if (!_areaScreenAppearedOrder.ContainsKey(areaName))
            {
                _areaScreenAppearedOrder[areaName] = new List<int>();
            }

            if (!_areaScreenAppearedOrder[areaName].Contains(screen))
            {
                if (DoesNewScreenChangeExistingGraphOrder(areaName))
                {
                    _screenOrderRevision++;
                }

                _areaScreenAppearedOrder[areaName].Add(screen);
                AppendScreenOrderTsv(areaName, screen);
                return true;
            }

            return false;
        }

        private bool DoesNewScreenChangeExistingGraphOrder(string areaName)
        {
            if (!IsAreaIncludedForMetrics(areaName))
            {
                return false;
            }

            string lastIncludedArea = "";

            for (int i = 0; i < _areaAppearedOrder.Count; i++)
            {
                string area = _areaAppearedOrder[i];

                if (IsAreaIncludedForMetrics(area) && _areaFrames.ContainsKey(area))
                {
                    lastIncludedArea = area;
                }
            }

            return lastIncludedArea.Length > 0 && lastIncludedArea != areaName;
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
            PlayerStats? stats = PlayerStatsReader.TryGetPlayerStats("PlayerStatsAttemptSnapshot");

            if (stats.HasValue)
            {
                return stats.Value.attempts;
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
