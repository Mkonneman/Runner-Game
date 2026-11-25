using System;
using Microsoft.Xna.Framework.Media;

namespace Runner_Game
{
    /// <summary>
    /// All the music goes through here so every switch fades: the old song fades out, the new one fades in at its own speed
    /// (the calm menu groove takes its time, run songs come in quicker). Calling Play with the song that's already on just keeps it going
    /// </summary>
    public static class Music
    {
        private const float FadeOut = 0.7f;
        private static Song _next;
        private static TimeSpan? _nextAt;
        private static float _level, _fadeIn = 1f, _nextFadeIn;
        private static bool _stopping;

        // Gameplay sets this each frame (Time Slow makes it quiet), Play puts it back to 1
        public static float Duck = 1f;

        public static void Play(Song song, float fadeIn = 1f, TimeSpan? at = null)
        {
            Duck = 1f;
            if (song == null) return;
            bool on = MediaPlayer.State == MediaState.Playing && MediaPlayer.Queue.ActiveSong == song;
            if (on && _next == null) { _stopping = false; _fadeIn = fadeIn; return; }  // already playing, keep it (and cancel a stop)
            if (_next == song) return;  // already waiting to come in
            if (MediaPlayer.State == MediaState.Playing && _level > 0.01f) { _next = song; _nextFadeIn = fadeIn; _nextAt = at; _stopping = false; return; }  // fade the old one out first
            Start(song, fadeIn, at);
        }

        // Fades out, then stops
        public static void Stop()
        {
            _next = null;
            if (MediaPlayer.State == MediaState.Playing) _stopping = true;
        }

        private static void Start(Song song, float fadeIn, TimeSpan? at)
        {
            _next = null; _stopping = false; _fadeIn = fadeIn; _level = 0;
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = 0;
            if (at is TimeSpan t) MediaPlayer.Play(song, t); else MediaPlayer.Play(song);
        }

        public static void Update(float dt)
        {
            if (_next != null || _stopping)
            {
                _level -= dt / FadeOut;
                if (_level <= 0)
                {
                    _level = 0;
                    if (_next != null) Start(_next, _nextFadeIn, _nextAt);
                    else { MediaPlayer.Stop(); _stopping = false; }
                }
            }
            else _level = MathF.Min(1, _level + dt / MathF.Max(0.01f, _fadeIn));
            MediaPlayer.Volume = Settings.MusicVolume * _level * Duck;
        }
    }
}
