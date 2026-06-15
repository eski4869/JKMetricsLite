using System;
using System.IO;
using System.Reflection;
using System.Text;
using JumpKing.MiscSystems.Achievements;

namespace JKMetricsLite
{
    public partial class ScreenStayStatsBehaviour
    {
        private struct TotalStats
        {
            public long TotalFrames;
            public long TotalJumps;
            public long TotalFalls;
        }

        private void AppendTotalStatsTsv()
        {
            try
            {
                TotalStats totals;

                if (!TryGetTotalStats(out totals))
                {
                    return;
                }

                bool needsHeader =
                    !File.Exists(_totalStatsPath) ||
                    new FileInfo(_totalStatsPath).Length == 0;

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

                File.AppendAllText(_totalStatsPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogError("Append total stats TSV", ex);
            }
        }

        private bool TryGetTotalStats(out TotalStats totals)
        {
            totals = default(TotalStats);

            PlayerStats? stats = TryGetAllTimeAchievementStats();

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

        private PlayerStats? TryGetAllTimeAchievementStats()
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

                object manager = null;

                FieldInfo instanceField = managerType.GetField(
                    "instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (instanceField != null)
                {
                    manager = instanceField.GetValue(null);
                }

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
                LogError("Get all-time achievement stats", ex);
            }

            return TryGetPlayerStats("PermanentPlayerStats");
        }
    }
}
