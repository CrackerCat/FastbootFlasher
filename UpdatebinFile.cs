using HuaweiUpdateLibrary.Core;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FastbootFlasher
{
    class UpdatebinFile
    {
        const int COMPINFO_LEN_OFFSET = 178;

        public class PartitionInfo
        {
            public int Index { get; set; } 
            public string Name { get; set; }
            public long Offset { get; set; }
            public long Size { get; set; }
            public string SourceFile { get; set; }
        }
        const int CHUNK_SIZE = 1024 * 1024; // 1MB
        public static List<ListViewItem> ParseUpdatebinFile(string filePath)
        {
            var entries = new List<ListViewItem>();
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                long inputFileSize = fs.Length;
                const int COMPINFO_LEN_OFFSET = 178;

                fs.Seek(COMPINFO_LEN_OFFSET, SeekOrigin.Begin);
                ushort compinfoAllSize = reader.ReadUInt16();

                long type2Offset = COMPINFO_LEN_OFFSET + 2L + compinfoAllSize + 16;
                fs.Seek(type2Offset, SeekOrigin.Begin);
                ushort type2 = reader.ReadUInt16();

                long dataStartOffset;
                if (type2 == 0x08)
                {
                    uint hashdataSize = reader.ReadUInt32();
                    dataStartOffset = type2Offset + 2 + 4 + hashdataSize;
                }
                else if (type2 == 0x06)
                {
                    reader.ReadBytes(16); // 跳过16字节
                    uint hashdataSize = reader.ReadUInt32();
                    dataStartOffset = type2Offset + 2 + 16 + 4 + hashdataSize;
                }
                else
                {
                    throw new Exception($"未知的类型: 0x{type2:X4}");
                }

                int componentCount = compinfoAllSize / 87;
                long currentOffset = COMPINFO_LEN_OFFSET + 2;
                long currentDataOffset = dataStartOffset;


                for (int count = 1; count <= componentCount; count++)
                {
                    fs.Seek(currentOffset, SeekOrigin.Begin);

                    // 读取组件名称
                    var nameBytes = new List<byte>();
                    byte b;
                    while ((b = reader.ReadByte()) != 0)
                    {
                        nameBytes.Add(b);
                    }

                    string componentName = Encoding.UTF8.GetString(nameBytes.ToArray());
                    componentName = componentName.TrimStart('/');

                    // 读取组件大小
                    fs.Seek(currentOffset + 47, SeekOrigin.Begin);
                    ulong componentSize = reader.ReadUInt64();
                    currentOffset += 87;

                    entries.Add(new ListViewItem
                    {
                        Num = count,
                        Part = componentName,
                        Size = MainWindow.FormatSize((long)componentSize),
                        Addr = $"0x{(long)componentSize:X8}",
                        Source = filePath
                    });
                
                    // 更新数据偏移量
                    currentDataOffset += (long)componentSize;
                }
            }
            return entries;
        }

        public static async Task ExtractPartition(string filePath, int partitionIndex)
        {
               
            List<PartitionInfo> partitions = GetAllPartitionsInfo(filePath);


            PartitionInfo partition = partitions[partitionIndex - 1];

            Directory.CreateDirectory(@".\images");
            long inputFileSize = new FileInfo(filePath).Length;
                
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (FileStream outFile = new FileStream(@$".\images\{partition.Name}.img", FileMode.Create))
            {
                fs.Seek(partition.Offset, SeekOrigin.Begin);
                long bytesLeft = partition.Size;
                byte[] buffer = new byte[CHUNK_SIZE];
                while (bytesLeft > 0)
                {
                    int readSize = (int)Math.Min(bytesLeft, CHUNK_SIZE);
                    int bytesRead = await fs.ReadAsync(buffer, 0, readSize);
                    outFile.Write(buffer, 0, bytesRead);
                    bytesLeft -= bytesRead;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainWindow.pb.Value = (double)(partition.Size - bytesLeft) / partition.Size * 100;
                    });
                    
                }
            }


        }

        
        public static List<PartitionInfo> GetAllPartitionsInfo(string filePath)
        {
            var partitions = new List<PartitionInfo>();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                long inputFileSize = fs.Length;

                fs.Seek(COMPINFO_LEN_OFFSET, SeekOrigin.Begin);
                ushort compinfoAllSize = reader.ReadUInt16();

                long type2Offset = COMPINFO_LEN_OFFSET + 2L + compinfoAllSize + 16;
                fs.Seek(type2Offset, SeekOrigin.Begin);
                ushort type2 = reader.ReadUInt16();

                long dataStartOffset;
                if (type2 == 0x08)
                {
                    uint hashdataSize = reader.ReadUInt32();
                    dataStartOffset = type2Offset + 2 + 4 + hashdataSize;
                }
                else if (type2 == 0x06)
                {
                    reader.ReadBytes(16); // 跳过16字节
                    uint hashdataSize = reader.ReadUInt32();
                    dataStartOffset = type2Offset + 2 + 16 + 4 + hashdataSize;
                }
                else
                {
                    throw new Exception($"未知的类型: 0x{type2:X4}");
                }

                int componentCount = compinfoAllSize / 87;
                long currentOffset = COMPINFO_LEN_OFFSET + 2;
                long currentDataOffset = dataStartOffset;

                for (int count = 1; count <= componentCount; count++)
                {
                    fs.Seek(currentOffset, SeekOrigin.Begin);

                    
                    var nameBytes = new List<byte>();
                    byte b;
                    while ((b = reader.ReadByte()) != 0)
                    {
                        nameBytes.Add(b);
                    }

                    string componentName = Encoding.UTF8.GetString(nameBytes.ToArray());
                    componentName = componentName.TrimStart('/');

                   
                    fs.Seek(currentOffset + 47, SeekOrigin.Begin);
                    ulong componentSize = reader.ReadUInt64();

                  
                    partitions.Add(new PartitionInfo
                    {
                        Index = count,
                        Name = componentName,
                        Offset = currentDataOffset,
                        Size = (long)componentSize,
                        SourceFile = filePath
                    });

                  
                    currentDataOffset += (long)componentSize;
                    currentOffset += 87;
                }
            }

            return partitions;
        }

    }
}
