using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Parse args
        string imageDir  = args.Length > 0 ? args[0] : "";
        string searchTerm = args.Length > 1 ? args[1] : "";
        string outputHtml = args.Length > 2 ? args[2] : "";

        if (string.IsNullOrWhiteSpace(imageDir) || string.IsNullOrWhiteSpace(searchTerm))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Usage: OcrScanner <image-folder> <search-term> [output.html]");
            Console.ResetColor();
            return 1;
        }

        if (!Directory.Exists(imageDir))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"ERROR: Directory not found: {imageDir}");
            Console.ResetColor();
            return 1;
        }

        string termLower = searchTerm.ToLower();

        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };

        var allImages = new List<string>();
        foreach (var file in Directory.GetFiles(imageDir, "*.*", SearchOption.AllDirectories))
            if (extensions.Contains(Path.GetExtension(file)))
                allImages.Add(file);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"=== Image Name Searcher ===");
        Console.ResetColor();
        Console.WriteLine($"  Folder : {imageDir}");
        Console.WriteLine($"  Keyword: \"{searchTerm}\"");
        Console.WriteLine($"  Images : {allImages.Count}");
        Console.WriteLine();

        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("ERROR: Could not create Windows OCR engine.");
            Console.ResetColor();
            return 1;
        }

        var matches = new List<string>();
        int i = 0;

        foreach (var imgPath in allImages)
        {
            i++;
            string name = Path.GetFileName(imgPath);
            Console.Write($"[{i,4}/{allImages.Count}] {name,-60}");

            try
            {
                // Use full absolute path for StorageFile
                string fullPath = Path.GetFullPath(imgPath);
                var storageFile = await StorageFile.GetFileFromPathAsync(fullPath);
                using var stream = await storageFile.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var bitmap  = await decoder.GetSoftwareBitmapAsync();
                var result  = await engine.RecognizeAsync(bitmap);
                string text = result.Text.ToLower();

                if (text.Contains(termLower))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("MATCH");
                    Console.ResetColor();
                    matches.Add(fullPath);
                }
                else
                {
                    Console.WriteLine(".");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"SKIP ({ex.Message})");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"=== Done: {matches.Count} match(es) found out of {allImages.Count} images ===");
        Console.ResetColor();

        if (matches.Count == 0)
            return 0;

        // Generate HTML gallery
        if (string.IsNullOrWhiteSpace(outputHtml))
            outputHtml = Path.Combine(Path.GetTempPath(), $"ImgSeek_{searchTerm}_{DateTime.Now:yyyyMMdd_HHmmss}.html");

        string html = BuildHtml(matches, searchTerm);
        File.WriteAllText(outputHtml, html, Encoding.UTF8);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Gallery saved: {outputHtml}");
        Console.ResetColor();

        // Open in default browser
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

    static string BuildHtml(List<string> matches, string searchTerm)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine($"<title>ImgSeek — \"{searchTerm}\"</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:sans-serif;background:#111;color:#eee;margin:0;padding:20px}");
        sb.AppendLine("h1{color:#7df}");
        sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:12px;margin-top:20px}");
        sb.AppendLine(".card{background:#222;border-radius:8px;overflow:hidden;cursor:pointer;transition:transform .2s}");
        sb.AppendLine(".card:hover{transform:scale(1.03)}");
        sb.AppendLine(".card img{width:100%;height:180px;object-fit:cover}");
        sb.AppendLine(".card p{padding:6px 8px;margin:0;font-size:12px;word-break:break-all;color:#aaa}");
        sb.AppendLine(".lightbox{display:none;position:fixed;inset:0;background:rgba(0,0,0,.85);justify-content:center;align-items:center;z-index:999}");
        sb.AppendLine(".lightbox.active{display:flex}");
        sb.AppendLine(".lightbox img{max-width:90vw;max-height:90vh;border-radius:8px;box-shadow:0 0 40px #000}");
        sb.AppendLine(".lightbox-close{position:fixed;top:20px;right:30px;font-size:36px;color:#fff;cursor:pointer;line-height:1}");
        sb.AppendLine("</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>ImgSeek: \"{searchTerm}\" — {matches.Count} match(es)</h1>");
        sb.AppendLine("<div class=\"grid\">");
        foreach (var p in matches)
        {
            // Convert path to file:/// URI
            string uri = new Uri(p).AbsoluteUri;
            string fname = Path.GetFileName(p);
            sb.AppendLine($"<div class=\"card\" onclick=\"openLightbox('{uri.Replace("'", "\\'")}')\">");
            sb.AppendLine($"  <img src=\"{uri}\" alt=\"{fname}\" loading=\"lazy\">");
            sb.AppendLine($"  <p>{fname}</p>");
            sb.AppendLine("</div>");
        }
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"lightbox\" id=\"lb\" onclick=\"closeLightbox()\">");
        sb.AppendLine("  <span class=\"lightbox-close\" onclick=\"closeLightbox()\">&times;</span>");
        sb.AppendLine("  <img id=\"lb-img\" src=\"\" alt=\"\">");
        sb.AppendLine("</div>");
        sb.AppendLine("<script>");
        sb.AppendLine("function openLightbox(src){document.getElementById('lb-img').src=src;document.getElementById('lb').classList.add('active');}");
        sb.AppendLine("function closeLightbox(){document.getElementById('lb').classList.remove('active');document.getElementById('lb-img').src='';}");
        sb.AppendLine("document.addEventListener('keydown',e=>{if(e.key==='Escape')closeLightbox();});");
        sb.AppendLine("</script>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
