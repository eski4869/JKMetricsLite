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
        private static readonly HashSet<string> ChargeableBlockNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "SandBlock",
            "Quicksand",
            "SideSand",
            "UpSand",
            "MagicSand",
            "InfinityJump",
            "WallJump",
            "AirJump",
            "AirDash",
            "Flapping"
        };

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

        private void RecordClearTimeIfNeeded()
        {
            if (_babeClearTimeMilliseconds.HasValue ||
                !IsOfficialWinConditionMet())
            {
                return;
            }

            _babeClearTimeMilliseconds =
                GetCurrentRunMilliseconds() + FramesToMilliseconds(1);

            WriteAreaProgressTsv();
        }

        private bool IsOfficialWinConditionMet()
        {
            if (_player == null)
            {
                _player = EntityManager.instance.Find<PlayerEntity>();
            }

            if (_player == null)
            {
                return false;
            }

            for (int i = 0; i < _officialEndings.Length; i++)
            {
                if (_officialEndings[i].CheckWin(_player))
                {
                    return true;
                }
            }

            return false;
        }

        private void LoadBabeScreens()
        {
            _babeScreens.Clear();
            AddBabeScreen(JumpKing.GameManager.MultiEnding.NormalEnding.NormalEnding.ENDING_SCREEN0 + 1);
            AddBabeScreen(JumpKing.GameManager.MultiEnding.NewBabePlusEnding.NewBabePlusEnding.ENDING_SCREEN0 + 1);
            AddBabeScreen(JumpKing.GameManager.MultiEnding.OwlEnding.OwlEnding.ENDING_SCREEN0 + 1);
        }

        private void AddBabeScreen(int screen)
        {
            if (screen >= MinScreen && screen <= MaxScreen)
            {
                _babeScreens.Add(screen);
            }
        }

        private void RecordBabeScreenSplitIfNeeded(int screen, bool playerLanded)
        {
            if (!_babeScreens.Contains(screen))
            {
                return;
            }

            if (!_babeScreenEntryMilliseconds.HasValue)
            {
                _babeScreenEntryMilliseconds = GetScreenEventMilliseconds();
            }

            if (playerLanded && !_babeScreenLandingMilliseconds.HasValue)
            {
                _babeScreenLandingMilliseconds = GetScreenEventMilliseconds();
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

        private bool IsPlayerLandedForMetrics()
        {
            if (_playerBody == null)
            {
                _playerBody = TryGetPlayerBody();
            }

            if (_playerBody == null)
            {
                return false;
            }

            return !_playerBody.IsKnocked &&
                (_playerBody.IsOnGround || IsPlayerOnChargeableBlock());
        }

        private bool IsPlayerOnChargeableBlock()
        {
            try
            {
                foreach (Type blockType in _playerBody.OnBlocks())
                {
                    if (blockType != null && ChargeableBlockNames.Contains(blockType.Name))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Check chargeable block", ex);
            }

            return false;
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

        private bool RegisterScreenEntryIfNeeded(string areaName, int screen)
        {
            if (areaName == "Unknown")
            {
                return false;
            }

            if (!_areaScreenEnteredOrder.ContainsKey(areaName))
            {
                _areaScreenEnteredOrder[areaName] = new List<int>();
            }

            if (_areaScreenEnteredOrder[areaName].Contains(screen))
            {
                return false;
            }

            _areaScreenEnteredOrder[areaName].Add(screen);
            AppendScreenEventTsv(areaName, screen, "entry");
            return true;
        }

        private bool RegisterScreenLandingIfNeeded(string areaName, int screen)
        {
            if (areaName == "Unknown")
            {
                return false;
            }

            if (!_areaScreenAppearedOrder.ContainsKey(areaName))
            {
                _areaScreenAppearedOrder[areaName] = new List<int>();
            }

            if (_areaScreenAppearedOrder[areaName].Contains(screen))
            {
                return false;
            }

            if (DoesNewScreenChangeExistingGraphOrder(areaName))
            {
                _screenOrderRevision++;
            }

            _areaScreenAppearedOrder[areaName].Add(screen);
            AppendScreenEventTsv(areaName, screen, "landing");
            return true;
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

            int matchCount = 0;
            bool found = false;
            Location firstLocation = default(Location);
            Location closestUnlockLocation = default(Location);
            int closestUnlockDistance = int.MaxValue;
            int closestUnlock = int.MinValue;
            int closestUnlockStart = int.MinValue;

            for (int i = 0; i < _locations.Length; i++)
            {
                Location location = _locations[i];

                if (screen < location.start || screen > location.end)
                {
                    continue;
                }

                matchCount++;

                if (!found)
                {
                    firstLocation = location;
                    found = true;
                }

                int unlockDistance = Math.Abs(screen - location.unlock);

                if (unlockDistance < closestUnlockDistance ||
                    (unlockDistance == closestUnlockDistance && location.unlock > closestUnlock) ||
                    (unlockDistance == closestUnlockDistance &&
                        location.unlock == closestUnlock &&
                        location.start > closestUnlockStart))
                {
                    closestUnlockLocation = location;
                    closestUnlockDistance = unlockDistance;
                    closestUnlock = location.unlock;
                    closestUnlockStart = location.start;
                }
            }

            if (!found)
            {
                return false;
            }

            matchedLocation = matchCount > 1
                ? closestUnlockLocation
                : firstLocation;

            return true;
        }

        private bool IsAreaUnlocked(string areaName)
        {
            if (string.IsNullOrEmpty(areaName) ||
                areaName == "Unknown" ||
                areaName == BabeScreenAreaName ||
                areaName == CompletionAreaName)
            {
                return true;
            }

            if (_locations == null || _locations.Length == 0)
            {
                return true;
            }

            List<int> screens;

            if (!_areaScreenAppearedOrder.TryGetValue(areaName, out screens))
            {
                return false;
            }

            bool foundLocation = false;

            for (int i = 0; i < _locations.Length; i++)
            {
                Location location = _locations[i];

                if (FormatAreaName(location.name) != areaName)
                {
                    continue;
                }

                foundLocation = true;

                for (int j = 0; j < screens.Count; j++)
                {
                    if (screens[j] >= location.unlock && screens[j] >= location.start && screens[j] <= location.end)
                    {
                        return true;
                    }
                }
            }

            return !foundLocation;
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
