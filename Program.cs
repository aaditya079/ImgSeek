using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImgSeek
{
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

        [STAThread]
        public static void Main(string[] args)
        {
            string tempLogPath = Path.Combine(Path.GetTempPath(), "ImgSeek_Program_Log.txt");
            try
            {
                File.AppendAllText(tempLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Program started with args: {string.Join(" ", args)}\n");
            }
            catch { }

            if (args.Length > 0)
            {
                AttachParentConsole();
                int exitCode = RunCliModeAsync(args).GetAwaiter().GetResult();
                try
                {
                    File.AppendAllText(tempLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] CLI mode finished with exit code {exitCode}\n");
                }
                catch { }
                Environment.Exit(exitCode);
            }
            else
            {
                try
                {
                    File.AppendAllText(tempLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Launching WPF application...\n");
                }
                catch { }
                var app = new App();
                app.Run();
            }
        }

        private static void AttachParentConsole()
        {
            try
            {
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
            }
            catch
            {
                // Ignore console attachment errors
            }
        }

        private static async Task<int> RunCliModeAsync(string[] args)
        {
            string imageDir = "";
            string searchTerm = "";
            string outputHtml = "";

            var options = new ScanOptions();
            var positionalArgs = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("--case-sensitive", StringComparison.OrdinalIgnoreCase) || arg.Equals("-c", StringComparison.OrdinalIgnoreCase))
                {
                    options.CaseSensitive = true;
                }
                else if (arg.Equals("--regex", StringComparison.OrdinalIgnoreCase) || arg.Equals("-r", StringComparison.OrdinalIgnoreCase))
                {
                    options.UseRegex = true;
                }
                else if ((arg.Equals("--lang", StringComparison.OrdinalIgnoreCase) || arg.Equals("-l", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    options.LanguageTag = args[++i];
                }
                else if ((arg.Equals("--threads", StringComparison.OrdinalIgnoreCase) || arg.Equals("-t", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    if (int.TryParse(args[++i], out int threads))
                        options.MaxDegreeOfParallelism = threads;
                }
                else
                {
                    positionalArgs.Add(arg);
                }
            }

            if (positionalArgs.Count > 0) imageDir = positionalArgs[0];
            if (positionalArgs.Count > 1) searchTerm = positionalArgs[1];
            if (positionalArgs.Count > 2) outputHtml = positionalArgs[2];

            if (string.IsNullOrWhiteSpace(imageDir) || string.IsNullOrWhiteSpace(searchTerm))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("\nUsage: ImgSeek <image-folder> <search-term> [output.html] [options]");
                Console.Error.WriteLine("\nOptions:");
                Console.Error.WriteLine("  -c, --case-sensitive   Enable case-sensitive keyword matching");
                Console.Error.WriteLine("  -r, --regex            Search using Regular Expressions");
                Console.Error.WriteLine("  -l, --lang <tag>       OCR language tag (e.g. en-US, de-DE)");
                Console.Error.WriteLine("  -t, --threads <num>    Maximum concurrent OCR worker threads");
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
            Console.WriteLine($"  Options: CaseSensitive={options.CaseSensitive}, UseRegex={options.UseRegex}, Lang={options.LanguageTag ?? "Auto"}, Threads={options.MaxDegreeOfParallelism}");
            Console.WriteLine();

            var matches = new List<string>();

            // Create console progress reporter
            var progress = new Progress<ScanProgress>(p =>
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
                matches = await OcrScannerCore.RunScanAsync(imageDir, searchTerm, options, progress, CancellationToken.None);
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
            string baseName = OcrScannerCore.SanitizeFileName(searchTerm) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            if (string.IsNullOrWhiteSpace(outputHtml))
                outputHtml = Path.Combine(Path.GetTempPath(), "ImgSeek_" + baseName + ".html");

            string copyBatPath = Path.Combine(Path.GetDirectoryName(outputHtml)!, "ImgSeek_" + baseName + "_CopyFiles.bat");
            string copyBatName = Path.GetFileName(copyBatPath);

            // Write HTML gallery and copy batch script
            try
            {
                File.WriteAllText(outputHtml, OcrScannerCore.BuildHtml(matches, searchTerm, copyBatName), Encoding.UTF8);
                File.WriteAllText(copyBatPath, OcrScannerCore.BuildCopyBat(matches, searchTerm), Encoding.UTF8);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Gallery  : " + outputHtml);
                Console.WriteLine("Copy tool: " + copyBatPath);
                Console.ResetColor();
                Console.WriteLine("\nTip: In the gallery click 'Copy All Photos', or run the _CopyFiles.bat directly.");

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = outputHtml,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"\nERROR writing outputs: {ex.Message}");
                Console.ResetColor();
                return 1;
            }

            return 0;
        }
    }
}
