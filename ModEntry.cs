using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using EntityComponent;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.MiscSystems.Achievements;
using JumpKing.MiscSystems.LocationText;
using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.Player;

namespace JKMetricsLite
{
    [JumpKingMod("eski4869.JKMetricsLite")]
    public static class JKMetricsLiteMod
    {
        private const string SettingsFileName = "eski4869.JKMetricsLite.Settings.xml";

        private static ScreenStayStatsBehaviour _registeredBehaviour;
        private static MetricsPreferences _preferences;
        private static string _settingsPath;
        private static bool _settingsDirty;
        private static bool _processExitRegistered;

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            EnsurePreferencesLoaded();

            if (_preferences.IsEnabled)
            {
                ScreenStayStatsBehaviour.PrepareForLevelLoad();
            }
            else
            {
                ScreenStayStatsBehaviour.ClearLevelLoadPreparation();
            }
        }

        [OnLevelStart]
        public static void OnLevelStart()
        {
            EnsurePreferencesLoaded();

            if (!_preferences.IsEnabled)
            {
                UnregisterMetricsBehaviour();
                return;
            }

            RegisterMetricsBehaviour();
        }

        private static void RegisterMetricsBehaviour()
        {
            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();

            if (player == null)
            {
                return;
            }

            if (_registeredBehaviour != null)
            {
                try
                {
                    player.m_body.RemoveBehaviour(_registeredBehaviour);
                }
                catch (Exception ex)
                {
                    ScreenStayStatsBehaviour.LogError("Remove previous behaviour", ex);
                }
            }

            _registeredBehaviour = new ScreenStayStatsBehaviour();
            player.m_body.RegisterBehaviour(_registeredBehaviour);
        }

