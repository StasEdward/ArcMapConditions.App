using System;
using System.IO;
using System.Media;

namespace ArcMapConditions.App.Services;

/// <summary>Shows the reminder popup and plays the notification sound.</summary>
public sealed class NotificationService : IDisposable
{
    private readonly SoundPlayer? _player;

    public NotificationService()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "sounds", "notify.wav");
            if (File.Exists(path))
            {
                _player = new SoundPlayer(path);
                _player.Load();
            }
        }
        catch
        {
            _player = null;
        }
    }

    /// <summary>Plays the sound and opens a reminder window for the event.</summary>
    public void Notify(Subscription sub)
    {
        PlaySound();

        var toast = new ToastWindow(sub.Condition, sub.Map, sub.IconSlug);
        toast.Show();
    }

    private void PlaySound()
    {
        try
        {
            if (_player != null)
            {
                _player.Play();          // async, on its own thread
                return;
            }
        }
        catch
        {
            // fall through to the system sound
        }

        try
        {
            SystemSounds.Exclamation.Play();
        }
        catch
        {
            // no audio device — silently ignore
        }
    }

    public void Dispose() => _player?.Dispose();
}
