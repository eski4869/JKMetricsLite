using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using JumpKing.MiscSystems.Achievements;
using JumpKing.MiscSystems.LocationText;

namespace JKMetricsLite
{
    public partial class ScreenStayStatsBehaviour
    {
        private double GetSecondsPerFrame()
        {
            try
            {
                return JumpKing.Game1.instance.TargetElapsedTime.TotalSeconds;
            }
            catch (Exception ex)
            {
                LogError("Get seconds per frame", ex);
                return 0.017;
            }
        }

        private string FormatFramesAsTime(int frames)
        {
            double seconds = frames * GetSecondsPerFrame();
            TimeSpan time = TimeSpan.FromSeconds(seconds);

            int totalHours = (int)Math.Floor(time.TotalHours);

            return totalHours.ToString().PadLeft(2) + "h " +
                   time.Minutes.ToString().PadLeft(2) + "m " +
                   time.Seconds.ToString().PadLeft(2) + "s";
        }

        private string FormatFramesAsTimeWithMs(int frames)
        {
            double seconds = frames * GetSecondsPerFrame();
            TimeSpan time = TimeSpan.FromSeconds(seconds);

            int totalHours = (int)Math.Floor(time.TotalHours);

            return totalHours.ToString().PadLeft(2) + "h " +
                   time.Minutes.ToString().PadLeft(2) + "m " +
                   time.Seconds.ToString().PadLeft(2) + "s " +
                   time.Milliseconds.ToString("000") + "ms";
        }

        private string FormatFramesAsSpeedrunTime(int frames)
        {
            double seconds = frames * GetSecondsPerFrame();
            TimeSpan time = TimeSpan.FromSeconds(seconds);

            int totalMinutes = (int)Math.Floor(time.TotalMinutes);

            return totalMinutes.ToString().PadLeft(3) + "m " +
                   time.Seconds.ToString().PadLeft(2) + "s " +
                   time.Milliseconds.ToString("000") + "ms";
        }

        private long FramesToMilliseconds(int frames)
        {
            return (long)Math.Round(frames * GetSecondsPerFrame() * 1000);
        }

        private string FormatMillisecondsAsTime(long milliseconds)
        {
            TimeSpan time = TimeSpan.FromMilliseconds(milliseconds);
            int totalHours = (int)Math.Floor(time.TotalHours);

            return totalHours.ToString().PadLeft(2) + "h " +
                   time.Minutes.ToString().PadLeft(2) + "m " +
                   time.Seconds.ToString().PadLeft(2) + "s";
        }

        private string FormatMillisecondsAsSpeedrunTime(long milliseconds)
        {
            TimeSpan time = TimeSpan.FromMilliseconds(milliseconds);
            int totalMinutes = (int)Math.Floor(time.TotalMinutes);

            return totalMinutes.ToString().PadLeft(3) + "m " +
                   time.Seconds.ToString().PadLeft(2) + "s " +
                   time.Milliseconds.ToString("000") + "ms";
        }

        private string EscapeTsv(string value)
        {
            if (value == null)
            {
                return "";
            }

            return value
                .Replace("\t", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private string EncodeText(string value)
        {
            if (value == null)
            {
                value = "";
            }

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private string DecodeText(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch (Exception ex)
            {
                LogError("Decode text", ex);
                return "";
            }
        }
    }
}
