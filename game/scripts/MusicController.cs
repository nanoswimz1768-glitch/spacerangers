using Godot;
using System.IO;

namespace SpaceManagersPrototype;

public partial class MusicController : Node
{
    private const string ProjectMusicPath = "res://assets/music";
    private const string ExternalMusicFolder = "music";

    private AudioStreamPlayer? _player;

    public override void _Ready()
    {
        if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _player = new AudioStreamPlayer
        {
            Name = "MusicPlayer",
            VolumeDb = -9f,
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
    }

    public override void _ExitTree()
    {
        if (_player is null)
        {
            return;
        }

        _player.Stop();
        _player.Stream = null;
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
