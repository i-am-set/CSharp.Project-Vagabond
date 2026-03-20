using System.Collections.Generic;

namespace ProjectVagabond.Audio
{
    public class AudioManifest
    {
        public List<AudioEntry> Sfx { get; set; } = new List<AudioEntry>();
        public List<AudioEntry> Ui { get; set; } = new List<AudioEntry>();
        public List<MusicEntry> Music { get; set; } = new List<MusicEntry>();
        public List<AudioEntry> Ambient { get; set; } = new List<AudioEntry>();
    }

    public class AudioEntry
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public int PoolSize { get; set; } = 3;
        public float DefaultVolume { get; set; } = 1.0f;
        public float MinPitch { get; set; } = 0.0f;
        public float MaxPitch { get; set; } = 0.0f;
    }

    public class MusicEntry
    {
        public string Id { get; set; }
        public List<string> StemPaths { get; set; } = new List<string>();
        public float DefaultVolume { get; set; } = 1.0f;
    }
}