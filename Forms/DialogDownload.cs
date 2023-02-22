using System;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace GenieClient
{
    public delegate void DownloadProgressChangedHandler(long? totalFileSize, long totalBytesDownloaded, double? progressPercentage);

    public delegate void DownloadFileCompletedHandler();

    public class HttpClientWithProgress : HttpClient
    {
        private string _downloadUrl;
        private string _destinationFilePath;
        private readonly CancellationTokenSource _cancelSource = new CancellationTokenSource();

        public event DownloadProgressChangedHandler DownloadProgressChanged;
        public event DownloadFileCompletedHandler DownloadFileCompleted;

        public HttpClientWithProgress()
        {
        }

        public HttpClientWithProgress(string downloadUrl, string destinationFilePath)
        {
            _downloadUrl = downloadUrl;
            _destinationFilePath = destinationFilePath;
        }

        public async Task DownloadFileAsync(string downloadUrl, string destinationFilePath)
        {
            _downloadUrl = downloadUrl;
            _destinationFilePath = destinationFilePath;
            using var response = await GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead, _cancelSource.Token);
            await DownloadFileFromHttpResponseMessage(response);
        }

        public void CancelAsync()
        {
            _cancelSource.Cancel();
        }

        private async Task DownloadFileFromHttpResponseMessage(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            long? totalBytes = response.Content.Headers.ContentLength;
            using (var contentStream = await response.Content.ReadAsStreamAsync())
                await ProcessContentStream(totalBytes, contentStream);
        }

        private async Task ProcessContentStream(long? totalDownloadSize, Stream contentStream)
        {
            long totalBytesRead = 0L;
            long readCount = 0L;
            byte[] buffer = new byte[8192];
            bool isMoreToRead = true;

            await using (FileStream fileStream = new FileStream(_destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                do
                {
                    int bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, bytesRead);

                    totalBytesRead += bytesRead;
                    readCount += 1;

                    if (readCount % 10 == 0)
                        TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                }
                while (isMoreToRead);
            }
            TriggerProgressChanged(totalDownloadSize, totalBytesRead);
            DownloadFileCompleted?.Invoke();
        }

        private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead)
        {
            if (DownloadProgressChanged == null)
                return;

            double? progressPercentage = null;
            if (totalDownloadSize.HasValue)
                progressPercentage = Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 2);

            DownloadProgressChanged(totalDownloadSize, totalBytesRead, progressPercentage);
        }
    }

    public partial class DialogDownload
    {
        public DialogDownload()
        {
            InitializeComponent();
        }

        private readonly HttpClientWithProgress _myWebClient = new HttpClientWithProgress();
        
        private DateTime _myStartTime;
        private bool _downloadCancelled;
        private string _strUpdateString = string.Empty;

        public object UpdateToString
        {
            get
            {
                return _strUpdateString;
            }

            set
            {
                _strUpdateString = Conversions.ToString(value);
            }
        }

        public bool MajorUpdate = false;

        private void OK_Button_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Cancel_Button_Click(object sender, EventArgs e)
        {
            _myWebClient.CancelAsync();
            DialogResult = DialogResult.Cancel;
            Close();
        }

        public void DownloadFile(string url, string strFileName)
        {
            _myWebClient.DownloadFileAsync(url, LocalDirectory.Path + @"\" + strFileName);
            _myStartTime = DateTime.Now;
            LabelFile.Text =  "Downloading to: " + strFileName;
            LabelFile.Tag =   "Downloading to: " + strFileName;
            LabelSpeed.Text = "0 of 0 kb received. (0 kb/s)";
        }

        private void UpdateDl(long? totalFileSize, long totalBytesDownloaded, double? progressPercentage)
        {
            var argbar = ProgressBar1;
            int percent = progressPercentage.HasValue ? (int)progressPercentage.Value : 0;
            UpdateProgressBar(argbar, percent);
            var arglabel = LabelFile;
            string argtext = $" ({percent}%)";
            UpdateLabelText(arglabel, argtext);
            var arglabel1 = LabelSpeed;
            string argtext1 = Math.Round(totalBytesDownloaded / (double)1024, 0) + " of " + (totalFileSize.HasValue ? Math.Round(totalFileSize.Value / (double)1024, 0) : "unknown") + " kb received. (" + CurrentSpeed(Conversions.ToInteger(totalBytesDownloaded), _myStartTime, DateTime.Now) + ")";
            UpdateLabelText(arglabel1, argtext1);
        }

        private bool _bRelaunch = false;

        private void UpdateDlFinished()
        {
            if (_downloadCancelled == true)
            {
                Label arglabel = LabelSpeed;
                string argtext = "Aborted.";
                UpdateLabelText(arglabel, argtext);
                ProgressBar argbar = ProgressBar1;
                int argpercent = 0;
                UpdateProgressBar(argbar, argpercent);
            }
            else
            {
                Label arglabel1 = LabelSpeed;
                string argtext1 = "Finished.";
                UpdateLabelText(arglabel1, argtext1);
                OK_Button.Enabled = true;
                ButtonDownload.Enabled = false;
                _bRelaunch = true;
            }

            _downloadCancelled = false;
        }

        public string CurrentSpeed(int bytesReceived, DateTime startTime, DateTime endTime)
        {
            var span = endTime - startTime;
            double duration = span.TotalMilliseconds;
            long speed = Conversions.ToLong(Math.Round(bytesReceived / duration, 0));
            return Math.Round(speed * 1000 / (double)1024, 0) + " kb/s";
        }

        // Private strExeName As String = String.Empty

        private void DialogDownload_Load(object sender, EventArgs e)
        {
            ButtonDownload.Tag = true;
            LabelFile.Text = "";
            LabelSpeed.Text = "";
            LabelNewVersion.Text = "An update is available for Genie!" + System.Environment.NewLine + System.Environment.NewLine + "Your Version: " + My.MyProject.Application.Info.Version.ToString() + System.Environment.NewLine + "Update Version: " + _strUpdateString + System.Environment.NewLine;
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(ButtonDownload.Tag, true, false)))
            {
                ButtonDownload.Tag = false;
                ButtonDownload.Text = "Cancel";
                DownloadFile("http://d23oq4rykk2wza.cloudfront.net/update.exe", "update.exe");
            }
            else
            {
                ButtonDownload.Tag = true;
                _downloadCancelled = true;
                _myWebClient.CancelAsync();
                ButtonDownload.Text = "Download";
            }
        }

        public delegate void UpdateLabelTextDelegate(Label label, string text);

        private void UpdateLabelText(Label label, string text)
        {
            if (InvokeRequired == true)
            {
                var parameters = new object[] { label, text };
                Invoke(new UpdateLabelTextDelegate(UpdateLabelTextMethod), parameters);
            }
            else
            {
                UpdateLabelTextMethod(label, text);
            }
        }

        private void UpdateLabelTextMethod(Label label, string text)
        {
            if (Information.IsNothing(label))
            {
                return;
            }

            if (label.IsDisposed == true)
            {
                return;
            }

            label.Text = Conversions.ToString(label.Tag + text);
        }

        public delegate void UpdateProgressBarDelegate(ProgressBar bar, int percent);

        private void UpdateProgressBar(ProgressBar bar, int percent)
        {
            if (InvokeRequired == true)
            {
                var parameters = new object[] { bar, percent };
                Invoke(new UpdateLabelTextDelegate(UpdateLabelTextMethod), parameters);
            }
            else
            {
                UpdateProgressBarMethod(bar, percent);
            }
        }

        private void UpdateProgressBarMethod(ProgressBar bar, int percent)
        {
            if (Information.IsNothing(bar))
            {
                return;
            }

            if (bar.IsDisposed == true)
            {
                return;
            }

            bar.Value = percent;
        }

        private void DialogDownload_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (MajorUpdate == true)
                {
                    if (!_bRelaunch)
                    {
                        Interaction.MsgBox("This is a major update of Genie. You will need to update to play.");
                        e.Cancel = true;
                    }
                }
            }
        }
    }
}