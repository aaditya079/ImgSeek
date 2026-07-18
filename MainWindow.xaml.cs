using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace ImgSeek
{
    public partial class MainWindow : Window
    {
        private readonly List<string> _matches = new();
        private CancellationTokenSource? _cts;
        private string _activeFolder = "";
        private string _activeTerm  = "";

        public MainWindow()
        {
            InitializeComponent();
            
            // Set Window Icon Safely
            try
            {
                var iconUri = new Uri("pack://application:,,,/imgseek_icon.png", UriKind.RelativeOrAbsolute);
                Icon = BitmapFrame.Create(iconUri);
            }
            catch { }

            WireHints(FolderBox, FolderHintTb);
            WireHints(SearchBox, SearchHintTb);

            // Register drag & drop
            AllowDrop = true;
            DragOver += MainWindow_DragOver;
            Drop += MainWindow_Drop;

            // Load OCR Languages
            LoadOcrLanguages();
        }

        private void LoadOcrLanguages()
        {
            try
            {
                var languages = Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages;
                LanguageSelect.Items.Clear();
                LanguageSelect.Items.Add(new ComboBoxItem { Content = "Default (Auto)", Tag = "" });
                foreach (var lang in languages)
                {
                    LanguageSelect.Items.Add(new ComboBoxItem 
                    { 
                        Content = $"{lang.DisplayName} [{lang.LanguageTag}]", 
                        Tag = lang.LanguageTag 
                    });
                }
                LanguageSelect.SelectedIndex = 0;
            }
            catch
            {
                LanguageSelect.Items.Clear();
                LanguageSelect.Items.Add(new ComboBoxItem { Content = "Default (Auto)", Tag = "" });
                LanguageSelect.SelectedIndex = 0;
            }
        }

        // ── Overlay hints ────────────────────────────────────────────────────────
        private static void WireHints(TextBox tb, TextBlock hint)
        {
            void Refresh() => hint.Visibility = string.IsNullOrEmpty(tb.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            tb.TextChanged += (_, _) => Refresh();
            Refresh();
        }

        private string GetInput(TextBox tb) => tb.Text.Trim();

        // ── Browse ───────────────────────────────────────────────────────────────
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select folder to scan" };
            if (dlg.ShowDialog(this) == true)
            {
                if (string.IsNullOrWhiteSpace(FolderBox.Text))
                {
                    FolderBox.Text = dlg.FolderName;
                }
                else
                {
                    FolderBox.Text = FolderBox.Text.TrimEnd(';') + ";" + dlg.FolderName;
                }
            }
        }

        // ── Scan ─────────────────────────────────────────────────────────────────
        private async void Scan_Click(object sender, RoutedEventArgs e) => await RunScanAsync();
        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await RunScanAsync();
        }

        private async Task RunScanAsync()
        {
            string folder = GetInput(FolderBox);
            string term   = GetInput(SearchBox);

            var folders = folder.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (folders.Length == 0) { ShowMsg("⚠  Please enter a folder path to scan.", error: true); return; }
            foreach (var f in folders)
            {
                if (!Directory.Exists(f)) { ShowMsg($"⚠  Folder not found: {f}", error: true); return; }
            }
            if (string.IsNullOrWhiteSpace(term))     { ShowMsg("⚠  Please enter a search keyword.", error: true); return; }

            HideMsg();
            _matches.Clear();
            ResultsPanel.Children.Clear();
            _activeFolder = folder;
            _activeTerm   = term;

            EmptyState.Visibility    = Visibility.Collapsed;
            ResultsScroll.Visibility = Visibility.Visible;
            FooterBar.Visibility     = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            SetControls(scanning: true);

            ProgressBar.Value   = 0;
            ProgressPct.Text    = "0%";
            ProgressStats.Text  = "Reading directory…";
            ProgressFile.Text   = "";
            MatchStats.Text     = "";

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<ScanProgress>(p => Dispatcher.Invoke(() =>
            {
                if (token.IsCancellationRequested) return;
                ProgressFile.Text = p.CurrentFile;
                if (p.Total > 0)
                {
                    double pct = (double)p.Current / p.Total * 100;
                    if (pct > ProgressBar.Value)
                    {
                        ProgressBar.Value  = pct;
                        ProgressPct.Text   = $"{Math.Round(pct)}%";
                    }
                    ProgressStats.Text = $"Scanned {p.Current} / {p.Total}";
                }
                if (p.IsMatch && p.MatchPath != null)
                {
                    _matches.Add(p.MatchPath);
                    MatchStats.Text = $"✦ {_matches.Count} match{(_matches.Count == 1 ? "" : "es")}";
                    AddImageCard(p.MatchPath);
                }
            }));

            try
            {
                var options = new ScanOptions
                {
                    CaseSensitive = CaseSensitiveCheck.IsChecked == true,
                    UseRegex = RegexCheck.IsChecked == true,
                    LanguageTag = (LanguageSelect.SelectedItem as ComboBoxItem)?.Tag as string
                };
                var results = await Task.Run(() => OcrScannerCore.RunScanAsync(folder, term, options, progress, token), token);

                ProgressBar.Value  = 100;
                ProgressPct.Text   = "100%";
                ProgressFile.Text  = "✓ Scan complete";
                ProgressStats.Text = $"Finished — {folder}";

                if (results.Count == 0)
                {
                    ShowMsg($"ℹ  No images found containing \"{term}\".", error: false);
                    EmptyIcon.Text     = "🔎";
                    EmptyMsg.Text      = $"No matches for \"{term}\" — try a different keyword or folder.";
                    EmptyState.Visibility    = Visibility.Visible;
                    ResultsScroll.Visibility = Visibility.Collapsed;
                }
                else
                {
                    FooterBar.Visibility = Visibility.Visible;
                    FooterCount.Text = $"{results.Count} image{(results.Count == 1 ? "" : "s")} found";
                }
            }
            catch (OperationCanceledException)
            {
                ProgressFile.Text = "Cancelled by user.";
                ShowMsg("ℹ  Scan cancelled.", error: false);
                if (_matches.Count > 0) FooterBar.Visibility = Visibility.Visible;
                else { EmptyState.Visibility = Visibility.Visible; ResultsScroll.Visibility = Visibility.Collapsed; }
            }
            catch (Exception ex)
            {
                ShowMsg($"✕  Error: {ex.Message}", error: true);
                EmptyIcon.Text     = "⚠";
                EmptyMsg.Text      = "Scan failed. Check the folder path and try again.";
                EmptyState.Visibility    = Visibility.Visible;
                ResultsScroll.Visibility = Visibility.Collapsed;
            }
            finally
            {
                SetControls(scanning: false);
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void SetControls(bool scanning)
        {
            ScanBtn.IsEnabled   = !scanning;
            CancelBtn.IsEnabled =  scanning;
            FolderBox.IsEnabled = !scanning;
            SearchBox.IsEnabled = !scanning;
        }

        // ── Cancel ───────────────────────────────────────────────────────────────
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            CancelBtn.IsEnabled = false;
            ProgressFile.Text   = "Cancelling…";
        }

        // ── Image card ───────────────────────────────────────────────────────────
        private void AddImageCard(string path)
        {
            var card = new Border
            {
                Width = 205, Margin = new Thickness(7),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x14, 0x24)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                Opacity = 0
            };

            // Fade-in animation
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(250))) { EasingFunction = new CubicEase() };
            var slideIn = new DoubleAnimation(10, 0, new Duration(TimeSpan.FromMilliseconds(250))) { EasingFunction = new CubicEase() };
            var tt = new TranslateTransform();
            card.RenderTransform = tt;
            card.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            tt.BeginAnimation(TranslateTransform.YProperty, slideIn);

            // Hover glow
            var normalBorder = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
            var hoverBorder  = new SolidColorBrush(Color.FromArgb(0x55, 0x60, 0xA5, 0xFA));
            card.MouseEnter += (_, _) => card.BorderBrush = hoverBorder;
            card.MouseLeave += (_, _) => card.BorderBrush = normalBorder;

            var sp = new StackPanel();

            // Thumbnail
            var img = new Image { Height = 168, Stretch = Stretch.UniformToFill };
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.DecodePixelWidth = 210;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                img.Source = bmp;
            }
            catch { /* placeholder on failure */ }

            img.MouseLeftButtonUp += (_, _) => OpenFile(path);

            // Overlay: open icon on hover
            var overlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0x00, 0x00)),
                Opacity = 0, IsHitTestVisible = false
            };
            var openIcon = new TextBlock
            {
                Text = "🔗  Open", Foreground = Brushes.White,
                FontSize = 14, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            overlay.Child = openIcon;

            var imgGrid = new Grid { Height = 168 };
            imgGrid.Children.Add(img);
            imgGrid.Children.Add(overlay);
            imgGrid.MouseEnter += (_, _) => overlay.Opacity = 1;
            imgGrid.MouseLeave += (_, _) => overlay.Opacity = 0;
            imgGrid.Cursor = Cursors.Hand;
            imgGrid.MouseLeftButtonUp += (_, _) => OpenFile(path);
            sp.Children.Add(imgGrid);

            // Footer
            var foot = new DockPanel { Margin = new Thickness(10, 7, 10, 10) };
            var copyBtn = new Button
            {
                Content = "Copy", FontSize = 10, FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 4, 8, 4),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x29, 0x3B)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD)),
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            
            var copyNormalBg = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x29, 0x3B));
            var copyHoverBg  = new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x41, 0x55));
            copyBtn.MouseEnter += (_, _) => copyBtn.Background = copyHoverBg;
            copyBtn.MouseLeave += (_, _) => copyBtn.Background = copyNormalBg;

            copyBtn.Click += (_, _) => { Clipboard.SetText(path); ShowMsg("✓ Path copied to clipboard!", error: false); };

            var fname = new TextBlock
            {
                Text = Path.GetFileName(path),
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center, ToolTip = path
            };
            DockPanel.SetDock(copyBtn, Dock.Right);
            foot.Children.Add(copyBtn);
            foot.Children.Add(fname);
            sp.Children.Add(foot);

            card.Child = sp;
            ResultsPanel.Children.Add(card);
        }

        private static void OpenFile(string path)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true }); }
            catch { /* ignore */ }
        }

        // ── Footer actions ───────────────────────────────────────────────────────
        private void CopyPaths_Click(object sender, RoutedEventArgs e)
        {
            if (_matches.Count == 0) return;
            Clipboard.SetText(string.Join(Environment.NewLine, _matches));
            ShowMsg($"✓ Copied {_matches.Count} path(s) to clipboard.", error: false);
        }

        private void CopyPhotos_Click(object sender, RoutedEventArgs e)
        {
            if (_matches.Count == 0) return;
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select destination folder" };
            if (dlg.ShowDialog(this) != true) return;

            string dest = dlg.FolderName;
            Directory.CreateDirectory(dest);
            int copied = 0, failed = 0;
            foreach (var p in _matches)
            {
                try { File.Copy(p, Path.Combine(dest, Path.GetFileName(p)), overwrite: true); copied++; }
                catch { failed++; }
            }
            ShowMsg(failed == 0
                ? $"✓ Copied {copied} photo(s) to: {dest}"
                : $"⚠  Copied {copied}, failed to copy {failed}.", error: failed > 0);
        }

        private async void OpenGallery_Click(object sender, RoutedEventArgs e)
        {
            if (_matches.Count == 0) return;
            try
            {
                string baseName = OcrScannerCore.SanitizeFileName(_activeTerm) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string htmlPath = Path.Combine(Path.GetTempPath(), "ImgSeek_" + baseName + ".html");
                string batName  = "ImgSeek_" + baseName + "_CopyFiles.bat";
                string batPath  = Path.Combine(Path.GetTempPath(), batName);

                await File.WriteAllTextAsync(htmlPath, OcrScannerCore.BuildHtml(_matches, _activeTerm, batName), Encoding.UTF8);
                await File.WriteAllTextAsync(batPath,  OcrScannerCore.BuildCopyBat(_matches, _activeTerm),       Encoding.UTF8);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = htmlPath, UseShellExecute = true });
                ShowMsg("✓ HTML gallery opened in browser!", error: false);
            }
            catch (Exception ex) { ShowMsg($"✕  Gallery error: {ex.Message}", error: true); }
        }

        // ── Drag & Drop ──────────────────────────────────────────────────────────
        private void MainWindow_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var validFolders = new List<string>();
                foreach (var path in files)
                {
                    if (Directory.Exists(path))
                    {
                        if (!validFolders.Contains(path))
                            validFolders.Add(path);
                    }
                    else if (File.Exists(path))
                    {
                        string? dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir) && !validFolders.Contains(dir))
                        {
                            validFolders.Add(dir);
                        }
                    }
                }

                if (validFolders.Count > 0)
                {
                    string joined = string.Join(";", validFolders);
                    if (string.IsNullOrWhiteSpace(FolderBox.Text))
                    {
                        FolderBox.Text = joined;
                    }
                    else
                    {
                        FolderBox.Text = FolderBox.Text.TrimEnd(';') + ";" + joined;
                    }
                    ShowMsg($"📁 Loaded {validFolders.Count} folder(s) via drag & drop.", error: false);
                }
            }
        }

        // ── Export Matches ───────────────────────────────────────────────────────
        private void ExportMatches_Click(object sender, RoutedEventArgs e)
        {
            if (_matches.Count == 0) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export OCR Matches",
                Filter = "JSON Files (*.json)|*.json|CSV Files (*.csv)|*.csv",
                FileName = $"ImgSeek_Export_{OcrScannerCore.SanitizeFileName(_activeTerm)}"
            };

            if (dlg.ShowDialog(this) == true)
            {
                try
                {
                    string ext = Path.GetExtension(dlg.FileName).ToLower();
                    if (ext == ".json")
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("{");
                        sb.AppendLine($"  \"searchTerm\": \"{_activeTerm.Replace("\\", "\\\\").Replace("\"", "\\\"")}\",");
                        sb.AppendLine($"  \"folder\": \"{_activeFolder.Replace("\\", "\\\\").Replace("\"", "\\\"")}\",");
                        sb.AppendLine("  \"matches\": [");
                        for (int i = 0; i < _matches.Count; i++)
                        {
                            string comma = (i == _matches.Count - 1) ? "" : ",";
                            sb.AppendLine($"    \"{_matches[i].Replace("\\", "\\\\").Replace("\"", "\\\"")}\"{comma}");
                        }
                        sb.AppendLine("  ]");
                        sb.AppendLine("}");
                        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    }
                    else if (ext == ".csv")
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("Index,FileName,FullPath");
                        for (int i = 0; i < _matches.Count; i++)
                        {
                            string csvEscapedPath = _matches[i].Replace("\"", "\"\"");
                            string name = Path.GetFileName(_matches[i]).Replace("\"", "\"\"");
                            sb.AppendLine($"{i + 1},\"{name}\",\"{csvEscapedPath}\"");
                        }
                        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    }

                    ShowMsg($"✓ Exported {_matches.Count} matches to: {Path.GetFileName(dlg.FileName)}", error: false);
                }
                catch (Exception ex)
                {
                    ShowMsg($"✕ Export error: {ex.Message}", error: true);
                }
            }
        }

        // ── Message bar ──────────────────────────────────────────────────────────
        private void ShowMsg(string msg, bool error)
        {
            MsgText.Text      = msg;
            MsgBar.Background = error
                ? new SolidColorBrush(Color.FromRgb(0x3B, 0x08, 0x08))
                : new SolidColorBrush(Color.FromRgb(0x06, 0x24, 0x18));
            MsgText.Foreground = error
                ? new SolidColorBrush(Color.FromRgb(0xFB, 0xB6, 0xBE))
                : new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC));
            MsgBar.Visibility = Visibility.Visible;
        }
        private void HideMsg() => MsgBar.Visibility = Visibility.Collapsed;
        private void DismissMsg_Click(object sender, RoutedEventArgs e) => HideMsg();
    }
}
