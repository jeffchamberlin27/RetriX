﻿using Plugin.FileSystem.Abstractions;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace RetriX.Shared.StreamProviders
{
    public class ArchiveStreamProvider : IStreamProvider
    {
        public static ISet<string> SupportedExtensions { get; } = new HashSet<string> { ".zip" };

        private string HandledScheme { get; }
        private IFileInfo ArchiveFile { get; }
        private ZipArchive Archive { get; set; }

        private IDictionary<string, ZipArchiveEntry> EntriesMapping { get; } = new SortedDictionary<string, ZipArchiveEntry>();
        private HashSet<Stream> OpenStreams { get; } = new HashSet<Stream>();

        public ArchiveStreamProvider(string handledScheme, IFileInfo archiveFile)
        {
            HandledScheme = handledScheme;
            ArchiveFile = archiveFile;
        }

        public void Dispose()
        {
            Archive?.Dispose();
            foreach (var i in OpenStreams)
            {
                i.Dispose();
            }

            OpenStreams.Clear();
        }

        public async Task<IEnumerable<string>> ListEntriesAsync()
        {
            await InitializeAsync();
            return EntriesMapping.Keys;
        }

        public async Task<Stream> OpenFileStreamAsync(string path, FileAccess accessType)
        {
            await InitializeAsync();
            if (!EntriesMapping.Keys.Contains(path))
            {
                return default(Stream);
            }

            var entry = EntriesMapping[path];
            using (var archiveStream = entry.Open())
            {
                var backingStore = new byte[entry.Length];
                using (var tempStream = new MemoryStream(backingStore, true))
                {
                    await archiveStream.CopyToAsync(tempStream);
                }

                var writeable = accessType == FileAccess.ReadWrite || accessType == FileAccess.Write;
                var output = new MemoryStream(backingStore, writeable);
                OpenStreams.Add(output);
                return output;
            }
        }

        public void CloseStream(Stream stream)
        {
            if (OpenStreams.Contains(stream))
            {
                stream.Dispose();
                OpenStreams.Remove(stream);
            }
        }

        private async Task InitializeAsync()
        {
            if (Archive != null)
            {
                return;
            }

            var stream = await ArchiveFile.OpenAsync(FileAccess.Read);
            Archive = new ZipArchive(stream, ZipArchiveMode.Read, false);
            foreach (var i in Archive.Entries)
            {
                EntriesMapping.Add(Path.Combine(HandledScheme, i.FullName.Replace('/', Path.DirectorySeparatorChar)), i);
            }
        }
    }
}
