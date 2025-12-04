using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NetCoreAudio.Interfaces;
using NetCoreAudio.Utils;
using Plugin.Maui.Audio;
using TagLib;

namespace NetCoreAudio.Players
{
    internal class AndroidPlayer : IPlayer
    {
        private AudioFileInfo _audioFileInfo = new AudioFileInfo();
        private IAudioPlayer _audioPlayer;
        private IAudioManager _audioManager;
        public bool Playing => throw new NotImplementedException();

        public bool Paused => throw new NotImplementedException();

        public event EventHandler PlaybackFinished;

        public AndroidPlayer()
        {
            _audioManager = new AudioManager();
        }

        public void Dispose()
        {
            _audioPlayer?.Dispose();
        }

        public Task<AudioFileInfo> GetFileInfo()
        {
            return Task.FromResult(_audioFileInfo);
        }

        public Task<long> GetStatus()
        {            
            if (_audioPlayer != null)
            {
                return Task.FromResult((long)(_audioPlayer.CurrentPosition * 1000));
            }
            return Task.FromResult(0L);
        }

        public Task Pause()
        {
            if (_audioPlayer.IsPlaying)
            {
                _audioPlayer.Pause();
            }
            return Task.CompletedTask;
        }

        public Task Play(string fileName)
        {
            _audioPlayer = _audioManager.CreatePlayer(fileName);
            _audioPlayer.Play();
            _audioPlayer.PlaybackEnded += (s, e) =>
            {
                PlaybackFinished?.Invoke(this, EventArgs.Empty);
            };

            var result = new AudioFileInfo();
            if (_audioPlayer != null)
            {
                result.FileName = System.IO.Path.GetFileName(fileName);
                result.FilePath = fileName;
                result.FileExtension = System.IO.Path.GetExtension(fileName);
                var tfile = TagLib.File.Create(fileName);
                result.Length = (long)tfile.Properties.Duration.TotalMilliseconds;
                result.Artist = tfile.Tag.FirstPerformer;
                result.Title = tfile.Tag.Title;
                result.Album = tfile.Tag.Album;
                result.Genre = tfile.Tag.FirstGenre;
                result.Year = tfile.Tag.Year.ToString();
                result.Track = tfile.Tag.Track.ToString();
                result.Comment = tfile.Tag.Comment;
                result.FileSize = new System.IO.FileInfo(fileName).Length;
            }
            _audioFileInfo = result;

            return Task.CompletedTask;
        }

        public Task Resume()
        {
            if (!_audioPlayer.IsPlaying)
            {
                _audioPlayer.Play();
            }
            return Task.CompletedTask;
        }

        public Task Seek(long position)
        {
            if (_audioPlayer != null)
            {
                double positionInSeconds = position / 1000.0;
                _audioPlayer.Seek(positionInSeconds);
            }
            return Task.CompletedTask;
        }

        public Task SetVolume(byte percent)
        {
            if (percent > 100)
                throw new ArgumentOutOfRangeException(nameof(percent), "Percent can't exceed 100");

            float volume = percent / 100f;
            _audioPlayer.Volume = volume;
            return Task.CompletedTask;
        }

        public Task Stop()
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.Stop();
            }
            return Task.CompletedTask;
        }
    }
}