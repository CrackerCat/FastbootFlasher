﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FastbootFlasher
{
    class FastbootCmd
    {
        public static async Task<string> Command(string fbshell)
        {
            StringBuilder outputBuilder = new();

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

                fb.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MainWindow.Instance.LogBox.AppendText(e.Data + Environment.NewLine);
                            MainWindow.Instance.LogBox.ScrollToEnd();
                            MainWindow.Instance.LogBox.CaretIndex = MainWindow.Instance.LogBox.Text.Length;
                        });

                        lock (outputBuilder)
                        {
                            outputBuilder.AppendLine(e.Data);
                        }
                    }
                };

                fb.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MainWindow.Instance.LogBox.AppendText( e.Data + Environment.NewLine);
                            MainWindow.Instance.LogBox.ScrollToEnd();
                            MainWindow.Instance.LogBox.CaretIndex = MainWindow.Instance.LogBox.Text.Length;
                        });

                        lock (outputBuilder)
                        {
                            outputBuilder.AppendLine( e.Data);
                        }
                    }
                };

                fb.Start();
                fb.BeginOutputReadLine();
                fb.BeginErrorReadLine();
                fb.WaitForExit();
                fb.Close();
            });

            return outputBuilder.ToString();
        }
    }
}
