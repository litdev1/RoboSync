using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Timers;
using Timer = System.Timers.Timer;

namespace RoboSync
{
    public class SyncModel : INotifyPropertyChanged
    {
        private List<Tuple<string, string, string>> commands = new List<Tuple<string, string, string>>();
        private Tuple<string, string, string>? command = null;
        private BackgroundWorker? worker;
        private Process? process = null;
        private double inSize = 0;
        private double outSize = 0;
        private long numError = 0;
        private string estimate = "";

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
        public void Initialise()
        {
            Status = 0;
            Progress1 = 0;
            Progress2 = 0;
        }

        public void DoSync(List<Tuple<string, string, string>> _commands)
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
                Progress1 = 0;
                Progress2 = (int)(100 * (i++ / (double)commands.Count));
                Status = 1;
                ProgressTime = command.Item1;

                //First call get number of bytes that will be copied
                estimate = "";
                process = new Process();
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = "ROBOCOPY";
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string[] words = e.Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (words[0] == "Bytes")
                        {
                            inSize = 0;
                            double.TryParse(words[4], out inSize);
                            switch (words[5])
                            {
                                case "g":
                                    estimate = "INFO : Estimated " + inSize.ToString("0.0") + " GB to be copied";
                                    inSize *= 1024 * 1024 * 1024;
                                    break;
                                case "m":
                                    estimate = "INFO : Estimated " + inSize.ToString("0.0") + " MB to be copied";
                                    inSize *= 1024 * 1024;
                                    break;
                                case "k":
                                    estimate = "INFO : Estimated " + inSize.ToString("0.0") + " kB to be copied";
                                    inSize *= 1024;
                                    break;
                                default:
                                    estimate = "INFO : Estimated " + inSize.ToString("0.0") + " B to be copied";
                                    break;
                            }
                        }
                    }
                };
                process.StartInfo.Arguments = "\"" + command.Item1 + "\" \"" + command.Item2 + "\" " + command.Item3 + " /L";
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();

                //Reporting timer
                Timer timer = new Timer();
                timer.Elapsed += new ElapsedEventHandler(DoTimer);
                timer.Interval = 5000;
                timer.Enabled = true;

                if (worker.CancellationPending)
                {
                    timer.Enabled = false;
                    return;
                }

                //Now do the copying
                process = new Process();
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
                process.StartInfo.Arguments = "\"" + command.Item1 + "\" \"" + command.Item2 + "\" " + command.Item3;
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
                timer.Enabled = false;
            }
            command = null;
            if (worker.CancellationPending)
            {
                LogLine = Environment.NewLine + "INFO : Aborted with a total of " + numError + " errors detected";
            }
            else
            {
                LogLine = Environment.NewLine + "INFO : Completed with a total of " + numError + " errors detected";
                Progress1 = 100;
                Progress2 = (int)(100 * (i / (double)commands.Count));
            }
        }

        private void DoTimer(object? sender, ElapsedEventArgs e)
        {
            if (null == command) return;
            if (null == worker) return;
            if (process == null) return; // guard against null Process

            try
            {
                if (estimate != "")
                {
                    LogLine = Environment.NewLine + estimate + Environment.NewLine;
                    estimate = "";
                }
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
                ReadBytes = "\nRead : " + rates.Item3.ToString("0") + " MB\n(" + readB.ToString("0.0") + readUnit + ")";
                WriteBytes = "\nWrite : " + rates.Item4.ToString("0") + " MB\n(" +writeB.ToString("0.0") + writeUnit + ")";
                long sec = (long)runTime.TotalSeconds;
                long min = sec / 60;
                long hour = min / 60;
                ProgressTime = command.Item1 + "\n\n" + hour.ToString("00") + ":" + (min % 60).ToString("00") +
                ":" + (sec % 60).ToString("00") + " (H:M:S)";
                outSize = Dir.GetSize(command.Item2);

                // avoid divide-by-zero if inSize is 0
                outSize = rates.Item4 * 1024 * 1024;
                int progress = inSize == 0 ? 0 : (int)(100 * outSize / inSize);
                worker.ReportProgress(progress);
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

        public void AbortSync()
        {
            if (null == worker) return;
            worker.CancelAsync();
            EndSync();
        }
    }
}
