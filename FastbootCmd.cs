using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FastbootFlasher
{
    class FastbootCmd
    {
        public static async Task<string> Command(string fbshell)
        {
            string cmd = @".\tools\fastboot.exe";
            ProcessStartInfo fastboot = new ProcessStartInfo(cmd, fbshell)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };
            using Process fb = new Process();
            fb.StartInfo = fastboot;
            _ = fb.Start();
            string output = await fb.StandardError.ReadToEndAsync();
            if (output == "")
            {
                output = await fb.StandardOutput.ReadToEndAsync();
            }
            fb.WaitForExit();
            return output;
        }

        
    }
}
