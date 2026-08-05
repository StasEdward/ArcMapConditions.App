using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace ArcMapConditions.App.Services;

/// <summary>Shows the reminder popup and plays the notification sound.</summary>
public sealed class NotificationService : IDisposable
{
    private readonly SoundPlayer? _player;
    private readonly object _lock = new();

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
        catch (Exception ex)
        {
            // Log the exception but don't crash the app
            System.Diagnostics.Debug.WriteLine($"Error initializing sound player: {ex.Message}");
            _player = null;
        }
    }

    /// <summary>Plays the sound and opens a reminder window for the event.</summary>
    public void Notify(Subscription sub)
    {
        // Play sound on separate thread to avoid blocking UI
        Task.Run(() => PlaySound());
        
        // Show toast window on main thread
        var toast = new ToastWindow(sub.Condition, sub.Map, sub.IconSlug);
        toast.Show();
    }

    private void PlaySound()
    {
        lock (_lock)
        {
            try
            {
                if (_player != null)
                {
                    _player.PlaySync(); // Use synchronous play to ensure completion
                    return;
                }
            }
            catch (Exception ex)
            {
                // Log the exception but don't crash the app
                System.Diagnostics.Debug.WriteLine($"Error playing sound: {ex.Message}");
            }

            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch (Exception ex)
            {
                // Log the exception but don't crash the app
                System.Diagnostics.Debug.WriteLine($"Error playing system sound: {ex.Message}");
            }
        }
        }

    public void Dispose()
    {
        lock (_lock)
        {
            _player?.Dispose();
        }
    }
}
