using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using FastbootFlasher;

namespace Fastboot
{
    class FastbootCmd
    {
        public async Task Fastboot(string fbshell)
        {
            await Task.Run(() =>
            {
                string cmd = @".\tools\fastboot.exe";
                ProcessStartInfo fastboot = new(cmd, fbshell)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using Process fb = new();
                fb.StartInfo = fastboot;
                _ = fb.Start();
                fb.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);
                fb.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
                fb.BeginOutputReadLine();
                fb.BeginErrorReadLine();
                fb.WaitForExit();
                fb.Close();
            });
        }

        private async void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (!string.IsNullOrEmpty(outLine.Data))
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MainWindow.Instance.Log.AppendText(outLine.Data + Environment.NewLine);
                    MainWindow.Instance.Log.ScrollToEnd();
                    MainWindow.Instance.Log.CaretIndex = MainWindow.Instance.Log.Text.Length;
                    string output="";
                    output += outLine.Data + Environment.NewLine;
                });
            }
        }
    }
}
