using System;
using System.IO;
using System.Text;

namespace Poker.Adventure
{
    /// <summary>One atomic file contains the run and its progression settlement together.</summary>
    public sealed class AdventureSaveStore
    {
        public string Path { get; }
        public bool Exists => File.Exists(Path);
        public AdventureSaveStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is required.");
            Path = path;
        }
        public string Read() => File.ReadAllText(Path, Encoding.UTF8);
        public void Write(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("A complete snapshot is required.");
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = Path + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (Exists) File.Replace(temporary, Path, Path + ".bak");
                else File.Move(temporary, Path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
