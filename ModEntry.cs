using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BehaviorTree;
using EntityComponent;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.MiscSystems.Achievements;
using JumpKing.MiscSystems.LocationText;
using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.Player;

namespace JKMetricsLite
{
    [JumpKingMod("eski4869.JKMetricsLite")]
    public static class JKMetricsLiteMod
    {
        private static ScreenStayStatsBehaviour _registeredBehaviour;

        [OnLevelStart]
        public static void OnLevelStart()
        {
            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();

            if (player != null)
            {
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
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            ScreenStayStatsBehaviour.FlushOnLevelEnd();
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            ScreenStayStatsBehaviour.FlushOnLevelUnload();
        }

        [PauseMenuItemSetting]
        public static TextButton ResetMetricsMenu(object factory, GuiFormat format)
        {
            return new TextButton("Reset Metrics", new ResetMetricsNode());
        }
    }

    public class ResetMetricsNode : IBTnode
    {
        protected override BTresult MyRun(TickData p_data)
        {
            ScreenStayStatsBehaviour.ResetFromMenu();
            return BTresult.Success;
        }
    }

    public partial class ScreenStayStatsBehaviour : IBodyCompBehaviour
    {
        private const int MinScreen = 1;
        private const int MaxScreen = 169;
        private const int ScreenCount = 170;

        private const int OutputIntervalFrames = 60;
        private const int MaxBarWidth = 30;
        private const string OutputFolderName = "JKMetricsLite";
        private const string ConfigFileName = "JKMetricsLite.env";
        private const string OutputDirKey = "OUTPUT_DIR";

        private static ScreenStayStatsBehaviour _instance;
        private static bool _processExitRegistered = false;

        private readonly int[] _screenFrames = new int[ScreenCount];

        private readonly Dictionary<string, int> _areaFrames = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _areaFirstReachedFrames = new Dictionary<string, int>();
        private readonly List<string> _areaAppearedOrder = new List<string>();

        // Area-internal screen order is also based on first-reached order.
        private readonly Dictionary<string, List<int>> _areaScreenAppearedOrder =
            new Dictionary<string, List<int>>();

        private readonly string _outputDir;
        private readonly string _statePath;
        private readonly string _areaBarGraphPath;
        private readonly string _screenBarGraphPath;
        private readonly string _screenTimelinePath;
        private readonly string _progressStatusPath;

        private Location[] _locations = new Location[0];

        private int _totalFrames = 0;
        private int _outputCounter = 0;
        private int _lastScreen = -1;
        private int _lastTimelineAppendFrames = -1;
        private string _lastArea = "Unknown";

        // PB is based on first-reached area order + first-reached screen order inside that area.
        private string _pbArea = "";
        private int _pbAreaIndex = -1;
        private int _pbScreenInArea = -1;
        private int _pbScreen = -1;

        private int? _stateAttempt = null;

        public ScreenStayStatsBehaviour()
        {
            _instance = this;
            RegisterProcessExitHandler();

            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            WriteDefaultConfigFileIfMissing(assemblyDir);

            _outputDir = ResolveOutputDir(assemblyDir);
            Directory.CreateDirectory(_outputDir);
            SetLogOutputDir(_outputDir);

            _statePath = Path.Combine(_outputDir, "metrics_state.tsv");
            _areaBarGraphPath = Path.Combine(_outputDir, "area_bar_graph.tsv");
            _screenBarGraphPath = Path.Combine(_outputDir, "screen_bar_graph.tsv");
            _screenTimelinePath = Path.Combine(_outputDir, "screen_timeline.tsv");
            _progressStatusPath = Path.Combine(_outputDir, "progress_status.tsv");

            WriteAreaNameOverlayHtml();
            WriteAreaNoOverlayHtml();
            WriteAreaNameSpeedrunOverlayHtml();
            WriteScreenTimelineOverlayHtml();

            _locations = LoadLocations();

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
            FlushCurrentInstance(true);
        }

        public static void FlushOnLevelEnd()
        {
            FlushCurrentInstance(true);
        }

        public static void FlushOnLevelUnload()
        {
            FlushCurrentInstance(false);
        }

        private static void FlushCurrentInstance(bool appendTimeline)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.WriteOutputFiles(appendTimeline);
        }

        public static void ResetFromMenu()
        {
            if (_instance == null)
            {
                return;
            }

            _instance.ResetStats();
            _instance._stateAttempt = _instance.TryGetCurrentAttempt();
            _instance.ResetTimelineFile();
            _instance.SaveState();
            _instance.WriteOutputFiles(false);
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

            SaveState();
          }
    }
}

