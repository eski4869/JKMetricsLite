using System;
using System.IO;
using System.Reflection;
using System.Text;
using EntityComponent;
using JumpKing.MiscSystems.Achievements;

namespace JKMetricsLite
{
    internal static class PlayerStatsReader
    {
        internal static PlayerStats? TryGetPlayerStats(string propertyName)
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
                ScreenStayStatsBehaviour.LogError("Get player stats", ex);
            }

            return null;
        }

        internal static TimeSpan? TryGetCurrentRunTime()
        {
            PlayerStats? stats = TryGetCurrentStats();

            if (!stats.HasValue)
            {
                return null;
            }

            TimeSpan time = stats.Value.timeSpan;

            return time.TotalMilliseconds >= 0 ? (TimeSpan?)time : null;
        }

        internal static PlayerStats? TryGetCurrentStats()
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

                object manager = GetAchievementManagerInstance(managerType);

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
                    return (PlayerStats)statsObject;
                }
            }
            catch (Exception ex)
            {
                ScreenStayStatsBehaviour.LogError("Get current stats", ex);
            }

            return null;
        }

        internal static PlayerStats? TryGetAllTimeStats()
        {
            try
            {
                Type managerType = typeof(PlayerStats).Assembly.GetType(
                    "JumpKing.MiscSystems.Achievements.AchievementManager"
                );

                if (managerType == null)
                {
                    return TryGetPlayerStats("PermanentPlayerStats");
                }

                object manager = GetAchievementManagerInstance(managerType);

                if (manager == null)
                {
                    return TryGetPlayerStats("PermanentPlayerStats");
                }

                MethodInfo getAllTimeStatsMethod = managerType.GetMethod(
                    "GetAllTimeStats",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (getAllTimeStatsMethod == null)
                {
                    return TryGetPlayerStats("PermanentPlayerStats");
                }

                object statsObject = getAllTimeStatsMethod.Invoke(manager, null);

                if (statsObject is PlayerStats)
                {
                    return (PlayerStats)statsObject;
                }
            }
            catch (Exception ex)
            {
                ScreenStayStatsBehaviour.LogError("Get all-time achievement stats", ex);
            }

            return TryGetPlayerStats("PermanentPlayerStats");
        }

        private static object GetAchievementManagerInstance(Type managerType)
        {
            FieldInfo instanceField = managerType.GetField(
                "instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );

            return instanceField == null ? null : instanceField.GetValue(null);
        }
    }

    public class TotalMetricsBehaviour : Component
    {
        private const int OutputIntervalFrames = 3600;

        private static TotalMetricsBehaviour _instance;
        private static bool _processExitRegistered = false;

        private bool _runtimeInitialized = false;
        private bool _hasFlushed = false;
        private int _outputCounter = 0;
        private string _totalMetricsPath;

        private struct TotalStats
        {
            public long TotalFrames;
            public long TotalJumps;
            public long TotalFalls;
        }

        protected override void OnEnable()
        {
            _instance = this;
            _hasFlushed = false;

            if (!_runtimeInitialized)
            {
                InitializeForLevelStart();
                return;
            }

            _outputCounter = 0;
            AppendTotalMetricsTsv();
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

            JKMetricsLiteMod.ClearRegisteredTotalMetricsBehaviour(this);
        }

        protected override void Update(float p_delta)
        {
            if (!_runtimeInitialized)
            {
                return;
            }

            _outputCounter++;

            if (_outputCounter < OutputIntervalFrames)
            {
                return;
            }

            _outputCounter = 0;
            AppendTotalMetricsTsv();
        }

        private void InitializeForLevelStart()
        {
            _runtimeInitialized = true;
            _hasFlushed = false;
            _outputCounter = 0;
            _totalMetricsPath = ScreenStayStatsBehaviour.GetPreparedTotalMetricsPath();
            RegisterProcessExitHandler();
            AppendTotalMetricsTsv();
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

            AppendTotalMetricsTsv();
            _hasFlushed = true;
        }

        private void AppendTotalMetricsTsv()
        {
            try
            {
                TotalStats totals;

                if (!TryGetTotalStats(out totals))
                {
                    return;
                }

                bool needsHeader =
                    !File.Exists(_totalMetricsPath) ||
                    new FileInfo(_totalMetricsPath).Length == 0;

                var sb = new StringBuilder();

                if (needsHeader)
                {
                    sb.AppendLine("sampled_at\ttotal_frames\ttotal_jumps\ttotal_falls");
                }

                sb.AppendLine(
                    DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz") + "\t" +
                    totals.TotalFrames + "\t" +
                    totals.TotalJumps + "\t" +
                    totals.TotalFalls
                );

                File.AppendAllText(_totalMetricsPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ScreenStayStatsBehaviour.LogError("Append total metrics TSV", ex);
            }
        }

        private bool TryGetTotalStats(out TotalStats totals)
        {
            totals = default(TotalStats);

            PlayerStats? stats = PlayerStatsReader.TryGetAllTimeStats();

            if (!stats.HasValue)
            {
                return false;
            }

            double secondsPerFrame = GetSecondsPerFrame();

            if (secondsPerFrame <= 0)
            {
                return false;
            }

            PlayerStats value = stats.Value;

            totals.TotalFrames = (long)Math.Round(value.timeSpan.TotalSeconds / secondsPerFrame);
            totals.TotalJumps = value.jumps;
            totals.TotalFalls = value.falls;

            return true;
        }

        private double GetSecondsPerFrame()
        {
            try
            {
                return JumpKing.Game1.instance.TargetElapsedTime.TotalSeconds;
            }
            catch (Exception ex)
            {
                ScreenStayStatsBehaviour.LogError("Get seconds per frame", ex);
                return 0.017;
            }
        }
    }
}
