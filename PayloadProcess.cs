using ChromeosUpdateEngine;
using HuaweiUpdateLibrary.Core;
using PayloadLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FastbootFlasher
{
    class PayloadProcess
    {
        public static void ReadInfo(string FilePath)
        {
            using var parser = new PayloadParser(FilePath);
            var partitions = parser.GetPartitions();
            
            for (int a = 0; a < partitions.Count; a++)
            {
                var partitionInfo = partitions[a];
                MainWindow.Instance.Listview.Items.Add(new ListViewItem { Num = a, Partitions = partitionInfo.Name, Size = MainWindow.FormatSize(partitionInfo.Size), Address = "0x" + partitionInfo.Size.ToString("X8"), SourceFilePath = FilePath});
            }
            MainWindow.Instance.FilePath.Text += FilePath;
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


                    MainWindow.Instance.ProgressBar1.Value = (double)currentBytes / totalSize * 100;
                }
            }
        }
        public static async Task ExtractSelectedPartitions()
        {
            var selectedItems = MainWindow.Instance.Listview.SelectedItems.Cast<ListViewItem>().ToList();
            int totalItems = selectedItems.Count;
            int completedCount = 0;

            MainWindow.Instance.ProgressBar1.Maximum = totalItems;
            MainWindow.Instance.ProgressBar1.Value = 0;

            var tasks = selectedItems.Select(async item =>
            {
                string partition = item.Partitions;
                string filePath = item.SourceFilePath;

                MainWindow.Instance.Dispatcher.Invoke(() => {
                    MainWindow.Instance.Log.Text += $"正在提取 {partition} 分区...\n";
                    MainWindow.Instance.Log.ScrollToEnd();
                });

                var progressReporter = new Progress<double>(p => {});

                await PayloadProcess.ExtractPartition(filePath, item.Num);

                Interlocked.Increment(ref completedCount);

                MainWindow.Instance.Dispatcher.Invoke(() => {
                    MainWindow.Instance.Log.Text += $"{partition} 分区提取完成！\n";
                    MainWindow.Instance.ProgressBar1.Value = completedCount;
                    MainWindow.Instance.Log.ScrollToEnd();
                });
            }).ToList();

            await Task.WhenAll(tasks);

            MainWindow.Instance.Dispatcher.Invoke(() => {
                MainWindow.Instance.Log.Text += "所有分区提取完成！\n";
            });
        }

    }
}
