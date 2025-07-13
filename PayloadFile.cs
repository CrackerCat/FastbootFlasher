using ChromeosUpdateEngine;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.BZip2;
using ZstdNet;
using PayloadLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastbootFlasher
{
    class PayloadFile
    {
        public static List<ListViewItem> ParsePayloadFile(string filePath)
        {
            var entries = new List<ListViewItem>();
            using var parser = new PayloadParser(filePath);
            var partitions = parser.GetPartitions();
            for (int a = 0; a < partitions.Count; a++)
            {
                var partitionInfo = partitions[a];
                entries.Add(new ListViewItem
                {
                    Num = a,
                    Part = partitionInfo.Name,
                    Size = MainWindow.FormatSize(partitionInfo.Size),
                    Addr = $"0x{partitionInfo.Size:X8}",
                    Source = filePath
                });
            }
            return entries;
        }

        public static async Task ExtractPartition(string FilePath, int index)
        {
            using var parser = new PayloadParser(FilePath);
            var partitions = parser.GetPartitions();
            var partitionInfo = partitions[index];
            Directory.CreateDirectory(@".\images");

            long totalSize = partitionInfo.Size;
            long currentBytes = 0;
            string partition = partitionInfo.Name;

            using var dataStream = parser.GetPartitionStream(index);
            using (var fileStream = new FileStream(@$".\images\{partition}.img", FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[1024 * 1024];
                int bytesRead;

                while ((bytesRead = await dataStream.ReadAsync(buffer)) > 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                    currentBytes += bytesRead;

                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainWindow.pb.Value = (double)currentBytes / totalSize * 100;
                    });
                }
            }
        }
    }
    class MetadataInfo
    {
        public string ProductName { get; set; } = "";
        public string AndroidVersion { get; set; } = "";
        public string SecurityPatch { get; set; } = "";
        public string VersionName { get; set; } = "";

        public MetadataInfo(string metadataPath)
        {

            foreach (var line in File.ReadLines(metadataPath))
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2) continue;
                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "product_name":
                        ProductName = value;
                        break;
                    case "android_version":
                        AndroidVersion = value;
                        break;
                    case "security_patch":
                        SecurityPatch = value;
                        break;
                    case "version_name":
                        VersionName = value;
                        break;
                }
            }
        }


    }
}
