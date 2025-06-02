using Fastboot;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using HuaweiUpdateLibrary;
using HuaweiUpdateLibrary.Core;
using HuaweiUpdateLibrary.Streams;
using Potato.Fastboot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Net;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ICSharpCode.SharpZipLib.BZip2;
using SharpCompress.Compressors.Xz;

namespace FastbootFlasher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; }
        public string[] skipPartitions = ["sha256rsa", "crc", "curver", "verlist", "package_type", "base_verlist", "base_ver", "ptable_cust", "cust_verlist", "cust_ver", "preload_verlist", "preload_ver", "ptable_preload"];
        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            Log.IsReadOnly = true;
            FilePath.IsReadOnly = true;

        }

        private void LoadFile_Click(object sender, RoutedEventArgs e)
        {
            Listview.Items.Clear();
            FilePath.Clear();
            Log.Clear();
            Microsoft.Win32.OpenFileDialog openFileDialog = new()
            {
                Filter = "固件文件|*.app;flash_all.bat;payload.bin",
                Multiselect = true,
                Title = "选择文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                if (openFileDialog.SafeFileName == "flash_all.bat")
                {
                    FilePath.Text += openFileDialog.FileName;
                    string[] lines = File.ReadAllLines(openFileDialog.FileName);
                    Regex flashRegex = new(@"\bflash\s+(\w+)\s+(?:%~dp0)?images[\\/]([\w\.\-\\/]+)(?=\s|\|)", RegexOptions.IgnoreCase);
                    int i=0;
                    foreach (string line in lines)
                    {
                        Match match = flashRegex.Match(line);
                        if (match.Success)
                        {
                            i++;
                            string original = match.Groups[1].Value;
                            string partitionName;
                            if (original.EndsWith("_ab"))
                            {
                                partitionName = original[..^3];
                            }
                            else
                            {
                                partitionName = original;
                            }
                            string imgName = match.Groups[2].Value.Replace("\\", "/");
                            string batPath = openFileDialog.FileName[..^13];
                            string imgPath= @$"{batPath}images\{imgName}";
                            FileInfo fileInfo = new(imgPath);
                            long imgSize = fileInfo.Length;

                            Listview.Items.Add(new ListViewItem { Num = i, Size = FormatSize(imgSize), Address = "0x" + imgSize.ToString("X8"),Partitions = partitionName, SourceFilePath = imgPath });
                        }
                    }
                }
                if (openFileDialog.SafeFileName == "payload.bin")
                {
                    PayloadProcess.ReadInfo(openFileDialog.FileName);
                }
                else
                {
                    APPProcess.ReadInfo(openFileDialog.FileNames); 
                }
            }

        }

        private async void Flash_Click(object sender, RoutedEventArgs e)
        {
            if (Listview.SelectedItems.Count == 0)
            {
                MessageBox.Show("未选择分区！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;

            }
            var devices = Potato.Fastboot.Fastboot.GetDevices();
            if (devices.Length == 0)
            {
                Log.Text += "未检测到Fastboot设备\n";
                return;
            }
            if (devices.Length > 1)
            {
                Log.Text += "检测到多台设备，请只连接一台设备！\n";
                return;
            }
            Log.Text += $"已检测到设备：{string.Join(", ", devices)}\n";
            Potato.Fastboot.Fastboot fastboot = new();
            FastbootCmd fb = new();
            Log.Text += "连接设备...\n";
            fastboot.Connect();
            Log.Text += $"已连接到设备：{fastboot.GetSerialNumber()}\n\r";
            Log.Text += "读取设备解锁状态...\n";
            Potato.Fastboot.Fastboot.Response response;
            if (FilePath.Text.EndsWith("flash_all.bat"))
            {
                response = fastboot.Command("oem device-info");
                Log.Text += $"{response.Payload}";
                fastboot.Disconnect();

                Flash.IsEnabled = false;
                RebootButton.IsEnabled = false;
                Flash.Content="刷写中...";
                Listview.IsEnabled = false;

                foreach (var item in Listview.SelectedItems.Cast<ListViewItem>())
                {
                    string partition = item.Partitions;
                    string FilePath = item.SourceFilePath;
                    Log.Text += $"正在刷入 {partition} 分区...\n";
                    await fb.Fastboot($@"flash {partition} {FilePath}");
                    Log.Text += $"刷入 {partition} 完成！\n\r";
                    Log.ScrollToEnd();
                }
            }
            if (FilePath.Text.EndsWith("payload.bin"))
            {
                response = fastboot.Command("oem device-info");
                Log.Text += $"{response.Payload}";
                fastboot.Disconnect();
                Listview.IsEnabled = false;
                await PayloadProcess.ExtractSelectedPartitions();
                Flash.IsEnabled = false;
                RebootButton.IsEnabled = false;
                Flash.Content = "刷写中...";
                Listview.IsEnabled = false;
                foreach (var item in Listview.SelectedItems.Cast<ListViewItem>())
                {
                    string partition = item.Partitions;
                    Log.Text += $"正在刷入 {partition} 分区...\n";
                    await fb.Fastboot($@"flash {partition} .\images\{partition}.img");
                    File.Delete($@".\images\{partition}.img");
                    Log.Text += $"刷入 {partition} 完成！\n\r";
                    Log.ScrollToEnd();
                }
            }
            else
            {
                response = fastboot.Command("oem lock-state info");
                Log.Text += $"{response.Payload}";
                fastboot.Disconnect();
                Listview.IsEnabled = false;
                await APPProcess.ExtractSelectedPartitions(true);
                RebootButton.IsEnabled = false;
                Flash.IsEnabled = false;
                Flash.Content = "刷写中...";
                foreach (var item in Listview.SelectedItems.Cast<ListViewItem>())
                {
                    string partition = item.Partitions;
                    if (skipPartitions.Contains(partition))
                    {
                        Log.Text += $"跳过刷入{partition}分区\n\r";
                        continue;
                    }
                    Log.Text += $"正在刷入 {partition} 分区...\n";
                    if (partition == "hisiufs_gpt") partition = "ptable";
                    if (partition == "ufsfw") partition = "ufs_fw";
                    if (partition == "super") if (!File.Exists($@".\images\super.img")) continue;
                    await fb.Fastboot($@"flash {partition} .\images\{partition}.img");
                    File.Delete($@".\images\{partition}.img");
                    Log.Text += $"刷入 {partition} 完成！\n\r";
                    Log.ScrollToEnd();
                }
            }     
            Listview.IsEnabled = true;
            RebootButton.IsEnabled = true;
            Flash.Content = "刷写";
            Flash.IsEnabled = true;
            Log.Text += $"所有选中分区刷写完成！\n\r";
            Log.ScrollToEnd();
        }

        private void RebootButton_Click(object sender, RoutedEventArgs e)
        {
            var devices = Potato.Fastboot.Fastboot.GetDevices();
            if (devices.Length == 0)
            {
                Log.Text += "未检测到Fastboot设备\n";
                return;
            }
            if (devices.Length > 1)
            {
                Log.Text += "检测到多台设备，请只连接一台设备！\n";
            }
            Potato.Fastboot.Fastboot fastboot = new();
            fastboot.Connect();
            fastboot.Command("reboot");
            fastboot.Disconnect();
            Log.Text += "重启完成！\n\r";
        }

        public static string FormatSize(long size)
        {
            if (size < 1024)
                return $"{size}B";
            else if (size < 1024 * 1024)
                return $"{size / 1024.0:F2}KB";
            else if (size < 1024 * 1024 * 1024)
                return $"{size / (1024.0 * 1024.0):F2}MB";
            else
                return $"{size / (1024.0 * 1024.0 * 1024.0):F2}GB";
        }

        private async void MenuItemExtractSelected_Click(object sender, RoutedEventArgs e)
        {
            if (FilePath.Text.EndsWith("flash_all.bat"))
            {
               return; 
            }
            if (FilePath.Text.EndsWith("payload.bin"))
            {
                Listview.IsEnabled = false;
                await PayloadProcess.ExtractSelectedPartitions();
            }
            else
            {
                Listview.IsEnabled = false;
                await APPProcess.ExtractSelectedPartitions();
            }
            Listview.IsEnabled = true;

        }

        private void OpenGit_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/Natsume324/FastbootFlasher") { UseShellExecute = true });
        }

        private void OpenQQ_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://qm.qq.com/q/Hnjo84QWUC") { UseShellExecute = true });
        }

        private async void FbToUp_Click(object sender, RoutedEventArgs e)
        {
            if(this.FilePath.Text == "" || this.FilePath.Text.EndsWith("flash_all.bat") || this.FilePath.Text.EndsWith("payload.bin"))
            {
                MessageBox.Show("请先选择华为固件文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                this.Tab.SelectedItem = this.Tab1;
                return;
            }
            bool found = false;
            foreach (var item in Listview.Items.Cast<ListViewItem>())
            {
                if (item.Partitions == "recovery_ramdisk")
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                MessageBox.Show("请选择基础包固件文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                this.Tab.SelectedItem = this.Tab1;
                return;
            }

            Potato.Fastboot.Fastboot fastboot = new();
            var devices = Potato.Fastboot.Fastboot.GetDevices();
            if (devices.Length == 0)
            {
                MessageBox.Show("未检测到Fastboot设备", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (devices.Length > 1)
            {
                MessageBox.Show("检测到多台设备，请只连接一台设备!", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            this.Tab.SelectedItem = this.Tab1;
            Log.Text += $"已检测到设备：{string.Join(", ", devices)}\n";
            Log.Text += "连接设备...\n";
            fastboot.Connect();
            Log.Text += $"已连接到设备：{fastboot.GetSerialNumber()}\n\r";
            Log.Text += "查询救援版本:";
            Potato.Fastboot.Fastboot.Response response;
            response = fastboot.Command("getvar:rescue_version");
            Log.Text += $"{response.Payload}\n";
            foreach (var item in Listview.Items.Cast<ListViewItem>())
            {
                string partition = item.Partitions;
                int num = item.Num;
                string FilePath = item.SourceFilePath;
                if (partition == "kernel")
                {
                    var appfile = UpdateFile.Open(FilePath, false);
                    var entry = appfile.Entries[num];
                    var progressReporter = new Progress<double>(p => ProgressBar1.Value = p);
                    Log.Text += $"开始提取{entry.FileType.ToLower()}...\n";
                    await APPProcess.ExtractPartition(FilePath, num);
                    Log.Text += $"提取{entry.FileType.ToLower()}分区完成！\n\r";
                    Log.ScrollToEnd();
                }    
                if (partition == "recovery_ramdisk")
                {
                    var appfile = UpdateFile.Open(FilePath, false);
                    var entry = appfile.Entries[num];
                    var progressReporter = new Progress<double>(p => ProgressBar1.Value = p);
                    Log.Text += $"开始提取{entry.FileType.ToLower()}...\n";
                    await APPProcess.ExtractPartition(FilePath, num);
                    Log.Text += $"提取{entry.FileType.ToLower()}分区完成！\n\r";
                    Log.ScrollToEnd();
                }
                if (partition == "recovery_vendor")
                {
                    var appfile = UpdateFile.Open(FilePath, false);
                    var entry = appfile.Entries[num];
                    var progressReporter = new Progress<double>(p => ProgressBar1.Value = p);
                    Log.Text += $"开始提取{entry.FileType.ToLower()}...\n";
                    await APPProcess.ExtractPartition(FilePath, num);
                    Log.Text += $"提取{entry.FileType.ToLower()}分区完成！\n\r";
                    Log.ScrollToEnd();
                }
                Log.ScrollToEnd();
            }
            Log.Text += "刷入recovery_kernel...\n";
            await fastboot.UploadData($@".\images\kernel.img");
            File.Delete($@".\images\kernel.img");
            fastboot.Command("flash:rescue_recovery_kernel");
            Log.Text += "刷入recovery_kernel完成！\n\r";
            Log.Text += "刷入recovery_ramdisk...\n";
            await fastboot.UploadData($@".\images\recovery_ramdisk.img");
            File.Delete($@".\images\recovery_ramdisk.img");
            fastboot.Command("flash:rescue_recovery_ramdisk");
            Log.Text += "刷入recovery_ramdisk完成！\n\r";
            Log.Text += "刷入recovery_vendor...\n";
            await fastboot.UploadData($@".\images\recovery_vendor.img");
            File.Delete($@".\images\recovery_vendor.img");
            fastboot.Command("flash:rescue_recovery_vendor");
            Log.Text += "刷入recovery_vendor完成！\n\r";
            Log.Text += "跳转升级模式...\n";
            fastboot.Command("getvar:rescue_enter_recovery");
            Log.Text += "跳转升级模式完成！\n\r";
            Log.ScrollToEnd();
            fastboot.Disconnect();

        }

    }


    public class ListViewItem
    {
        public int Num { get; set; }
        public string Partitions { get; set; }
        public string Size { get; set; }
        public string Address { get; set; }

        public string SourceFilePath { get; set; } 
    }


}



