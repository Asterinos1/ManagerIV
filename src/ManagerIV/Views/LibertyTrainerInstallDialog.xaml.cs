using System.IO;
using System.Windows;
using ManagerIV.Core;
using Microsoft.Win32;

namespace ManagerIV.Views;

/// <summary>
/// Nonblocking dialog that coordinates waiting for user's Liberty's Legacy download and manual ZIP selection fallback.
/// </summary>
public partial class LibertyTrainerInstallDialog : Window
{
    private readonly ILibertyTrainerDownloadMonitor _downloadMonitor;
    private readonly string _downloadsDir;
    private readonly DateTime _startTime;
    private readonly CancellationTokenSource _cts = new();

    public string? CandidateZipPath { get; private set; }

    public LibertyTrainerInstallDialog(
        ILibertyTrainerDownloadMonitor downloadMonitor,
        string downloadsDir,
        DateTime startTime)
    {
        InitializeComponent();
        _downloadMonitor = downloadMonitor ?? throw new ArgumentNullException(nameof(downloadMonitor));
        _downloadsDir = string.IsNullOrWhiteSpace(downloadsDir) ? _downloadMonitor.GetDownloadsDirectory() : downloadsDir;
        _startTime = startTime;

        DownloadsPathText.Text = _downloadsDir;

        Loaded += LibertyTrainerInstallDialog_Loaded;
        Closing += LibertyTrainerInstallDialog_Closing;
    }

    private async void LibertyTrainerInstallDialog_Loaded(object sender, RoutedEventArgs e)
    {
        var progress = new Progress<string>(msg =>
        {
            Dispatcher.Invoke(() =>
            {
                StatusMessageText.Text = msg;
            });
        });

        try
        {
            string? result = await _downloadMonitor.WaitForCandidateArchiveAsync(_downloadsDir, _startTime, _cts.Token, progress);
            if (!string.IsNullOrEmpty(result) && File.Exists(result))
            {
                CandidateZipPath = result;
                DialogResult = true;
                Close();
            }
        }
        catch (OperationCanceledException)
        {
            // User cancelled
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                StatusMessageText.Text = $"Monitoring error: {ex.Message}. You can use 'Select ZIP...' below.";
            });
        }
    }

    private void SelectZipButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Liberty's Legacy ZIP Package",
            Filter = "ZIP Archives (*.zip)|*.zip|All Files (*.*)|*.*",
            InitialDirectory = Directory.Exists(_downloadsDir) ? _downloadsDir : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog(this) == true)
        {
            CandidateZipPath = dialog.FileName;
            _cts.Cancel();
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        DialogResult = false;
        Close();
    }

    private void LibertyTrainerInstallDialog_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
    }
}
