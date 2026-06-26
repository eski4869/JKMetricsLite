using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using JumpKing.MiscSystems.Achievements;

namespace JKMetricsLite
{
    internal static class TenTimesMetrics
    {
        private const int MaxRecords = 10;
        private const string Header = "attempt\tmap_name\tclear_time_ms";

        private sealed class Record
        {
            public int Attempt;
            public string MapName;
            public long ClearTimeMilliseconds;
        }

        internal static void TryRecordCompletion()
        {
            if (!JKMetricsLiteMod.IsTenTimesMetricsEnabled())
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

            string path = ScreenStayStatsBehaviour.GetPreparedTenTimesMetricsPath();
            List<Record> records = ReadRecords(path);

            if (ContainsAttempt(records, winStats.Value.attempts))
            {
                return;
            }

            records.Add(new Record
            {
                Attempt = winStats.Value.attempts,
                MapName = GetCurrentMapTitle(),
                ClearTimeMilliseconds =
                    (long)Math.Round(winStats.Value.timeSpan.TotalMilliseconds)
            });

            while (records.Count > MaxRecords)
            {
                records.RemoveAt(0);
            }

            WriteRecords(path, records);
        }

        internal static void Reset()
        {
            string path = ScreenStayStatsBehaviour.GetPreparedTenTimesMetricsPath();
            WriteRecords(path, new List<Record>());
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

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                string[] columns = lines[i].Split('\t');

                if (columns.Length < 3)
                {
                    continue;
                }

                int attempt;
                long clearTimeMilliseconds;

                if (!int.TryParse(columns[0], out attempt) ||
                    !long.TryParse(columns[2], out clearTimeMilliseconds))
                {
                    continue;
                }

                records.Add(new Record
                {
                    Attempt = attempt,
                    MapName = columns[1],
                    ClearTimeMilliseconds = clearTimeMilliseconds
                });
            }

            return records;
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
                        EscapeTsv(records[i].MapName) + "\t" +
                        records[i].ClearTimeMilliseconds
                    );
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ScreenStayStatsBehaviour.LogError("Write 10 Times metrics TSV", ex);
            }
        }

        private static string GetCurrentMapTitle()
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
                    return GetOfficialMapTitle();
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
                ScreenStayStatsBehaviour.LogError("Get current map title", ex);
                return "";
            }
        }

        private static string GetOfficialMapTitle()
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
