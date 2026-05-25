using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace ImgSeek
{
    public class MatchedImageItem
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string ImageUri { get; set; } = "";
    }

    public sealed partial class MainWindow : Window
    {
        private ObservableCollection<MatchedImageItem> _matchedImages = new();
        private List<string> _rawMatches = new();
        private CancellationTokenSource? _cts;
        private string _activeSearchFolder = "";
        private string _activeSearchTerm = "";

        public MainWindow()
        {
            this.InitializeComponent();
            ResultsGridView.ItemsSource = _matchedImages;

            // Set default window size programmatically for unpackaged WinUI 3 Window
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(1050, 780));
        }

        // ─── Browse Folder Event ─────────────────────────────────────────────────
        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            HideMessage();
            try
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
                folderPicker.FileTypeFilter.Add("*");

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    FolderTextBox.Text = folder.Path;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to open folder picker: {ex.Message}");
            }
        }

        // ─── Start Scan Event ────────────────────────────────────────────────────
        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            await StartSearchAsync();
        }

        private async void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                await StartSearchAsync();
            }
        }

        private async Task StartSearchAsync()
        {
            string folder = FolderTextBox.Text.Trim();
            string keyword = SearchTextBox.Text.Trim();

            // 1. Inputs validation
            if (string.IsNullOrWhiteSpace(folder))
            {
                ShowError("Please specify an images directory folder to scan.");
                return;
            }

            if (!Directory.Exists(folder))
            {
                ShowError($"The specified folder does not exist: {folder}");
                return;
            }

            if (string.IsNullOrWhiteSpace(keyword))
            {
                ShowError("Please enter a search keyword.");
                return;
            }

            // 2. Prepare state for search
            HideMessage();
            _matchedImages.Clear();
            _rawMatches.Clear();

            _activeSearchFolder = folder;
            _activeSearchTerm = keyword;

            // UI Adjustments
            EmptyStateStackPanel.Visibility = Visibility.Collapsed;
            ResultsGridView.Visibility = Visibility.Visible;
            ActionFooterBorder.Visibility = Visibility.Collapsed;
            ProgressBorder.Visibility = Visibility.Visible;

            FolderTextBox.IsEnabled = false;
            BrowseButton.IsEnabled = false;
            SearchTextBox.IsEnabled = false;
            ScanButton.IsEnabled = false;
            CancelButton.IsEnabled = true;

            ScanProgressBar.Value = 0;
            ProgressPercentTextBlock.Text = "0%";
            ProgressStatsTextBlock.Text = "Reading directory...";
            MatchesStatsTextBlock.Text = "Found 0 matches";
            ProgressFileTextBlock.Text = "";

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // 3. Define progress reporter
            var progressReporter = new Progress<ScanProgress>(p =>
            {
                // Enqueue on dispatcher to update UI securely
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested) return;

                    ProgressFileTextBlock.Text = p.CurrentFile;
                    
                    if (p.Total > 0)
                    {
                        double percent = ((double)p.Current / p.Total) * 100;
                        ScanProgressBar.Value = percent;
                        ProgressPercentTextBlock.Text = $"{Math.Round(percent)}%";
                        ProgressStatsTextBlock.Text = $"Scanned {p.Current} of {p.Total} images";
                    }

                    if (p.IsMatch && !string.IsNullOrEmpty(p.MatchPath))
                    {
                        _rawMatches.Add(p.MatchPath);
                        _matchedImages.Add(new MatchedImageItem
                        {
                            FilePath = p.MatchPath,
                            FileName = Path.GetFileName(p.MatchPath),
                            ImageUri = new Uri(p.MatchPath).AbsoluteUri
                        });
                        MatchesStatsTextBlock.Text = $"Found {_matchedImages.Count} match(es)";
                    }

                    if (!string.IsNullOrEmpty(p.ErrorMessage))
                    {
                        // Minor warning log, don't halt scan
                        System.Diagnostics.Debug.WriteLine($"Skipped file error: {p.ErrorMessage}");
                    }
                });
            });

            // 4. Run native OCR scanning in thread pool
            try
            {
                List<string> results = await Task.Run(async () =>
                    await OcrScannerCore.RunScanAsync(folder, keyword, progressReporter, token)
                , token);

                // Success completion
                ProgressFileTextBlock.Text = "Scan completed successfully!";
                ProgressPercentTextBlock.Text = "100%";
                ScanProgressBar.Value = 100;

                if (results.Count == 0)
                {
                    ShowInfo($"Search complete. No images containing \"{keyword}\" were found.");
                    EmptyStateStackPanel.Visibility = Visibility.Visible;
                    ResultsGridView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ActionFooterBorder.Visibility = Visibility.Visible;
                }
            }
            catch (OperationCanceledException)
            {
                ProgressFileTextBlock.Text = "Scan cancelled by user.";
                ShowInfo("Search cancelled successfully.");
                if (_matchedImages.Count == 0)
                {
                    EmptyStateStackPanel.Visibility = Visibility.Visible;
                    ResultsGridView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ActionFooterBorder.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                ShowError($"An error occurred during scanning: {ex.Message}");
                ProgressFileTextBlock.Text = "Scan halted due to an error.";
                EmptyStateStackPanel.Visibility = Visibility.Visible;
                ResultsGridView.Visibility = Visibility.Collapsed;
            }
            finally
            {
                // Reset controls
                FolderTextBox.IsEnabled = true;
                BrowseButton.IsEnabled = true;
                SearchTextBox.IsEnabled = true;
                ScanButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        // ─── Cancel Event ────────────────────────────────────────────────────────
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            CancelButton.IsEnabled = false;
            ProgressFileTextBlock.Text = "Cancelling scan...";
        }

        // ─── Single Item Actions ─────────────────────────────────────────────────
        private async void OpenImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is MatchedImageItem item)
            {
                try
                {
                    var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(item.FilePath);
                    await Launcher.LaunchFileAsync(file);
                }
                catch (Exception ex)
                {
                    ShowError($"Could not open image file: {ex.Message}");
                }
            }
        }

        private void CopyPathButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is MatchedImageItem item)
            {
                try
                {
                    var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dp.SetText(item.FilePath);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                    
                    // Show a quick visual success info
                    ShowInfo($"Copied path to clipboard: {item.FileName}", Severity.Success);
                }
                catch (Exception ex)
                {
                    ShowError($"Clipboard copy failed: {ex.Message}");
                }
            }
        }

        // ─── Bulk Action Center ──────────────────────────────────────────────────
        private void CopyPathsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_rawMatches.Count == 0) return;
            try
            {
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(string.Join(Environment.NewLine, _rawMatches));
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                ShowInfo($"Copied all {_rawMatches.Count} paths to clipboard!", Severity.Success);
            }
            catch (Exception ex)
            {
                ShowError($"Clipboard bulk copy failed: {ex.Message}");
            }
        }

        private async void CopyPhotosButton_Click(object sender, RoutedEventArgs e)
        {
            if (_rawMatches.Count == 0) return;

            try
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
                folderPicker.FileTypeFilter.Add("*");

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    string dest = folder.Path;
                    if (!Directory.Exists(dest))
                    {
                        Directory.CreateDirectory(dest);
                    }

                    int copied = 0;
                    int failed = 0;

                    await Task.Run(() =>
                    {
                        foreach (var path in _rawMatches)
                        {
                            try
                            {
                                string name = Path.GetFileName(path);
                                string destPath = Path.Combine(dest, name);
                                File.Copy(path, destPath, overwrite: true);
                                copied++;
                            }
                            catch
                            {
                                failed++;
                            }
                        }
                    });

                    if (failed == 0)
                    {
                        ShowInfo($"Successfully copied all {copied} photos to {dest}!", Severity.Success);
                    }
                    else
                    {
                        ShowInfo($"Copied {copied} photos to {dest}. Failed to copy {failed} photos due to file access/lock errors.", Severity.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Bulk photo copy failed: {ex.Message}");
            }
        }

        private async void OpenGalleryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_rawMatches.Count == 0) return;

            try
            {
                string baseName = OcrScannerCore.SanitizeFileName(_activeSearchTerm) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string outputHtml = Path.Combine(Path.GetTempPath(), "ImgSeek_" + baseName + ".html");
                string copyBatName = "ImgSeek_" + baseName + "_CopyFiles.bat";
                string copyBatPath = Path.Combine(Path.GetTempPath(), copyBatName);

                // Build templates
                string htmlContent = OcrScannerCore.BuildHtml(_rawMatches, _activeSearchTerm, copyBatName);
                string batContent = OcrScannerCore.BuildCopyBat(_rawMatches, _activeSearchTerm);

                // Write files
                await File.WriteAllTextAsync(outputHtml, htmlContent, Encoding.UTF8);
                await File.WriteAllTextAsync(copyBatPath, batContent, Encoding.UTF8);

                // Open HTML in default browser
                await Launcher.LaunchUriAsync(new Uri(outputHtml));
                ShowInfo("HTML Gallery launched in browser!", Severity.Success);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to generate HTML gallery: {ex.Message}");
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────
        private void ShowError(string msg)
        {
            MessageInfoBar.Message = msg;
            MessageInfoBar.Severity = InfoBarSeverity.Error;
            MessageInfoBar.Title = "Error Occurred";
            MessageInfoBar.IsOpen = true;
        }

        private void ShowInfo(string msg, Severity sev = Severity.Informational)
        {
            MessageInfoBar.Message = msg;
            MessageInfoBar.Title = sev switch
            {
                Severity.Success => "Success",
                Severity.Warning => "Warning",
                _ => "Notice"
            };
            MessageInfoBar.Severity = sev switch
            {
                Severity.Success => InfoBarSeverity.Success,
                Severity.Warning => InfoBarSeverity.Warning,
                _ => InfoBarSeverity.Informational
            };
            MessageInfoBar.IsOpen = true;
        }

        private void HideMessage()
        {
            MessageInfoBar.IsOpen = false;
        }
    }

    public enum Severity
    {
        Informational,
        Success,
        Warning
    }
}
