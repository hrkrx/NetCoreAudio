using System;
using System.IO;
using System.Threading.Tasks;
using NetCoreAudio.Interfaces;

namespace NetCoreAudio.Players
{
    internal class LinuxPlayer : UnixPlayerBase, IPlayer
    {
        protected override string GetBashCommand(string fileName)
        {
            // Use ffplay for all audio files as it supports seeking
            return "ffplay -nodisp -autoexit -loglevel quiet";
        }
        
        protected override string GetBashCommandWithSeek(string fileName, long positionMs)
        {
            if (positionMs > 0)
            {
                // Convert milliseconds to seconds for ffplay's -ss parameter
                double positionSeconds = positionMs / 1000.0;
                return $"ffplay -nodisp -autoexit -ss {positionSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} -loglevel quiet";
            }
            return GetBashCommand(fileName);
        }

        public override Task SetVolume(byte percent)
        {
            if (percent > 100)
                throw new ArgumentOutOfRangeException(nameof(percent), "Percent can't exceed 100");

            var tempProcess = StartBashProcess($"amixer -M set 'Master' {percent}%");
            tempProcess.WaitForExit();

            return Task.CompletedTask;
        }
    }
}
