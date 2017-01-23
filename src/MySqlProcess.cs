using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace MySqlUtils
{
    public class MySqlProcess
    {
        private readonly Process _process;
        private readonly StringBuilder _output;
        private readonly ManualResetEvent _processEvent;

        private MySqlProcess(string exe, string arguments)
        {
            CommandLine = String.Format("\"{0}\" {1}", exe, arguments);

            var processInfo = new ProcessStartInfo(exe, arguments);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardInput = true;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            _process = Process.Start(processInfo);
            ProcessName = _process.ProcessName;
            Id = _process.Id;
            StartTime = DateTime.UtcNow;

            // hook process event
            _processEvent = new ManualResetEvent(true);
            _processEvent.SafeWaitHandle = new SafeWaitHandle(_process.Handle, false);

            _output = new StringBuilder();
            var ignoreWarning = false;
            DataReceivedEventHandler stdHandler = (object sender, DataReceivedEventArgs e) =>
            {
                if (!ignoreWarning)
                {
                    ignoreWarning = e.Data != null && e.Data.Contains("Using a password on the command line interface can be insecure");
                    if (ignoreWarning)
                    {
                        return;
                    }
                }

                lock (_output)
                {
                    _output.AppendLine(e.Data);
                }
            };

            // hook stdout and stderr
            _process.OutputDataReceived += stdHandler;
            _process.BeginOutputReadLine();
            _process.ErrorDataReceived += stdHandler;
            _process.BeginErrorReadLine();
        }

        public string CommandLine
        {
            get; private set;
        }

        public string ProcessName
        {
            get; private set;
        }

        public int Id
        {
            get; private set;
        }

        public DateTime StartTime
        {
            get; private set;
        }

        public Stream Input
        {
            get { return _process.StandardInput.BaseStream; }
        }

        public string Output
        {
            get { return _output.ToString(); }
        }

        public int ExitCode
        {
            get { return _process.ExitCode; }
        }

        public bool HasExited
        {
            get { return _process.HasExited; }
        }

        public void Kill()
        {
            _process.Kill();
        }

        public void WaitForExit()
        {
            _process.WaitForExit();
        }

        public async Task WaitForExitAsync(TimeSpan timeout)
        {
            try
            {
                await _processEvent.WaitHandleAsync(timeout);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(String.Format("Execute sql process {0}:{1} has exceeded {2} timeout", ProcessName, Id, timeout));
            }
        }

        public static MySqlProcess Start(string exe, string arguments)
        {
            return new MySqlProcess(exe, arguments);
        }
    }
}