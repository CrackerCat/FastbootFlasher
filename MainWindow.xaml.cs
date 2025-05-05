using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Text;
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
using HuaweiUpdateLibrary;
using HuaweiUpdateLibrary.Core;
using HuaweiUpdateLibrary.Streams;
using Potato.Fastboot;
using FastbootCLI;
using System.Collections.Concurrent;

namespace FastbootFlasher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Log.IsReadOnly = true;
            FilePath.IsReadOnly = true;

        }

        private void LoadFile_Click(object sender, RoutedEventArgs e)
        {
            Listview.Items.Clear();
            Log.Clear();
            Microsoft.Win32.OpenFileDialog openFileDialog =new();
            openFileDialog.Filter = "APP文件|*.app";
            openFileDialog.Multiselect = false;
            openFileDialog.Title = "选择文件"; 

            if (openFileDialog.ShowDialog() == true)
            {
                string FilePath = openFileDialog.FileName;
                var appfile = UpdateFile.Open(FilePath, false);
                var entry1 = appfile.Entries[2];
                Stream dataStream = entry1.GetDataStream(FilePath);
                StreamReader reader = new StreamReader(dataStream, Encoding.UTF8);
                string content = reader.ReadToEnd();
                Log.AppendText("版本信息：\n");
                Log.Text+=content+"\n\r";
                for (int i = 0; i < appfile.Entries.Count; i++)
                {
                    var entry = appfile.Entries[i];

                    Listview.Items.Add(new ListViewItem { Num = i, Partitions = entry.FileType.ToLower(), Size = FormatSize(entry.FileSize),Address= "0x" + entry.FileSize.ToString("X8") });
                }
                this.FilePath.Text = FilePath;
            }

        }

        static string FormatSize(long size)
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

        private async void Flash_Click(object sender, RoutedEventArgs e)
        {
            var devices = Fastboot.GetDevices();
            if (devices.Length == 0)
            {
                Log.Text += "未检测到Fastboot设备\n";
                return;
            }
            if (devices.Length>1)
            {
                Log.Text += "检测到多台设备，请只连接一台设备！\n";
            }     
            Log.Text += $"已检测到设备：{string.Join(", ", devices)}\n";
            Fastboot fastboot= new Fastboot();
            Log.Text += "连接设备...\n";
            fastboot.Connect();
            Log.Text += $"已连接到设备：{fastboot.GetSerialNumber()}\n\r";
            Log.Text += "读取设备解锁状态...\n";
            var response = fastboot.Command("oem lock-state info");
            if (response.Status == Fastboot.Status.Okay)
            {
                Log.Text+=$"{response.Payload}";
            }
            else
            {
                Log.Text += $"错误：{response.Payload}\n";
            }
            if (Listview.SelectedItems.Count == 0)
            {
                MessageBox.Show("未选择分区！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
                
            }
            foreach (var item in Listview.SelectedItems)
            {
                if (item is ListViewItem selectedItem)
                {
                    int num = selectedItem.Num;
                    var appfile = UpdateFile.Open(FilePath.Text, false);
                    var entry = appfile.Entries[num];
                    APPProcess APPprocess = new();
                    var progressReporter = new Progress<double>(p => ProgressBar1.Value = p);
                    Log.Text+= $"开始提取{entry.FileType.ToLower()}...\n";
                    await APPprocess.ExtractPartition(FilePath.Text,num, progressReporter);
                    Log.Text += $"提取{entry.FileType.ToLower()}分区完成！\n\r";
                    Log.ScrollToEnd(); 
                }
            }
            Log.Text += "所有选中分区提取完成！\n\r";
            fastboot.Disconnect();
            foreach (var item in Listview.SelectedItems.Cast<ListViewItem>())
            {
                string partition = item.Partitions;
                
                Log.Text += $"正在刷入 {partition} 分区...\n";
                if (partition == "hisiufs_gpt")
                {
                    partition = "ptable";
                }
                if (partition == "ufsfw")
                {
                    partition = "ufs_fw";
                }
                await RunFastbootAsync(
                    arguments: $@"flash {partition} .\images\{partition}.img",
                    outputCallback: msg =>
                    {
                       
                        Dispatcher.Invoke(() =>
                        {
                            Log.Text += $"{msg}";
                            Log.ScrollToEnd();
                        });
                    }
                );

                File.Delete($@".\images\{partition}.img");
                Log.Text += $"刷入 {partition} 完成！\n\r";
            }
            Log.Text += $"所有选中分区完成！\n\r";
            Log.ScrollToEnd();

        }
        private async Task RunFastbootAsync(string arguments, Action<string> outputCallback)
        {
            await Task.Run(() =>
            {
                using (var fastboot = new FastbootCli(arguments))
                {
                    while (!fastboot.stdout.EndOfStream)
                    {
                        string line = fastboot.stdout.ReadLine();
                        outputCallback?.Invoke(line);
                    }

                    string error = fastboot.stderr.ReadToEnd();
                    if (!string.IsNullOrEmpty(error))
                    {
                        outputCallback?.Invoke($"{error}");
                    }
                }
            });
        }

        private void RebootButton_Click(object sender, RoutedEventArgs e)
        {
            Fastboot fastboot = new();
            fastboot.Connect();
            fastboot.Command("reboot");
            fastboot.Disconnect();
            Log.Text += "重启完成！\n\r";
        }
    }


    public class ListViewItem
    {
        public int Num { get; set; }
        public string Partitions { get; set; }
        public string Size { get; set; }
        public string Address { get; set; }
    }

}


