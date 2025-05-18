using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using HuaweiUpdateLibrary;
using HuaweiUpdateLibrary.Core;
using HuaweiUpdateLibrary.Streams;

namespace FastbootFlasher
{
    class APPProcess
    {
        private const uint SPARSE_HEADER_MAGIC = 0xED26FF3A;
        private const uint DEFAULT_BLOCK_SIZE = 4096;
        public async Task ExtractPartition(string FilePath, int index, IProgress<double> progress)
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

                while ((bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                    currentBytes += bytesRead;


                    progress?.Report((double)currentBytes / totalSize * 100);
                }
            }
        }
        public async Task MergerSuperSparse(string super1 = $@".\images\super.1.img", string super2 = $@".\images\super.2.img", string super = $@".\images\super.img")
        {
            using (FileStream fs = new FileStream(super2, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            using (FileStream outputFs = new FileStream(super, FileMode.Create, FileAccess.Write))
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
                byte[] buffer = new byte[1024*1024];
                long bytesToRead = lastChunkOffset;
                int bytesRead;
                while (bytesToRead > 0 && (bytesRead = await fs.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, bytesToRead))) > 0)
                {
                    await outputFs.WriteAsync(buffer, 0, bytesRead);
                    bytesToRead -= bytesRead;
                }
            }
            using (FileStream fs = new FileStream(super1, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            using (FileStream outputFs = new FileStream(super, FileMode.Append, FileAccess.Write))
            {
                SparseHeader header = ReadSparseHeader(reader);

                long firstChunkOffset = fs.Position;
                ChunkHeader firstChunk = ReadChunkHeader(reader);
                long newDataOffset = fs.Position + (firstChunk.TotalSize - header.ChunkHeaderSize);

                fs.Seek(newDataOffset, SeekOrigin.Begin);
                byte[] buffer = new byte[1024 * 1024];
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await outputFs.WriteAsync(buffer, 0, bytesRead);
                }
            }
            SparseHeader header1 = ReadSparseHeader(super1);
            SparseHeader header2 = ReadSparseHeader(super2);
            uint newTotalChunks = (header1.TotalChunks - 1) + (header2.TotalChunks - 1);
            using (FileStream fs = new FileStream(super, FileMode.Open, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(fs))
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

        static SparseHeader ReadSparseHeader(BinaryReader reader)
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
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                return ReadSparseHeader(reader);
            }
        }
    }
}
