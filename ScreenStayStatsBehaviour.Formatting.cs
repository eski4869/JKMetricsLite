using System;

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

        private long FramesToMilliseconds(int frames)
        {
            return (long)Math.Round(frames * GetSecondsPerFrame() * 1000);
        }

        private int MillisecondsToFrames(long milliseconds)
        {
            double secondsPerFrame = GetSecondsPerFrame();

            if (secondsPerFrame <= 0)
            {
                return 0;
            }

            return (int)Math.Round(
                Math.Max(0, milliseconds) / 1000.0 / secondsPerFrame
            );
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

    }
}
