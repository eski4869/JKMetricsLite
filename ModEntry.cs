using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using EntityComponent;
using JumpKing.API;
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

        private static ScreenStayStatsBehaviour _registeredAttemptBehaviour;
        private static TotalMetricsBehaviour _registeredTotalBehaviour;
        private static MetricsPreferences _preferences;
        private static string _settingsPath;
        private static bool _settingsDirty;
        private static bool _processExitRegistered;

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            EnsurePreferencesLoaded();
            JKMetricsDebugLog.Write(
                "BeforeLevelLoad: attempt=" + _preferences.AttemptMetricsEnabled +
                ", total=" + _preferences.TotalMetricsEnabled
            );

            if (AreAnyMetricsEnabled())
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
            JKMetricsDebugLog.Write(
                "OnLevelStart: attempt=" + _preferences.AttemptMetricsEnabled +
                ", total=" + _preferences.TotalMetricsEnabled
            );
            RegisterMetricsBehaviours();
        }

        private static void RegisterMetricsBehaviours()
        {
            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();

            if (player == null)
            {
                JKMetricsDebugLog.Write("RegisterMetricsBehaviours: player not found");
                return;
            }

            RegisterAttemptMetricsBehaviour(player);
            RegisterTotalMetricsBehaviour(player);
        }

        private static void RegisterAttemptMetricsBehaviour(PlayerEntity player)
        {
            ScreenStayStatsBehaviour existingBehaviour = player.GetComponent<ScreenStayStatsBehaviour>();

            if (existingBehaviour != null)
            {
                _registeredAttemptBehaviour = existingBehaviour;
                _registeredAttemptBehaviour.Enabled = _preferences.AttemptMetricsEnabled;
                JKMetricsDebugLog.Write(
                    "Attempt component reused: enabled=" + _registeredAttemptBehaviour.Enabled
                );
                return;
            }

            _registeredAttemptBehaviour = new ScreenStayStatsBehaviour();
            player.AddComponents(
                _preferences.AttemptMetricsEnabled,
                new Component[] { _registeredAttemptBehaviour }
            );
            JKMetricsDebugLog.Write(
                "Attempt component added: enabled=" + _preferences.AttemptMetricsEnabled
            );
        }

        private static void RegisterTotalMetricsBehaviour(PlayerEntity player)
        {
            TotalMetricsBehaviour existingBehaviour = player.GetComponent<TotalMetricsBehaviour>();

            if (existingBehaviour != null)
            {
                _registeredTotalBehaviour = existingBehaviour;
                _registeredTotalBehaviour.Enabled = _preferences.TotalMetricsEnabled;
                JKMetricsDebugLog.Write(
                    "Total component reused: enabled=" + _registeredTotalBehaviour.Enabled
                );
                return;
            }

            _registeredTotalBehaviour = new TotalMetricsBehaviour();
            player.AddComponents(
                _preferences.TotalMetricsEnabled,
                new Component[] { _registeredTotalBehaviour }
            );
            JKMetricsDebugLog.Write(
                "Total component added: enabled=" + _preferences.TotalMetricsEnabled
            );
        }

        internal static void ClearRegisteredAttemptMetricsBehaviour(ScreenStayStatsBehaviour behaviour)
        {
            if (ReferenceEquals(_registeredAttemptBehaviour, behaviour))
            {
                _registeredAttemptBehaviour = null;
            }
        }

        internal static void ClearRegisteredTotalMetricsBehaviour(TotalMetricsBehaviour behaviour)
        {
            if (ReferenceEquals(_registeredTotalBehaviour, behaviour))
            {
                _registeredTotalBehaviour = null;
            }
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            JKMetricsDebugLog.Write("OnLevelEnd");
            SaveSettingsIfDirty();
            ScreenStayStatsBehaviour.FlushOnLevelEnd();
            TotalMetricsBehaviour.FlushOnLevelEnd();
            _registeredAttemptBehaviour = null;
            _registeredTotalBehaviour = null;
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            JKMetricsDebugLog.Write("OnLevelUnload");
            SaveSettingsIfDirty();
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static AttemptMetricsToggle AttemptMetricsMenu(object factory, GuiFormat format)
        {
            return new AttemptMetricsToggle();
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static TotalMetricsToggle TotalMetricsMenu(object factory, GuiFormat format)
        {
            return new TotalMetricsToggle();
        }

        [PauseMenuItemSetting]
        public static CurrentAreaMetricsToggle CurrentAreaMetricsMenu(object factory, GuiFormat format)
        {
            return new CurrentAreaMetricsToggle();
        }

        public static bool IsAttemptMetricsEnabled()
        {
            EnsurePreferencesLoaded();
            return _preferences.AttemptMetricsEnabled;
        }

        public static bool IsTotalMetricsEnabled()
        {
            EnsurePreferencesLoaded();
            return _preferences.TotalMetricsEnabled;
        }

        internal static bool AreAnyMetricsEnabled()
        {
            EnsurePreferencesLoaded();
            return _preferences.AttemptMetricsEnabled || _preferences.TotalMetricsEnabled;
        }

        public static void SetAttemptMetricsEnabled(bool isEnabled)
        {
            EnsurePreferencesLoaded();

            if (_preferences.AttemptMetricsEnabled == isEnabled)
            {
                return;
            }

            _preferences.AttemptMetricsEnabled = isEnabled;
            _settingsDirty = true;
            JKMetricsDebugLog.Write("Attempt toggle: enabled=" + isEnabled);
            ApplyAttemptMetricsEnabledToRegisteredBehaviour();
        }

        public static void SetTotalMetricsEnabled(bool isEnabled)
        {
            EnsurePreferencesLoaded();

            if (_preferences.TotalMetricsEnabled == isEnabled)
            {
                return;
            }

            _preferences.TotalMetricsEnabled = isEnabled;
            _settingsDirty = true;
            JKMetricsDebugLog.Write("Total toggle: enabled=" + isEnabled);
            ApplyTotalMetricsEnabledToRegisteredBehaviour();
        }

        private static void ApplyAttemptMetricsEnabledToRegisteredBehaviour()
        {
            if (_registeredAttemptBehaviour == null ||
                _registeredAttemptBehaviour.gameObject == null ||
                !_registeredAttemptBehaviour.gameObject.IsAlive)
            {
                return;
            }

            _registeredAttemptBehaviour.Enabled = _preferences.AttemptMetricsEnabled;
        }

        private static void ApplyTotalMetricsEnabledToRegisteredBehaviour()
        {
            if (_registeredTotalBehaviour == null ||
                _registeredTotalBehaviour.gameObject == null ||
                !_registeredTotalBehaviour.gameObject.IsAlive)
            {
                return;
            }

            _registeredTotalBehaviour.Enabled = _preferences.TotalMetricsEnabled;
        }

        internal static string GetConfiguredOutputDir()
        {
            EnsurePreferencesLoaded();
            return _preferences.OutputDir ?? "";
        }

        internal static int GetAttemptBackupGenerations()
        {
            EnsurePreferencesLoaded();
            return _preferences.AttemptBackupGenerations;
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

            NormalizePreferences();

            RegisterProcessExit();
            SaveSettingsIfDirty();
        }

        private static void NormalizePreferences()
        {
            if (_preferences.OutputDir == null)
            {
                _preferences.OutputDir = "";
                _settingsDirty = true;
            }

            int clampedBackupGenerations = Math.Max(
                0,
                Math.Min(10, _preferences.AttemptBackupGenerations)
            );

            if (_preferences.AttemptBackupGenerations != clampedBackupGenerations)
            {
                _preferences.AttemptBackupGenerations = clampedBackupGenerations;
                _settingsDirty = true;
            }
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

    public class AttemptMetricsToggle : ITextToggle
    {
        public AttemptMetricsToggle() : base(JKMetricsLiteMod.IsAttemptMetricsEnabled())
        {
        }

        protected override string GetName()
        {
            return "Attempt Metrics";
        }

        protected override void OnToggle()
        {
            JKMetricsLiteMod.SetAttemptMetricsEnabled(toggle);
        }
    }

    public class TotalMetricsToggle : ITextToggle
    {
        public TotalMetricsToggle() : base(JKMetricsLiteMod.IsTotalMetricsEnabled())
        {
        }

        protected override string GetName()
        {
            return "Total Metrics";
        }

        protected override void OnToggle()
        {
            JKMetricsLiteMod.SetTotalMetricsEnabled(toggle);
        }
    }

    public class CurrentAreaMetricsToggle : ITextToggle
    {
        public CurrentAreaMetricsToggle() : base(ScreenStayStatsBehaviour.IsCurrentAreaExcludedFromMetrics())
        {
        }

        public override void Draw(int x, int y, bool selected)
        {
            OverrideToggle(ScreenStayStatsBehaviour.IsCurrentAreaExcludedFromMetrics());
            base.Draw(x, y, selected);
        }

        protected override string GetName()
        {
            return "Exclude This Area";
        }

        protected override bool CanChange()
        {
            return JKMetricsLiteMod.IsAttemptMetricsEnabled() &&
                ScreenStayStatsBehaviour.CanChangeCurrentAreaMetricsExclusion();
        }

        protected override void OnToggle()
        {
            ScreenStayStatsBehaviour.SetCurrentAreaExcludedFromMetrics(toggle);
        }
    }

    public class MetricsPreferences
    {
        public bool IsEnabled
        {
            get
            {
                return AttemptMetricsEnabled && TotalMetricsEnabled;
            }
            set
            {
                AttemptMetricsEnabled = value;
                TotalMetricsEnabled = value;
            }
        }

        public bool ShouldSerializeIsEnabled()
        {
            return false;
        }

        public bool AttemptMetricsEnabled { get; set; } = true;
        public bool TotalMetricsEnabled { get; set; } = true;
        public string OutputDir { get; set; } = "";
        public int AttemptBackupGenerations { get; set; } = 1;
    }

    public partial class ScreenStayStatsBehaviour : Component
    {
        private const int MinScreen = 1;
        private const int MaxScreen = 169;
        private const int OutputIntervalFrames = 60;
        private const string OutputFolderName = "JKMetricsLite";

        private static ScreenStayStatsBehaviour _instance;
        private static bool _processExitRegistered = false;
        private bool _runtimeInitialized = false;
        private bool _hasFlushed = false;

        private readonly Dictionary<string, int> _areaFrames = new Dictionary<string, int>();
        private readonly Dictionary<string, long> _areaFirstReachedMilliseconds =
            new Dictionary<string, long>();
        private readonly List<string> _areaAppearedOrder = new List<string>();
        private readonly HashSet<string> _excludedAreas = new HashSet<string>();

        // Area-internal screen order is also based on first-reached order.
        private readonly Dictionary<string, List<int>> _areaScreenAppearedOrder =
            new Dictionary<string, List<int>>();

        private string _outputDir;
        private string _areaProgressPath;
        private string _screenMetricsPath;
        private string _screenOrderPath;
        private string _statePath;
        private string _attemptsDir;
        private string _currentAttemptDir;
        private int _attemptBackupGenerations = 1;

        private Location[] _locations = new Location[0];

        private int _totalFrames = 0;
        private int _outputCounter = 0;
        private int _lastScreen = -1;
        private string _lastArea = "Unknown";
        private int _screenOrderRevision = 0;

        // PB is based on first-reached area order + first-reached screen order inside that area.
        private string _pbArea = "";
        private int _pbAreaIndex = -1;
        private int _pbScreenInArea = -1;
        private int _pbScreen = -1;

        private int? _attempt = null;

        private sealed class LevelLoadPreparation
        {
            public string OutputDir;
            public string AttemptsDir;
            public string CurrentAttemptDir;
            public string AreaProgressPath;
            public string ScreenMetricsPath;
            public string ScreenOrderPath;
            public string StatePath;
            public string TotalStatsPath;
            public int AttemptBackupGenerations;
        }

        private static LevelLoadPreparation _levelLoadPreparation;

        internal static void PrepareForLevelLoad()
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string outputDir = ResolveOutputDir(
                assemblyDir,
                JKMetricsLiteMod.GetConfiguredOutputDir()
            );
            string rawDataDir = Path.Combine(outputDir, "raw_data");
            string attemptsDir = Path.Combine(rawDataDir, "attempts");
            string currentAttemptDir = Path.Combine(attemptsDir, "current");
            string obsDir = Path.Combine(outputDir, "obs");
            string localDir = Path.Combine(outputDir, "local");

            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(rawDataDir);
            Directory.CreateDirectory(attemptsDir);
            Directory.CreateDirectory(currentAttemptDir);
            Directory.CreateDirectory(obsDir);
            Directory.CreateDirectory(localDir);
            SetLogOutputDir(outputDir);

            var preparation = new LevelLoadPreparation
            {
                OutputDir = outputDir,
                AttemptsDir = attemptsDir,
                CurrentAttemptDir = currentAttemptDir,
                AreaProgressPath = Path.Combine(currentAttemptDir, "area_progress.tsv"),
                ScreenMetricsPath = Path.Combine(currentAttemptDir, "screen_metrics.tsv"),
                ScreenOrderPath = Path.Combine(currentAttemptDir, "screen_order.tsv"),
                StatePath = Path.Combine(currentAttemptDir, "current_state.tsv"),
                TotalStatsPath = Path.Combine(rawDataDir, "total_metrics.tsv"),
                AttemptBackupGenerations = JKMetricsLiteMod.GetAttemptBackupGenerations()
            };

            WriteOverlayHtmlIfMissing(obsDir, "area_name_splits.html", LoadOverlayTemplate(AreaNameSplitTemplateName));
            WriteOverlayHtmlIfMissing(obsDir, "area_number_splits.html", LoadOverlayTemplate(AreaNumberSplitTemplateName));
            WriteOverlayHtmlIfMissing(obsDir, "area_name_splits_speedrun.html", LoadOverlayTemplate(AreaNameSplitSpeedrunTemplateName));
            WriteOverlayHtmlIfMissing(obsDir, "area_number_splits_speedrun.html", LoadOverlayTemplate(AreaNumberSplitSpeedrunTemplateName));
            WriteOverlayHtmlIfMissing(obsDir, "progress_graph.html", LoadOverlayTemplate(ScreenGraphTemplateName));
            WriteOverlayHtmlIfMissing(localDir, "recap.html", LoadOverlayTemplate(RecapTemplateName));

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

        internal static string GetPreparedTotalMetricsPath()
        {
            return GetLevelLoadPreparation().TotalStatsPath;
        }

        public ScreenStayStatsBehaviour()
        {
        }

        protected override void OnEnable()
        {
            _instance = this;
            _hasFlushed = false;
            JKMetricsDebugLog.Write(
                "Attempt OnEnable: initialized=" + _runtimeInitialized
            );

            if (!_runtimeInitialized)
            {
                InitializeForLevelStart();
            }
        }

        protected override void OnDisable()
        {
            JKMetricsDebugLog.Write(
                "Attempt OnDisable: initialized=" + _runtimeInitialized +
                ", flushed=" + _hasFlushed
            );
            FlushForStop();
        }

        protected override void OnOwnerDestroy()
        {
            JKMetricsDebugLog.Write(
                "Attempt OnOwnerDestroy: initialized=" + _runtimeInitialized +
                ", flushed=" + _hasFlushed
            );
            FlushForStop();

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }

            JKMetricsLiteMod.ClearRegisteredAttemptMetricsBehaviour(this);
        }

        private void InitializeForLevelStart()
        {
            _runtimeInitialized = true;
            RegisterProcessExitHandler();
            JKMetricsDebugLog.Write("Attempt InitializeForLevelStart");

            LevelLoadPreparation preparation = GetLevelLoadPreparation();

            _outputDir = preparation.OutputDir;
            SetLogOutputDir(_outputDir);

            _attemptsDir = preparation.AttemptsDir;
            _currentAttemptDir = preparation.CurrentAttemptDir;
            _areaProgressPath = preparation.AreaProgressPath;
            _screenMetricsPath = preparation.ScreenMetricsPath;
            _screenOrderPath = preparation.ScreenOrderPath;
            _statePath = preparation.StatePath;
            _attemptBackupGenerations = preparation.AttemptBackupGenerations;
            _hasFlushed = false;

            _locations = LoadLocations();

            int? currentAttempt = TryGetCurrentAttempt();
            bool loaded = LoadRunDataIfSameAttempt(currentAttempt);
            JKMetricsDebugLog.Write(
                "Attempt load: currentAttempt=" +
                (currentAttempt.HasValue ? currentAttempt.Value.ToString() : "null") +
                ", loaded=" + loaded
            );

            if (loaded)
            {
                SyncLoadedElapsedWithGameTime();
                RecalculatePb();
            }
            else
            {
                BackupCurrentAttemptIfNeeded(ReadSavedAttempt());
                ResetStats();
                _attempt = currentAttempt;
                ResetScreenMetricsFile();
            }

            TrackCurrentFrame();
            LogAttemptOutput("initialize", true, false, false);
            WriteOutputFiles();
        }

        private static string ResolveOutputDir(string assemblyDir, string configuredOutputDir)
        {
            string defaultOutputDir = Path.Combine(assemblyDir, OutputFolderName);

            try
            {
                if (string.IsNullOrWhiteSpace(configuredOutputDir))
                {
                    return defaultOutputDir;
                }

                string value = Environment.ExpandEnvironmentVariables(
                    configuredOutputDir.Trim().Trim('"')
                );

                string outputDir = Path.IsPathRooted(value)
                    ? Path.GetFullPath(value)
                    : Path.GetFullPath(Path.Combine(assemblyDir, value));

                Directory.CreateDirectory(outputDir);
                return outputDir;
            }
            catch (Exception ex)
            {
                LogError("Resolve output directory", ex);
            }

            return defaultOutputDir;
        }

        private void BackupCurrentAttemptIfNeeded(int? savedAttempt)
        {
            try
            {
                if (string.IsNullOrEmpty(_attemptsDir) ||
                    string.IsNullOrEmpty(_currentAttemptDir))
                {
                    return;
                }

                if (!Directory.Exists(_currentAttemptDir))
                {
                    Directory.CreateDirectory(_currentAttemptDir);
                    return;
                }

                bool hasCurrentData = Directory.GetFiles(
                    _currentAttemptDir,
                    "*.tsv"
                ).Length > 0;

                if (!hasCurrentData)
                {
                    return;
                }

                if (!savedAttempt.HasValue ||
                    savedAttempt.Value < 0 ||
                    _attemptBackupGenerations <= 0)
                {
                    Directory.Delete(_currentAttemptDir, true);
                    Directory.CreateDirectory(_currentAttemptDir);
                    PruneOneAttemptBackupIfNeeded();
                    return;
                }

                string backupDir = Path.Combine(_attemptsDir, savedAttempt.Value.ToString());

                if (Directory.Exists(backupDir))
                {
                    Directory.Delete(backupDir, true);
                }

                Directory.Move(_currentAttemptDir, backupDir);
                Directory.CreateDirectory(_currentAttemptDir);
                PruneOneAttemptBackupIfNeeded();
            }
            catch (Exception ex)
            {
                LogError("Backup current attempt", ex);

                try
                {
                    Directory.CreateDirectory(_currentAttemptDir);
                }
                catch (Exception createEx)
                {
                    LogError("Recreate current attempt directory", createEx);
                }
            }
        }

        private void PruneOneAttemptBackupIfNeeded()
        {
            try
            {
                if (string.IsNullOrEmpty(_attemptsDir) ||
                    !Directory.Exists(_attemptsDir))
                {
                    return;
                }

                var backups = new List<KeyValuePair<int, string>>();
                string[] directories = Directory.GetDirectories(_attemptsDir);

                for (int i = 0; i < directories.Length; i++)
                {
                    string name = Path.GetFileName(directories[i]);
                    int attempt;

                    if (int.TryParse(name, out attempt))
                    {
                        backups.Add(new KeyValuePair<int, string>(attempt, directories[i]));
                    }
                }

                if (backups.Count <= _attemptBackupGenerations)
                {
                    return;
                }

                backups.Sort((left, right) => right.Key.CompareTo(left.Key));
                Directory.Delete(backups[backups.Count - 1].Value, true);
            }
            catch (Exception ex)
            {
                LogError("Prune attempt backups", ex);
            }
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
            FlushCurrentInstance();
        }

        public static void FlushOnLevelEnd()
        {
            FlushCurrentInstance();
        }

        private static void FlushCurrentInstance()
        {
            if (_instance == null)
            {
                return;
            }

            _instance.FlushForStop();
        }

        private void FlushForStop()
        {
            if (!_runtimeInitialized || _hasFlushed)
            {
                JKMetricsDebugLog.Write(
                    "Attempt FlushForStop skipped: initialized=" + _runtimeInitialized +
                    ", flushed=" + _hasFlushed
                );
                return;
            }

            JKMetricsDebugLog.Write(
                "Attempt FlushForStop: frames=" + _totalFrames +
                ", screen=" + _lastScreen +
                ", area=" + _lastArea
            );
            LogAttemptOutput("flush", true, true, false);
            WriteOutputFiles();
            AppendScreenMetricTsv();

            _hasFlushed = true;
        }

        protected override void Update(float p_delta)
        {
            TrackCurrentFrame();
        }

        private void TrackCurrentFrame()
        {
            if (_locations == null || _locations.Length == 0)
            {
                _locations = LoadLocations();
            }

            int screen = JumpKing.Camera.CurrentScreen + 1;

            if (screen >= MinScreen && screen <= MaxScreen)
            {
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

                    if (!_areaFirstReachedMilliseconds.ContainsKey(areaName))
                    {
                        RecordAreaFirstReach(areaName);
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
                LogAttemptOutput("interval", true, true, false);
                WriteOutputFiles();
                AppendScreenMetricTsv();
            }

        }

        private void WriteOutputFiles()
        {
            WriteAreaProgressTsv();
            WriteStateTsv();
        }

        private void LogAttemptOutput(
            string reason,
            bool writesProgress,
            bool writesScreenMetric,
            bool writesScreenOrder
        )
        {
            JKMetricsDebugLog.Write(
                "Attempt output: reason=" + reason +
                ", frames=" + _totalFrames +
                ", screen=" + _lastScreen +
                ", area=" + _lastArea +
                ", progress=" + writesProgress +
                ", screenMetric=" + writesScreenMetric +
                ", screenOrder=" + writesScreenOrder
            );
        }
    }
}

