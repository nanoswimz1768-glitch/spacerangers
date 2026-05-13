using Godot;
using System.IO;

namespace SpaceManagersPrototype;

public partial class MusicController : Node
{
    private const string ProjectMusicPath = "res://assets/music";
    private const string ExternalMusicFolder = "music";
    private const string SettingsPath = "user://audio_settings.cfg";
    private const float PlaybackVolumeDb = -9f;
    private const float MutedVolumeDb = -80f;

    private static bool _settingsLoaded;
    private static bool _muted;

    private AudioStreamPlayer? _player;

    public static event Action<bool>? MutedChanged;

    public static bool IsMuted
    {
        get
        {
            EnsureSettingsLoaded();
            return _muted;
        }
    }

    public static void ToggleMuted()
    {
        SetMuted(!IsMuted);
    }

    public static void SetMuted(bool muted)
    {
        EnsureSettingsLoaded();
        if (_muted == muted)
        {
            return;
        }

        _muted = muted;
        SaveSettings();
        MutedChanged?.Invoke(_muted);
    }

    public override void _Ready()
    {
        MutedChanged += OnMutedChanged;
        if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _player = new AudioStreamPlayer
        {
            Name = "MusicPlayer",
            VolumeDb = PlaybackVolumeDb,
            Autoplay = false
        };
        AddChild(_player);

        var stream = LoadStartupTrack();
        if (stream is null)
        {
            return;
        }

        if (stream is AudioStreamMP3 mp3)
        {
            mp3.Loop = true;
        }

        _player.Stream = stream;
        _player.Play();
        ApplyMutedState();
    }

    public override void _ExitTree()
    {
        MutedChanged -= OnMutedChanged;
        if (_player is null)
        {
            return;
        }

        _player.Stop();
        _player.Stream = null;
    }

    private void OnMutedChanged(bool muted)
    {
        ApplyMutedState();
    }

    private void ApplyMutedState()
    {
        if (_player is null)
        {
            return;
        }

        if (IsMuted)
        {
            _player.VolumeDb = MutedVolumeDb;
            _player.StreamPaused = true;
            return;
        }

        _player.VolumeDb = PlaybackVolumeDb;
        if (_player.Stream is not null && !_player.Playing)
        {
            _player.Play();
        }

        _player.StreamPaused = false;
    }

    private static void EnsureSettingsLoaded()
    {
        if (_settingsLoaded)
        {
            return;
        }

        _settingsLoaded = true;
        if (!Godot.FileAccess.FileExists(SettingsPath))
        {
            return;
        }

        try
        {
            using var file = Godot.FileAccess.Open(SettingsPath, Godot.FileAccess.ModeFlags.Read);
            var value = file?.GetAsText().Trim();
            _muted = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            _muted = false;
        }
    }

    private static void SaveSettings()
    {
        try
        {
            using var file = Godot.FileAccess.Open(SettingsPath, Godot.FileAccess.ModeFlags.Write);
            file?.StoreString(_muted ? "true" : "false");
        }
        catch
        {
            // Audio preference is nice to keep, but losing it should never break startup.
        }
    }

    private static AudioStream? LoadStartupTrack()
    {
        return TryLoadExternalTrack() ?? TryLoadProjectTrack();
    }

    private static AudioStream? TryLoadExternalTrack()
    {
        if (OS.HasFeature("editor"))
        {
            return null;
        }

        var executablePath = OS.GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        var musicDirectory = Path.Combine(executablePath.GetBaseDir(), ExternalMusicFolder);
        Directory.CreateDirectory(musicDirectory);

        foreach (var path in Directory.EnumerateFiles(musicDirectory, "*.mp3").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var stream = AudioStreamMP3.LoadFromFile(path);
                if (stream is not null)
                {
                    return stream;
                }
            }
            catch
            {
                // Ignore bad tracks and keep looking for a playable file.
            }
        }

        return null;
    }

    private static AudioStream? TryLoadProjectTrack()
    {
        using var directory = DirAccess.Open(ProjectMusicPath);
        if (directory is null)
        {
            return null;
        }

        var files = new List<string>();
        directory.ListDirBegin();
        while (true)
        {
            var fileName = directory.GetNext();
            if (string.IsNullOrEmpty(fileName))
            {
                break;
            }

            if (!directory.CurrentIsDir() && fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(fileName);
            }
        }

        directory.ListDirEnd();
        foreach (var fileName in files.OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase))
        {
            var stream = ResourceLoader.Load<AudioStream>($"{ProjectMusicPath}/{fileName}");
            if (stream is not null)
            {
                return stream;
            }
        }

        return null;
    }
}