        private static void UnregisterMetricsBehaviour()
        {
            if (_registeredBehaviour == null)
            {
                return;
            }

            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();

            if (player != null)
            {
                try
                {
                    player.m_body.RemoveBehaviour(_registeredBehaviour);
                }
                catch (Exception ex)
                {
                    ScreenStayStatsBehaviour.LogError("Remove metrics behaviour", ex);
                }
            }

            _registeredBehaviour = null;
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            SaveSettingsIfDirty();

            if (IsMetricsEnabled())
            {
                ScreenStayStatsBehaviour.FlushOnLevelEnd();
            }
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            SaveSettingsIfDirty();

            if (IsMetricsEnabled())
            {
                ScreenStayStatsBehaviour.FlushOnLevelUnload();
            }
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static MetricsToggle MetricsMenu(object factory, GuiFormat format)
        {
            return new MetricsToggle();
        }

        [PauseMenuItemSetting]
        public static CurrentAreaMetricsToggle CurrentAreaMetricsMenu(object factory, GuiFormat format)
        {
            return new CurrentAreaMetricsToggle();
        }

        public static bool IsMetricsEnabled()
        {
            EnsurePreferencesLoaded();
            return _preferences.IsEnabled;
        }

        public static void SetMetricsEnabled(bool isEnabled)
        {
            EnsurePreferencesLoaded();

            if (_preferences.IsEnabled == isEnabled)
            {
                return;
            }

            _preferences.IsEnabled = isEnabled;
            _settingsDirty = true;

            if (isEnabled)
            {
                RegisterMetricsBehaviour();
            }
            else
            {
                UnregisterMetricsBehaviour();
            }
        }

        private static void EnsurePreferencesLoaded()
        {
            if (_preferences != null)
            {
                RegisterProcessExit();
                return;
            }

            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _settingsPath = Path.Combine(assemblyDir, SettingsFileName);

            try
            {
                if (File.Exists(_settingsPath))
                {
                    var serializer = new XmlSerializer(typeof(MetricsPreferences));

                    using (var stream = File.OpenRead(_settingsPath))
                    {
                        _preferences = (MetricsPreferences)serializer.Deserialize(stream);
                    }
                }
            }
            catch
            {
            }

            if (_preferences == null)
            {
                _preferences = new MetricsPreferences();
                _settingsDirty = true;
            }

            RegisterProcessExit();
        }

        private static void RegisterProcessExit()
        {
            if (_processExitRegistered)
            {
                return;
            }

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            _processExitRegistered = true;
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            SaveSettingsIfDirty();
        }

        private static void SaveSettingsIfDirty()
        {
            if (!_settingsDirty || _preferences == null)
            {
                return;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(MetricsPreferences));

                using (var stream = File.Create(_settingsPath))
                {
                    serializer.Serialize(stream, _preferences);
                }

                _settingsDirty = false;
            }
            catch
            {
            }
        }
    }

    public class MetricsToggle : ITextToggle
    {
        public MetricsToggle() : base(JKMetricsLiteMod.IsMetricsEnabled())
        {
        }

        protected override string GetName()
        {
            return "JK Metrics Lite";
        }

        protected override void OnToggle()
        {
            JKMetricsLiteMod.SetMetricsEnabled(toggle);
        }
    }

    public class CurrentAreaMetricsToggle : ITextToggle
    {
        public CurrentAreaMetricsToggle() : base(ScreenStayStatsBehaviour.IsCurrentAreaExcludedFromMetrics())
        {
        }

        protected override string GetName()
        {
            return "Exclude This Area from Metrics";
        }

        protected override bool CanChange()
        {
            return ScreenStayStatsBehaviour.CanChangeCurrentAreaMetricsExclusion();
        }

        protected override void OnToggle()
        {
            ScreenStayStatsBehaviour.SetCurrentAreaExcludedFromMetrics(toggle);
        }
    }

    public class MetricsPreferences
    {
        public bool IsEnabled { get; set; } = true;
    }

    public partial class ScreenStayStatsBehaviour : IBodyCompBehaviour
    {
        private const int MinScreen = 1;
        private const int MaxScreen = 169;
        private const int ScreenCount = 170;

        private const int OutputIntervalFrames = 60;
        private const int StateSaveIntervalFrames = 3600;
        private const int ActivitySampleIntervalFrames = 3600;
        private const string OutputFolderName = "JKMetricsLite";
        private const string ConfigFileName = "JKMetricsLite.env";
        private const string OutputDirKey = "OUTPUT_DIR";

        private static ScreenStayStatsBehaviour _instance;
        private static bool _processExitRegistered = false;

        private readonly int[] _screenFrames = new int[ScreenCount];

        private readonly Dictionary<string, int> _areaFrames = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _areaFirstReachedFrames = new Dictionary<string, int>();
        private readonly List<string> _areaAppearedOrder = new List<string>();
        private readonly HashSet<string> _excludedAreas = new HashSet<string>();

        // Area-internal screen order is also based on first-reached order.
        private readonly Dictionary<string, List<int>> _areaScreenAppearedOrder =
            new Dictionary<string, List<int>>();

        private readonly string _outputDir;
        private readonly string _statePath;
        private readonly string _areaBarGraphPath;
        private readonly string _screenBarGraphPath;
        private readonly string _screenTimelinePath;
        private readonly string _progressStatusPath;
        private readonly string _activitySamplesPath;

        private Location[] _locations = new Location[0];

        private int _totalFrames = 0;
        private int _outputCounter = 0;
        private int _stateSaveCounter = 0;
        private int _activitySampleCounter = 0;
        private int _lastScreen = -1;
        private int _lastTimelineAppendFrames = -1;
        private string _lastArea = "Unknown";

        // PB is based on first-reached area order + first-reached screen order inside that area.
        private string _pbArea = "";
        private int _pbAreaIndex = -1;
        private int _pbScreenInArea = -1;
        private int _pbScreen = -1;

        private int? _stateAttempt = null;

        private sealed class LevelLoadPreparation
        {
            public string OutputDir;
            public string StatePath;
            public string AreaBarGraphPath;
            public string ScreenBarGraphPath;
            public string ScreenTimelinePath;
            public string ProgressStatusPath;
            public string ActivitySamplesPath;
        }

        private static LevelLoadPreparation _levelLoadPreparation;

        internal static void PrepareForLevelLoad()
        {
            RegisterProcessExitHandler();

            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            WriteDefaultConfigFileIfMissing(assemblyDir);

            string outputDir = ResolveOutputDir(assemblyDir);
            Directory.CreateDirectory(outputDir);
            SetLogOutputDir(outputDir);

            var preparation = new LevelLoadPreparation
            {
                OutputDir = outputDir,
                StatePath = Path.Combine(outputDir, "metrics_state.tsv"),
                AreaBarGraphPath = Path.Combine(outputDir, "area_bar_graph.tsv"),
                ScreenBarGraphPath = Path.Combine(outputDir, "screen_bar_graph.tsv"),
                ScreenTimelinePath = Path.Combine(outputDir, "screen_timeline.tsv"),
                ProgressStatusPath = Path.Combine(outputDir, "progress_status.tsv"),
                ActivitySamplesPath = Path.Combine(outputDir, "jump_activity.tsv")
            };

            WriteOverlayHtmlIfMissing(outputDir, "area_name.html", LoadOverlayTemplate(AreaNameTemplateName));
            WriteOverlayHtmlIfMissing(outputDir, "area_no.html", LoadOverlayTemplate(AreaNoTemplateName));
            WriteOverlayHtmlIfMissing(outputDir, "area_name_speedrun.html", LoadOverlayTemplate(AreaNameSpeedrunTemplateName));
            WriteOverlayHtmlIfMissing(outputDir, "screen_timeline.html", LoadOverlayTemplate(ScreenTimelineTemplateName));
            WriteOverlayHtmlIfMissing(outputDir, "jump_activity.html", LoadOverlayTemplate(JumpActivityTemplateName));

            _levelLoadPreparation = preparation;
        }

        internal static void ClearLevelLoadPreparation()
        {
            _levelLoadPreparation = null;
        }

        private static LevelLoadPreparation GetLevelLoadPreparation()
        {
            if (_levelLoadPreparation == null)
            {
                PrepareForLevelLoad();
            }

            return _levelLoadPreparation;
        }

        public ScreenStayStatsBehaviour()
        {
            _instance = this;

            LevelLoadPreparation preparation = GetLevelLoadPreparation();

            _outputDir = preparation.OutputDir;
            SetLogOutputDir(_outputDir);

            _statePath = preparation.StatePath;
            _areaBarGraphPath = preparation.AreaBarGraphPath;
            _screenBarGraphPath = preparation.ScreenBarGraphPath;
            _screenTimelinePath = preparation.ScreenTimelinePath;
            _progressStatusPath = preparation.ProgressStatusPath;
            _activitySamplesPath = preparation.ActivitySamplesPath;
            _locations = LoadLocations();

            AppendActivitySampleTsv();

            int? currentAttempt = TryGetCurrentAttempt();

            if (LoadStateIfSameAttempt(currentAttempt))
            {
                ReconcileLoadedStateWithGameTime();
                RepairPbIfNeeded();
            }
            else
            {
                ResetStats();
                _stateAttempt = currentAttempt;
                ResetTimelineFile();
            }

            WriteOutputFiles(false);
            SaveState();
        }

        private static void WriteDefaultConfigFileIfMissing(string assemblyDir)
        {
            try
            {
                string configPath = Path.Combine(assemblyDir, ConfigFileName);

                if (File.Exists(configPath))
                {
                    return;
                }

                string content =
                    "# JK Metrics Lite config" + Environment.NewLine +
                    "# Leave OUTPUT_DIR empty to use the default JKMetricsLite folder in the same folder as this mod." + Environment.NewLine +
                    "# Relative paths are based on this mod's folder. Absolute paths are also supported." + Environment.NewLine +
                    "OUTPUT_DIR=" + Environment.NewLine;

                File.WriteAllText(configPath, content, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogError("Write default config", ex);
            }
        }

        private static string ResolveOutputDir(string assemblyDir)
        {
            string defaultOutputDir = Path.Combine(assemblyDir, OutputFolderName);

            try
            {
                string configPath = Path.Combine(assemblyDir, ConfigFileName);

                if (!File.Exists(configPath))
                {
                    return defaultOutputDir;
                }

                foreach (string line in File.ReadAllLines(configPath, Encoding.UTF8))
                {
                    string trimmedLine = line.Trim();

                    if (trimmedLine.Length == 0 || trimmedLine.StartsWith("#"))
                    {
                        continue;
                    }

                    int separatorIndex = trimmedLine.IndexOf('=');

                    if (separatorIndex < 0)
                    {
                        continue;
                    }

                    string key = trimmedLine.Substring(0, separatorIndex).Trim();

                    if (!string.Equals(key, OutputDirKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string value = trimmedLine.Substring(separatorIndex + 1).Trim().Trim('"');

                    if (value.Length == 0)
                    {
                        return defaultOutputDir;
                    }

                    value = Environment.ExpandEnvironmentVariables(value);

                    string configuredOutputDir = Path.IsPathRooted(value)
                        ? Path.GetFullPath(value)
                        : Path.GetFullPath(Path.Combine(assemblyDir, value));

                    Directory.CreateDirectory(configuredOutputDir);
                    return configuredOutputDir;
                }
            }
            catch (Exception ex)
            {
                LogError("Read config", ex);
            }

            return defaultOutputDir;
        }

        private static void RegisterProcessExitHandler()
        {
            if (_processExitRegistered)
            {
                return;
            }

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            _processExitRegistered = true;
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            FlushOnExit();
        }

        public static void FlushOnExit()
        {
            if (!JKMetricsLiteMod.IsMetricsEnabled())
            {
                return;
            }

            FlushCurrentInstance(true, false);
        }

        public static void FlushOnLevelEnd()
        {
            FlushCurrentInstance(true, true);
        }

        public static void FlushOnLevelUnload()
        {
            FlushCurrentInstance(false, false);
        }

        private static void FlushCurrentInstance(bool appendTimeline, bool appendActivity)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.WriteOutputFiles(appendTimeline);
            _instance.SaveState();

            if (appendActivity)
            {
                _instance.AppendActivitySampleTsv();
            }
        }

        public bool ExecuteBehaviour(BehaviourContext behaviourContext)
        {
            if (_locations == null || _locations.Length == 0)
            {
                _locations = LoadLocations();
            }

            int screen = JumpKing.Camera.CurrentScreen + 1;

            if (screen >= MinScreen && screen <= MaxScreen)
            {
                _screenFrames[screen]++;
                _lastScreen = screen;

                string areaName = GetAreaNameForScreen(screen);
                _lastArea = areaName;

                // Unknown is intentionally excluded from area statistics and PB.
                if (areaName != "Unknown")
                {
                    if (!_areaFrames.ContainsKey(areaName))
                    {
                        _areaFrames[areaName] = 0;
                    }

                    if (!_areaFirstReachedFrames.ContainsKey(areaName))
                    {
                        _areaFirstReachedFrames[areaName] = _totalFrames;
                    }

                    if (!_areaAppearedOrder.Contains(areaName))
                    {
                        _areaAppearedOrder.Add(areaName);
                    }

                    RegisterAreaScreenIfNeeded(areaName, screen);
                    UpdatePbIfNeeded(screen, areaName);

                    _areaFrames[areaName]++;
                }
            }

            _totalFrames++;
            _outputCounter++;

            if (_outputCounter >= OutputIntervalFrames)
            {
                _outputCounter = 0;
                WriteOutputFiles(true);
            }

            _stateSaveCounter++;

            if (_stateSaveCounter >= StateSaveIntervalFrames)
            {
                _stateSaveCounter = 0;
                SaveState();
            }

            _activitySampleCounter++;

            if (_activitySampleCounter >= ActivitySampleIntervalFrames)
            {
                _activitySampleCounter = 0;
                AppendActivitySampleTsv();
            }

            return true;
        }

        private void WriteOutputFiles(bool appendTimeline)
        {
            WriteAreaBarGraphTsv();
            WriteScreenBarGraphTsv();
            WriteProgressStatusTsv();

            if (appendTimeline)
            {
                AppendScreenTimelineTsv();
            }
          }
    }
}

