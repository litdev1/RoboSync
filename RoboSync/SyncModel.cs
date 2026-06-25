using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Timers;
using System.Windows.Threading;
using Timer = System.Timers.Timer;

namespace RoboSync
{
    internal class SyncModel : INotifyPropertyChanged
    {
        private List<Tuple<string, string, string>> commands = new List<Tuple<string, string, string>>();
        private Tuple<string, string, string>? command = null;
        private BackgroundWorker? worker;
        private Process? process = null;
        private long inSize = 0;
        private long outSize = 0;
        private long numError = 0;

        private int _status;
        public int Status
        {
            get { return _status; }
            set { _status = value; OnPropertyChanged(); }
        }

        private int _progress1;
        public int Progress1
        {
            get { return _progress1; }
            set { _progress1 = value; OnPropertyChanged(); }
        }

        private int _progress2;
        public int Progress2
        {
            get { return _progress2; }
            set { _progress2 = value; OnPropertyChanged(); }
        }

        private string _logLine;
        public string LogLine
        {
            get { return _logLine; }
            set { _logLine = value; OnPropertyChanged(); }
        }

        private string _readBytes;
        public string ReadBytes
        {
            get { return _readBytes; }
            set { _readBytes = value; OnPropertyChanged(); }
        }

        private string _writeBytes;
        public string WriteBytes
        {
            get { return _writeBytes; }
            set { _writeBytes = value; OnPropertyChanged(); }
        }

        private string _progressTime;
        public string ProgressTime
        {
            get { return _progressTime; }
            set { _progressTime = value; OnPropertyChanged(); }
        }

        public SyncModel()
        {
        }

        // Default INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        internal void Initialise()
        {
            Status = 0;
            Progress1 = 0;
            Progress2 = 0;
        }

        internal void DoSync(List<Tuple<string, string, string>> _commands)
        {
            commands = _commands;

            Progress1 = 0;
            Progress2 = 0;
            ReadBytes = "";
            WriteBytes = "";
            ProgressTime = "";

            worker = new BackgroundWorker();
            worker.ProgressChanged += new ProgressChangedEventHandler(ProgressChanged);
            worker.DoWork += new DoWorkEventHandler(DoWork);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(WorkerCompleted);
            worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = true;
            worker.RunWorkerAsync();
        }

        private void DoWork(object? sender, DoWorkEventArgs e)
        {
            if (null == worker) return;
            if (null == sender) return;
            int i = 0;
            numError = 0;
            foreach (var _command in commands)
            {
                command = _command;
                process = new Process();
                Progress1 = 0;
                Progress2 = (int)(100 * (i++ / (double)commands.Count));
                Status = 1;
                ProgressTime = command.Item1;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = "ROBOCOPY";
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        if (e.Data.StartsWith("ERROR")) numError++;
                        LogLine = e.Data;
                    }
                };

                inSize = Dir.GetSize(command.Item1);

                Timer timer1 = new Timer();
                timer1.Elapsed += new ElapsedEventHandler(DoTimer1);
                timer1.Interval = 1000 * Math.Min(60, Math.Max(5, inSize / 1024.0 / 1024.0 / 1024.0));
                timer1.Enabled = true;
                Timer timer2 = new Timer();
                timer2.Elapsed += new ElapsedEventHandler(DoTimer2);
                timer2.Interval = 5000;
                timer2.Enabled = true;

                if (worker.CancellationPending)
                {
                    timer1.Enabled = false;
                    timer2.Enabled = false;
                    return;
                }
                process.StartInfo.Arguments = "\"" + command.Item1 + "\" \"" + command.Item2 + "\" " + command.Item3;
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
                Progress1 = 100;
                Progress2 = (int)(100 * (i / (double)commands.Count));
                timer1.Enabled = false;
                timer2.Enabled = false;
            }
            command = null;
            LogLine = Environment.NewLine + (worker.CancellationPending ? "Aborted" : "Completed") + " with a total of " + numError + " errors detected";
        }

        private void DoTimer1(object? sender, ElapsedEventArgs e)
        {
            if (null == command) return;
            if (null == worker) return;
            if (process == null) return; // guard against null Process

            outSize = Dir.GetSize(command.Item2);

            // avoid divide-by-zero if inSize is 0
            int progress = inSize == 0 ? 0 : (int)(100 * (double)outSize / (double)inSize);
            worker.ReportProgress(progress);
        }

        private void DoTimer2(object? sender, ElapsedEventArgs e)
        {
            if (null == command) return;
            if (null == worker) return;
            if (process == null) return; // guard against null Process

            try
            {
                var runTime = (DateTime.Now - process.StartTime);
                var rates = IoSampler.SampleBytesAsync(process);
                var readB = rates.Item1;
                string readUnit = " kByte/s";
                if (readB > 1024)
                {
                    readB /= 1024;
                    readUnit = " MByte/s";
                }
                var writeB = rates.Item2;
                string writeUnit = " kByte/s";
                if (writeB > 1024)
                {
                    writeB /= 1024;
                    writeUnit = " MByte/s";
                }
                ReadBytes = "\nRead = " + readB.ToString("0.#") + readUnit;
                WriteBytes = "\nWrite = " + writeB.ToString("0.#") + writeUnit;
                long sec = (long)runTime.TotalSeconds;
                long min = sec / 60;
                long hour = min / 60;
                ProgressTime = command.Item1 + "\n" + hour.ToString("00") + ":" + (min % 60).ToString("00") +
                ":" + (sec % 60).ToString("00") + " (H:M:S)";
            }
            catch
            {
            }
        }

        private void ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            Progress1 = e.ProgressPercentage;
        }

        private void WorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            EndSync();
        }

        public void EndSync()
        {
            try
            {
                if (null != process && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception)
            {
            }
            process = null;
            Status = 0;
        }

        internal void AbortSync()
        {
            if (null == worker) return;
            worker.CancelAsync();
            EndSync();
        }
    }
}
