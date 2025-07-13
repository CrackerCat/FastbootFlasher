using HuaweiUpdateLibrary.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FastbootFlasher
{
    class APPFile
    {
        private const uint DEFAULT_BLOCK_SIZE = 4096;

        public static List<ListViewItem> ParseAPPFile(string filePath, out string versionInfo)
        {
            versionInfo = string.Empty;
            var entries = new List<ListViewItem>();
            var appfile = UpdateFile.Open(filePath, false);
            var entry = appfile.Entries[2];
            using var dataStream = entry.GetDataStream(filePath);
            using var reader = new StreamReader(dataStream, Encoding.UTF8);

            versionInfo = reader.ReadToEnd();

            for (int i = 0; i < appfile.Entries.Count; i++)
            {
                entry = appfile.Entries[i];
                entries.Add(new ListViewItem
                {
                    Num = i,
                    Part = entry.FileType.ToLower(),
                    Size = MainWindow.FormatSize(entry.FileSize),
                    Addr = $"0x{entry.FileSize:X8}",
                    Source = filePath
                });
            }
            
            return entries;
        }

        public static async Task ExtractPartition(string FilePath, int index)
        {
            var APPFile = UpdateFile.Open(FilePath, false);
            var entry = APPFile.Entries[index];
            Directory.CreateDirectory(@".\images");

            long totalSize = entry.FileSize;
            long currentBytes = 0;
            string partition = entry.FileType.ToLower();
            if (partition == "hisiufs_gpt")
                partition = "ptable";
            if (partition == "ufsfw")
                partition = "ufs_fw";
            if (partition == "super")
            {
                if (File.Exists($@".\images\super.img"))
                {
                    File.Move($@".\images\super.img", $@".\images\super.1.img");
                    partition = "super.2";
                }
            }

            using (var entryStream = entry.GetDataStream(FilePath))
            using (var fileStream = new FileStream(@$".\images\{partition}.img", FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[1024 * 1024];
                int bytesRead;

                while ((bytesRead = await entryStream.ReadAsync(buffer)) > 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                    currentBytes += bytesRead;

                    MainWindow.pb.Value = (double)currentBytes / totalSize * 100;
                }
            }
        }

        public static async Task MergerSuperSparse(string super1 = $@".\images\super.1.img", string super2 = $@".\images\super.2.img", string super = $@".\images\super.img")
        {
            FileInfo file1 = new(super1);
            FileInfo file2 = new(super2);
            if (file1.Length > file2.Length)
            {
                super1 = $@".\images\super.2.img";
                super2 = $@".\images\super.1.img";

            }

            using (FileStream fs = new(super2, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new(fs))
            using (FileStream outputFs = new(super, FileMode.Create, FileAccess.Write))
            {
                SparseHeader header = ReadSparseHeader(reader);
                long lastChunkOffset = 0;
                for (int i = 0; i < header.TotalChunks; i++)
                {
                    long chunkOffset = fs.Position;
                    ChunkHeader chunk = ReadChunkHeader(reader);
                    lastChunkOffset = chunkOffset;
                    fs.Seek(chunk.TotalSize - header.ChunkHeaderSize, SeekOrigin.Current);
                }
                fs.Seek(0, SeekOrigin.Begin);
                byte[] buffer = new byte[1024 * 1024];
                long bytesToRead = lastChunkOffset;
                int bytesRead;
                while (bytesToRead > 0 && (bytesRead = await fs.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, bytesToRead)))) > 0)
                {
                    await outputFs.WriteAsync(buffer.AsMemory(0, bytesRead));
                    bytesToRead -= bytesRead;
                }
            }
            using (FileStream fs = new(super1, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new(fs))
            using (FileStream outputFs = new(super, FileMode.Append, FileAccess.Write))
            {
                SparseHeader header = ReadSparseHeader(reader);

                long firstChunkOffset = fs.Position;
                ChunkHeader firstChunk = ReadChunkHeader(reader);
                long newDataOffset = fs.Position + (firstChunk.TotalSize - header.ChunkHeaderSize);

                fs.Seek(newDataOffset, SeekOrigin.Begin);
                byte[] buffer = new byte[1024 * 1024];
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer)) > 0)
                {
                    await outputFs.WriteAsync(buffer.AsMemory(0, bytesRead));
                }
            }
            SparseHeader header1 = ReadSparseHeader(super1);
            SparseHeader header2 = ReadSparseHeader(super2);
            uint newTotalChunks = (header1.TotalChunks - 1) + (header2.TotalChunks - 1);
            using (FileStream fs = new(super, FileMode.Open, FileAccess.Write))
            using (BinaryWriter writer = new(fs))
            {
                fs.Seek(0x0C, SeekOrigin.Begin);
                writer.Write(DEFAULT_BLOCK_SIZE);
                fs.Seek(0x14, SeekOrigin.Begin);
                writer.Write(newTotalChunks);
            }
        }
        
        private struct SparseHeader
        {
            public uint Magic;
            public ushort MajorVersion;
            public ushort MinorVersion;
            public ushort FileHeaderSize;
            public ushort ChunkHeaderSize;
            public uint BlockSize;
            public uint TotalBlocks;
            public uint TotalChunks;
            public uint ImageChecksum;
        }
        
        private struct ChunkHeader
        {
            public ushort ChunkType;
            public ushort Reserved;
            public uint ChunkSize;
            public uint TotalSize;
        }
        
        private static ChunkHeader ReadChunkHeader(BinaryReader reader)
        {
            return new ChunkHeader
            {
                ChunkType = reader.ReadUInt16(),
                Reserved = reader.ReadUInt16(),
                ChunkSize = reader.ReadUInt32(),
                TotalSize = reader.ReadUInt32()
            };
        }
       
        private static SparseHeader ReadSparseHeader(BinaryReader reader)
        {
            return new SparseHeader
            {
                Magic = reader.ReadUInt32(),
                MajorVersion = reader.ReadUInt16(),
                MinorVersion = reader.ReadUInt16(),
                FileHeaderSize = reader.ReadUInt16(),
                ChunkHeaderSize = reader.ReadUInt16(),
                BlockSize = reader.ReadUInt32(),
                TotalBlocks = reader.ReadUInt32(),
                TotalChunks = reader.ReadUInt32(),
                ImageChecksum = reader.ReadUInt32()
            };
        }
        
        private static SparseHeader ReadSparseHeader(string filePath)
        {
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
            using BinaryReader reader = new(fs);
            return ReadSparseHeader(reader);
        }
    }
}
