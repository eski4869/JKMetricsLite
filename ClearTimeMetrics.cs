using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using JumpKing.MiscSystems.Achievements;

namespace JKMetricsLite
{
    internal static class ClearTimeMetrics
    {
        private const int MaxRecords = 100;
        private const string Header = "attempt\tlevel_name\tclear_time_ms\tsummary_target";

        private static string _loadedPath;
        private static List<Record> _records = new List<Record>();

        private sealed class Record
        {
            public int Attempt;
            public string LevelName;
            public long ClearTimeMilliseconds;
            public bool SummaryTarget;
        }

        internal static void PrepareForLevelLoad()
        {
            string path = ScreenStayStatsBehaviour.GetPreparedClearTimeMetricsPath();
            EnsureLoaded(path);
        }

        internal static void TryRecordCompletion()
        {
            if (!JKMetricsLiteMod.IsClearTimeMetricsEnabled())
            {
                return;
            }

            PlayerStats? currentStats = PlayerStatsReader.TryGetCurrentStats();
            PlayerStats? winStats = PlayerStatsReader.TryGetWinStats();

            if (!currentStats.HasValue ||
                !winStats.HasValue ||
                !PlayerStatsReader.IsCurrentStatsAfterWin(currentStats.Value, winStats.Value))
            {
                return;
            }

            string path = ScreenStayStatsBehaviour.GetPreparedClearTimeMetricsPath();
            EnsureLoaded(path);

            if (ContainsAttempt(_records, winStats.Value.attempts))
            {
                return;
            }

            _records.Add(new Record
            {
                Attempt = winStats.Value.attempts,
                LevelName = GetCurrentLevelName(),
                ClearTimeMilliseconds =
                    (long)Math.Round(winStats.Value.timeSpan.TotalMilliseconds),
                SummaryTarget = true
            });

            while (_records.Count > MaxRecords)
            {
                _records.RemoveAt(0);
            }

            WriteRecords(path, _records);
        }

        internal static void Reset()
        {
            string path = ScreenStayStatsBehaviour.GetPreparedClearTimeMetricsPath();
            EnsureLoaded(path);

            for (int i = 0; i < _records.Count; i++)
            {
                _records[i].SummaryTarget = false;
            }

            WriteRecords(path, _records);
        }

        private static void EnsureLoaded(string path)
        {
            if (_loadedPath == path)
            {
                return;
            }

            _records = ReadRecords(path);
            _loadedPath = path;
        }

        private static bool ContainsAttempt(List<Record> records, int attempt)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Attempt == attempt)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<Record> ReadRecords(string path)
        {
            var records = new List<Record>();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return records;
            }

            string[] lines = File.ReadAllLines(path, Encoding.UTF8);

            if (lines.Length == 0)
            {
                return records;
            }

            string[] headers = lines[0].Split('\t');
            int attemptIndex = FindHeader(headers, "attempt");
            int levelNameIndex = FindHeader(headers, "level_name");
            int clearTimeIndex = FindHeader(headers, "clear_time_ms");
            int summaryTargetIndex = FindHeader(headers, "summary_target");


            if (attemptIndex < 0 || levelNameIndex < 0 || clearTimeIndex < 0)
            {
                return records;
            }

            int requiredIndex = Math.Max(attemptIndex, Math.Max(levelNameIndex, clearTimeIndex));

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                string[] columns = lines[i].Split('\t');

                if (columns.Length <= requiredIndex)
                {
                    continue;
                }

                int attempt;
                long clearTimeMilliseconds;

                if (!int.TryParse(columns[attemptIndex], out attempt) ||
                    !long.TryParse(columns[clearTimeIndex], out clearTimeMilliseconds))
                {
                    continue;
                }

                records.Add(new Record
                {
                    Attempt = attempt,
                    LevelName = columns[levelNameIndex],
                    ClearTimeMilliseconds = clearTimeMilliseconds,
                    SummaryTarget = summaryTargetIndex < 0 ||
                        (summaryTargetIndex < columns.Length && columns[summaryTargetIndex] == "1")
                });
            }

            return records;
        }

        private static int FindHeader(string[] headers, string name)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i] == name)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void WriteRecords(string path, List<Record> records)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var sb = new StringBuilder();
                sb.AppendLine(Header);

                for (int i = 0; i < records.Count; i++)
                {
                    sb.AppendLine(
                        records[i].Attempt + "\t" +
                        EscapeTsv(records[i].LevelName) + "\t" +
                        records[i].ClearTimeMilliseconds + "\t" +
                        (records[i].SummaryTarget ? "1" : "0")
                    );
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ScreenStayStatsBehaviour.LogError("Write Clear Time Metrics TSV", ex);
            }
        }

        private static string GetCurrentLevelName()
        {
            try
            {
                object contentManager = JumpKing.Game1.instance.contentManager;
                FieldInfo rootField = contentManager.GetType().GetField(
                    "root",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
                string root = rootField.GetValue(contentManager) as string;

                if (root == "Content")
                {
                    return GetOfficialLevelName();
                }

                FieldInfo levelField = contentManager.GetType().GetField(
                    "level",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
                object level = levelField.GetValue(contentManager);

                PropertyInfo nameProperty = level.GetType().GetProperty(
                    "Name",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
                object name = nameProperty.GetValue(level, null);

                return name == null ? "" : name.ToString();
            }
            catch (Exception ex)
            {
                ScreenStayStatsBehaviour.LogError("Get current level name", ex);
                return "";
            }
        }

        private static string GetOfficialLevelName()
        {
            JumpKing.GameManager.MultiEnding.EndingType ending =
                JumpKing.GameManager.MultiEnding.GameEnding.GetEnding();

            switch (ending)
            {
                case JumpKing.GameManager.MultiEnding.EndingType.NewBabePlus:
                    return "New Babe+";
                case JumpKing.GameManager.MultiEnding.EndingType.Ghost:
                    return "Ghost of the Babe";
                default:
                    return "Main Babe";
            }
        }

        private static string EscapeTsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            return value
                .Replace('\t', ' ')
                .Replace('\r', ' ')
                .Replace('\n', ' ');
        }
    }
}