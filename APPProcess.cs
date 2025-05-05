using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HuaweiUpdateLibrary;
using HuaweiUpdateLibrary.Core;
using HuaweiUpdateLibrary.Streams;

namespace FastbootFlasher
{
    class APPProcess
    {
        public async Task ExtractPartition(string FilePath, int index, IProgress<double> progress)
        {
            var APPFile = UpdateFile.Open(FilePath,false);
            var entry = APPFile.Entries[index];
            Directory.CreateDirectory(@".\images");

            long totalSize = entry.FileSize;
            long currentBytes = 0;
            string partition = entry.FileType.ToLower();
            if (partition =="hisiufs_gpt")
            {
                partition = "ptable";
            }
            if(partition == "ufsfw")
            {
                partition = "ufs_fw";
            }
            using (var entryStream = entry.GetDataStream(FilePath))
            using (var fileStream = new FileStream(@$".\images\{partition}.img", FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[1024 * 1024];
                int bytesRead;

                while ((bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    currentBytes += bytesRead;

                    
                    progress?.Report((double)currentBytes / totalSize * 100);
                }
            }
        }
    }
}
