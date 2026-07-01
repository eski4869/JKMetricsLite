using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using EntityComponent;
using JumpKing.API;
using JumpKing.GameManager.MultiEnding;
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

            if (AreAnyMetricsEnabled())
            {
                ScreenStayStatsBehaviour.PrepareForLevelLoad();

                if (IsClearTimeMetricsEnabled())
                {
                    ClearTimeMetrics.PrepareForLevelLoad();
                }
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
            RegisterMetricsBehaviours();
        }

        private static void RegisterMetricsBehaviours()
        {
            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();

            if (player == null)
            {
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
                return;
            }

            _registeredAttemptBehaviour = new ScreenStayStatsBehaviour();
            player.AddComponents(
                _preferences.AttemptMetricsEnabled,
                new Component[] { _registeredAttemptBehaviour }
            );
        }

        private static void RegisterTotalMetricsBehaviour(PlayerEntity player)
        {
            TotalMetricsBehaviour existingBehaviour = player.GetComponent<TotalMetricsBehaviour>();

            if (existingBehaviour != null)
            {
                _registeredTotalBehaviour = existingBehaviour;
                _registeredTotalBehaviour.Enabled = _preferences.TotalMetricsEnabled;
                return;
            }

            _registeredTotalBehaviour = new TotalMetricsBehaviour();
            player.AddComponents(
                _preferences.TotalMetricsEnabled,
                new Component[] { _registeredTotalBehaviour }
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
            ClearTimeMetrics.TryRecordCompletion();
            SaveSettingsIfDirty();
            ScreenStayStatsBehaviour.FlushOnLevelEnd();
            TotalMetricsBehaviour.FlushOnLevelEnd();
            _registeredAttemptBehaviour = null;
            _registeredTotalBehaviour = null;
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            SaveSettingsIfDirty();
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static AttemptMetricsToggle AttemptMetricsMenu(object factory, GuiFormat format)
        {
            return new AttemptMetricsToggle();
        }

        [PauseMenuItemSetting]
        public static CurrentAreaMetricsToggle CurrentAreaMetricsMenu(object factory, GuiFormat format)
        {
            return new CurrentAreaMetricsToggle();
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static TotalMetricsToggle TotalMetricsMenu(object factory, GuiFormat format)
        {
            return new TotalMetricsToggle();
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static ClearTimeMetricsToggle ClearTimeMetricsMenu(object factory, GuiFormat format)
        {
            return new ClearTimeMetricsToggle();
        }

        [PauseMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton ResetClearTimeMetricsMenu(
            object factory,
            GuiFormat format
        )
        {
            return new JumpKing.PauseMenu.BT.TextButton(
                "  - Reset",
                new ResetClearTimeMetricsNode()
            );
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

        public static bool IsClearTimeMetricsEnabled()
        {
            EnsurePreferencesLoaded();
            return _preferences.ClearTimeMetricsEnabled;
        }

        internal static bool AreAnyMetricsEnabled()
        {
            EnsurePreferencesLoaded();
            return _preferences.AttemptMetricsEnabled ||
                _preferences.TotalMetricsEnabled ||
                _preferences.ClearTimeMetricsEnabled;
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
            ApplyTotalMetricsEnabledToRegisteredBehaviour();
        }

        public static void SetClearTimeMetricsEnabled(bool isEnabled)
        {
            EnsurePreferencesLoaded();

            if (_preferences.ClearTimeMetricsEnabled == isEnabled)
            {
                return;
            }

            _preferences.ClearTimeMetricsEnabled = isEnabled;
            _settingsDirty = true;
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
            if (string.IsNullOrWhiteSpace(_preferences.OutputDir))
            {
                _preferences.OutputDir = ScreenStayStatsBehaviour.DefaultOutputFolderName;
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

    public class ClearTimeMetricsToggle : ITextToggle
    {
        public ClearTimeMetricsToggle() : base(JKMetricsLiteMod.IsClearTimeMetricsEnabled())
        {
        }

        protected override string GetName()
        {
            return "Clear Time Metrics";
        }

        protected override void OnToggle()
        {
            JKMetricsLiteMod.SetClearTimeMetricsEnabled(toggle);
        }
    }

    public class ResetClearTimeMetricsNode : BehaviorTree.IBTnode
    {
        protected override BehaviorTree.BTresult MyRun(BehaviorTree.TickData p_data)
        {
            ClearTimeMetrics.Reset();
            return BehaviorTree.BTresult.Success;
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
            return "  - Exclude This Area";
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
                return AttemptMetricsEnabled &&
                    TotalMetricsEnabled &&
                    ClearTimeMetricsEnabled;
            }
            set
            {
                AttemptMetricsEnabled = value;
                TotalMetricsEnabled = value;
                ClearTimeMetricsEnabled = value;
            }
        }

        public bool ShouldSerializeIsEnabled()
        {
            return false;
        }

        public bool AttemptMetricsEnabled { get; set; } = true;
        public bool TotalMetricsEnabled { get; set; } = true;
        public bool ClearTimeMetricsEnabled { get; set; } = true;
        public string OutputDir { get; set; } = ScreenStayStatsBehaviour.DefaultOutputFolderName;
        public int AttemptBackupGenerations { get; set; } = 1;
    }

    public partial class ScreenStayStatsBehaviour : Component
    {
        private const int MinScreen = 1;
        private const int MaxScreen = 169;
        private const int OutputIntervalFrames = 60;
        private const int PerformanceLogIntervalFrames = 600;
        private const string BabeScreenAreaName = "Babe Screen";
        private const string CompletionAreaName = "Clear Time";
        internal const string DefaultOutputFolderName = "JKMetricsLite";

        private static ScreenStayStatsBehaviour _instance;
        private static bool _processExitRegistered = false;
        private bool _runtimeInitialized = false;
        private bool _hasFlushed = false;

        private readonly Dictionary<string, int> _areaFrames = new Dictionary<string, int>();
        private readonly Dictionary<string, long> _areaFirstReachedMilliseconds =
            new Dictionary<string, long>();
        private readonly Dictionary<string, long> _areaFirstLandedMilliseconds =
            new Dictionary<string, long>();
        private readonly List<string> _areaAppearedOrder = new List<string>();
        private readonly HashSet<string> _excludedAreas = new HashSet<string>();
        private readonly HashSet<int> _babeScreens = new HashSet<int>();
        private long? _babeClearTimeMilliseconds = null;
        private readonly IEnding[] _officialEndings = new IEnding[]
        {
            new JumpKing.GameManager.MultiEnding.NormalEnding.NormalEnding(),
            new JumpKing.GameManager.MultiEnding.NewBabePlusEnding.NewBabePlusEnding(),
            new JumpKing.GameManager.MultiEnding.OwlEnding.OwlEnding()
        };
        private PlayerEntity _player;

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
        private BodyComp _playerBody;

        private int _totalFrames = 0;
        private int _outputCounter = 0;
        private int _performanceLogCounter = 0;
        private readonly Dictionary<string, PerformanceTiming> _performanceTimings =
            new Dictionary<string, PerformanceTiming>();
        private int _lastScreen = -1;
        private string _lastArea = "Unknown";
        private int _screenOrderRevision = 0;
        private bool _screenMetricsHeaderChecked = false;

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
            public string ClearTimeMetricsPath;
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
                ClearTimeMetricsPath = Path.Combine(rawDataDir, "clear_times.tsv"),
                AttemptBackupGenerations = JKMetricsLiteMod.GetAttemptBackupGenerations()
            };

            WriteOverlayHtmlIfMissing(obsDir, "area_name_splits.html", LoadOverlayTemplate(AreaNameSplitTemplateName));
            WriteOverlayHtmlIfMissing(obsDir, "area_number_splits.html", LoadOverlayTemplate(AreaNumberSplitTemplateName));
            WriteOverlayHtmlIfMissing(obsDir, "area_name_splits_speedrun.html", LoadOverlayTemplate(AreaNameSplitSpeedrunTemplateName));
            WriteOverlayHtmlIfMissing(obsDir, "area_number_splits_speedrun.html", LoadOverlayTemplate(AreaNumberSplitSpeedrunTemplateName));
            WriteOverlayHtmlIfMissing(obsDir, "progress_graph.html", LoadOverlayTemplate(ScreenGraphTemplateName));
            WriteOverlayHtmlIfMissing(obsDir, "clear_time_metrics.html", LoadOverlayTemplate(ClearTimeMetricsTemplateName));
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

        internal static string GetPreparedClearTimeMetricsPath()
        {
            return GetLevelLoadPreparation().ClearTimeMetricsPath;
        }

        public ScreenStayStatsBehaviour()
        {
        }

        protected override void OnEnable()
        {
            _instance = this;
            _hasFlushed = false;

            if (!_runtimeInitialized)
            {
                InitializeForLevelStart();
            }
        }

        protected override void OnDisable()
        {
            FlushForStop();
        }

        protected override void OnOwnerDestroy()
        {
            FlushForStop();

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }

            _playerBody = null;
            _player = null;
            JKMetricsLiteMod.ClearRegisteredAttemptMetricsBehaviour(this);
        }

        private void InitializeForLevelStart()
        {
            _runtimeInitialized = true;
            RegisterProcessExitHandler();

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
            _screenMetricsHeaderChecked = false;
            _hasFlushed = false;
            _player = EntityManager.instance.Find<PlayerEntity>();
            _playerBody = _player != null ? _player.m_body : TryGetPlayerBody();

            _locations = LoadLocations();
            LoadBabeScreens();

            int? currentAttempt = TryGetCurrentAttempt();
            bool loaded = LoadRunDataIfSameAttempt(currentAttempt);

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
            WriteOutputFiles();
        }

        private static string ResolveOutputDir(string assemblyDir, string configuredOutputDir)
        {
            string defaultOutputDir = Path.Combine(assemblyDir, DefaultOutputFolderName);

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
                return;
            }

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
            var frameStopwatch = System.Diagnostics.Stopwatch.StartNew();

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

                    if (!_areaFirstLandedMilliseconds.ContainsKey(areaName) &&
                        IsPlayerOnGround())
                    {
                        RecordAreaFirstLanding(areaName);
                    }

                    if (!_areaAppearedOrder.Contains(areaName))
                    {
                        _areaAppearedOrder.Add(areaName);
                    }

                    if (RegisterAreaScreenIfNeeded(areaName, screen))
                    {
                        UpdatePbIfNeeded(screen, areaName);
                    }

                    _areaFrames[areaName]++;
                }

                RecordClearTimeIfNeeded();
            }

            frameStopwatch.Stop();
            AddPerformanceTiming("frame_tracking", frameStopwatch.Elapsed.TotalMilliseconds);

            _totalFrames++;
            _outputCounter++;

            if (_outputCounter >= OutputIntervalFrames)
            {
                _outputCounter = 0;
                WriteOutputFiles();
                AppendScreenMetricTsv();
            }

            MaybeWritePerformanceLog();
        }

        private void WriteOutputFiles()
        {
            WriteAreaProgressTsv();
            WriteStateTsv();
        }
    }
}






