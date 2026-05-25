using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

public static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    private const int ATTACH_PARENT_PROCESS = -1;
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [STAThread]
    static async Task<int> Main(string[] args)
    {
        try
        {
            // 1. Detect if run from CLI (with arguments) or Desktop GUI (no arguments)
            if (args.Length > 0)
            {
                // Attach to the parent console output and redirect streams
                if (AttachConsole(ATTACH_PARENT_PROCESS))
                {
                    var stdOut = GetStdHandle(STD_OUTPUT_HANDLE);
                    var stdErr = GetStdHandle(STD_ERROR_HANDLE);
                    SetStdHandle(STD_OUTPUT_HANDLE, stdOut);
                    SetStdHandle(STD_ERROR_HANDLE, stdErr);

                    var writer = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true };
                    Console.SetOut(writer);
                    Console.SetError(writer);
                }
                
                // Execute in standard command-line mode
                return await RunCliModeAsync(args);
            }
            else
            {
                // Start the WinUI 3 Desktop GUI
                Microsoft.UI.Xaml.Application.Start((p) =>
                {
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    new ImgSeek.App();
                });
                return 0;
            }
        }
        catch (Exception ex)
        {
            // Write to a local crash log file
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                File.WriteAllText(logPath, $"CRITICAL CRASH ON STARTUP:\nTime: {DateTime.Now}\nMessage: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\nInner Exception:\n{ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}");
            }
            catch { }

            MessageBox(IntPtr.Zero, $"ImgSeek crashed on startup:\n{ex.Message}\n\nCheck crash_log.txt for details.", "ImgSeek Critical Error", 0x10);
            return 1;
        }
    }

    // ─── Command Line Mode ────────────────────────────────────────────────────
    private static async Task<int> RunCliModeAsync(string[] args)
    {
        string imageDir   = args.Length > 0 ? args[0] : "";
        string searchTerm = args.Length > 1 ? args[1] : "";
        string outputHtml = args.Length > 2 ? args[2] : "";

        if (string.IsNullOrWhiteSpace(imageDir) || string.IsNullOrWhiteSpace(searchTerm))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("\nUsage: OcrScanner <image-folder> <search-term> [output.html]");
            Console.ResetColor();
            return 1;
        }

        if (!Directory.Exists(imageDir))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("\nERROR: Directory not found: " + imageDir);
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n=== Image Name Searcher ===");
        Console.ResetColor();
        Console.WriteLine("  Folder : " + imageDir);
        Console.WriteLine("  Keyword: \"" + searchTerm + "\"");
        Console.WriteLine();

        var matches = new List<string>();

        // Create console progress reporter
        var progress = new Progress<ImgSeek.ScanProgress>(p =>
        {
            if (p.IsMatch && !string.IsNullOrEmpty(p.MatchPath))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n[{p.Current}/{p.Total}] MATCH: {p.CurrentFile}");
                Console.ResetColor();
            }
            else if (!string.IsNullOrEmpty(p.ErrorMessage))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n[{p.Current}/{p.Total}] SKIP ({p.ErrorMessage}): {p.CurrentFile}");
                Console.ResetColor();
            }
            else
            {
                // Print a dot to represent scanning progress
                Console.Write(".");
            }
        });

        try
        {
            matches = await ImgSeek.OcrScannerCore.RunScanAsync(imageDir, searchTerm, progress, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"\nERROR: Scan failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n=== Done: {matches.Count} match(es) out of search ===");
        Console.ResetColor();

        if (matches.Count == 0)
            return 0;

        // Determine output paths
        string baseName = ImgSeek.OcrScannerCore.SanitizeFileName(searchTerm) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        if (string.IsNullOrWhiteSpace(outputHtml))
            outputHtml = Path.Combine(Path.GetTempPath(), "ImgSeek_" + baseName + ".html");

        string copyBatPath = Path.Combine(Path.GetDirectoryName(outputHtml)!, "ImgSeek_" + baseName + "_CopyFiles.bat");
        string copyBatName = Path.GetFileName(copyBatPath);

        // Write HTML gallery and copy batch script
        File.WriteAllText(outputHtml, ImgSeek.OcrScannerCore.BuildHtml(matches, searchTerm, copyBatName), Encoding.UTF8);
        File.WriteAllText(copyBatPath, ImgSeek.OcrScannerCore.BuildCopyBat(matches, searchTerm), Encoding.UTF8);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Gallery  : " + outputHtml);
        Console.WriteLine("Copy tool: " + copyBatPath);
        Console.ResetColor();
        Console.WriteLine("\nTip: In the gallery click 'Copy All Photos', or run the _CopyFiles.bat directly.");

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = outputHtml,
                UseShellExecute = true
            });
        }
        catch { /* non-fatal */ }

        return 0;
    }
}
