using System;
using System.Diagnostics;
using System.IO;

namespace FastbootCLI
{
    class FastbootCli : IDisposable
    {
        Process process;
        public StreamReader stdout;
        public StreamReader stderr;

        public FastbootCli(string action)
        {
            process = new Process();
            process.StartInfo.FileName = @".\hwtools\fastboot.exe";
            process.StartInfo.Arguments = action;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.UseShellExecute = false;
            process.Start();

            stdout = process.StandardOutput;
            stderr = process.StandardError;
        }

        public void Dispose()
        {
            process.Close();
            process = null;
        }

        ~FastbootCli()
        {
            if (process != null)
                Dispose();
        }
    }
}
