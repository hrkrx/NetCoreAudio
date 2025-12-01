using NetCoreAudio.Interfaces;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NetCoreAudio.Utils;
using System.IO;

namespace NetCoreAudio.Players
{
    internal abstract class UnixPlayerBase : IPlayer
    {
        private Process _process = null;
        private Stopwatch _playStopwatch;
        private long _seekPosition = 0;
        private long _appliedSeekPosition = 0;
        private long _pausedPosition = 0;

        internal const string PauseProcessCommand = "kill -STOP {0}";
        internal const string ResumeProcessCommand = "kill -CONT {0}";

        public event EventHandler PlaybackFinished;
        
        private AudioFileInfo _audioFileInfo = new AudioFileInfo();

        public bool Playing { get; private set; }

        public bool Paused { get; private set; }

        protected abstract string GetBashCommand(string fileName);

        protected abstract string GetBashCommandWithSeek(string fileName, long positionMs);

        public async Task Play(string fileName)
        {
            if (Playing)
            {
                await Stop();
            }
            
            // Get file duration using ffprobe
            var ffprobeProcess = StartBashProcess($"ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 '{fileName}'");
            ffprobeProcess.WaitForExit();
            var durationOutput = ffprobeProcess.StandardOutput.ReadToEnd().Trim();
            if (double.TryParse(durationOutput, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var durationSeconds))
            {
                _audioFileInfo.Length = (long)(durationSeconds * 1000);
            }
            
            var BashToolName = GetBashCommandWithSeek(fileName, _seekPosition);
            _process = StartBashProcess($"{BashToolName} '{fileName}'");
            _process.EnableRaisingEvents = true;
            _process.Exited += HandlePlaybackFinished;
            _process.ErrorDataReceived += HandlePlaybackFinished;
            _process.Disposed += HandlePlaybackFinished;
            Playing = true;
            
            _playStopwatch = new Stopwatch();
            _playStopwatch.Start();
            
            // Store the seek position that was applied, then reset for next play
            _appliedSeekPosition = _seekPosition;
            _seekPosition = 0;

            _audioFileInfo.FilePath = fileName;
            _audioFileInfo.FileName = System.IO.Path.GetFileName(fileName);
            _audioFileInfo.FileExtension = System.IO.Path.GetExtension(fileName);
            _audioFileInfo.FileSize = new System.IO.FileInfo(fileName).Length;
        }

        public Task Pause()
        {
            if (Playing && !Paused && _process != null)
            {
                var tempProcess = StartBashProcess(string.Format(PauseProcessCommand, _process.Id));
                tempProcess.WaitForExit();
                Paused = true;
                
                if (_playStopwatch != null)
                {
                    _playStopwatch.Stop();
                    _pausedPosition = _appliedSeekPosition + _playStopwatch.ElapsedMilliseconds;
                }
            }

            return Task.CompletedTask;
        }

        public Task Resume()
        {
            if (Playing && Paused && _process != null)
            {
                var tempProcess = StartBashProcess(string.Format(ResumeProcessCommand, _process.Id));
                tempProcess.WaitForExit();
                Paused = false;
                
                if (_playStopwatch != null)
                {
                    _playStopwatch.Start();
                }
            }

            return Task.CompletedTask;
        }

        public Task Stop()
        {
            return Stop(true);
        }
        
        private Task Stop(bool resetPosition)
        {
            if (_process != null)
            {
                _process.Kill();
                _process.Dispose();
                _process = null;
            }

            Playing = false;
            Paused = false;
            
            if (resetPosition)
            {
                _seekPosition = 0;
                _appliedSeekPosition = 0;
                _pausedPosition = 0;
            }
            
            if (_playStopwatch != null)
            {
                _playStopwatch.Stop();
                _playStopwatch = null;
            }

            return Task.CompletedTask;
        }

        protected Process StartBashProcess(string command)
        {
            var escapedArgs = command.Replace("\"", "\\\"");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            return process;
        }

        public Task<AudioFileInfo> GetFileInfo()
        {
            return Task.FromResult(_audioFileInfo);
        }

        public Task<long> GetStatus()
        {
            if (!Playing || _playStopwatch == null)
            {
                return Task.FromResult(0L);
            }
            
            if (Paused)
            {
                return Task.FromResult(_pausedPosition);
            }
            
            return Task.FromResult(_appliedSeekPosition + _playStopwatch.ElapsedMilliseconds);
        }

        public async Task Seek(long position)
        {
            if (!Playing)
            {
                return;
            }
            
            var currentFile = _audioFileInfo.FilePath;
            await Stop(false);
            _seekPosition = position; 
            await Play(currentFile);
        }

        internal void HandlePlaybackFinished(object sender, EventArgs e)
        {
            if (Playing)
            {
                Playing = false;
                PlaybackFinished?.Invoke(this, e);
            }
        }

        public abstract Task SetVolume(byte percent);

        public void Dispose()
        {
            Stop().Wait();
        }
    }
}
