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
        string imageDir   = args.Length > 0 ? args[0] : "";
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
            Console.Error.WriteLine("ERROR: Directory not found: " + imageDir);
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
        Console.WriteLine("=== Image Name Searcher ===");
        Console.ResetColor();
        Console.WriteLine("  Folder : " + imageDir);
        Console.WriteLine("  Keyword: \"" + searchTerm + "\"");
        Console.WriteLine("  Images : " + allImages.Count);
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
            Console.Write("[" + i.ToString().PadLeft(4) + "/" + allImages.Count + "] " + name.PadRight(60));

            try
            {
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
                Console.WriteLine("SKIP (" + ex.Message + ")");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== Done: " + matches.Count + " match(es) out of " + allImages.Count + " images ===");
        Console.ResetColor();

        if (matches.Count == 0)
            return 0;

        // Determine output paths
        string baseName = SanitizeFileName(searchTerm) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        if (string.IsNullOrWhiteSpace(outputHtml))
            outputHtml = Path.Combine(Path.GetTempPath(), "ImgSeek_" + baseName + ".html");

        string copyBatPath = Path.Combine(Path.GetDirectoryName(outputHtml)!, "ImgSeek_" + baseName + "_CopyFiles.bat");
        string copyBatName = Path.GetFileName(copyBatPath);

        // Write HTML gallery
        File.WriteAllText(outputHtml, BuildHtml(matches, searchTerm, copyBatName), Encoding.UTF8);

        // Write copy batch script
        File.WriteAllText(copyBatPath, BuildCopyBat(matches, searchTerm), Encoding.ASCII);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Gallery  : " + outputHtml);
        Console.WriteLine("Copy tool: " + copyBatPath);
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Tip: In the gallery click 'Copy All Photos', or run the _CopyFiles.bat directly.");

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

    // ─── HTML Gallery ────────────────────────────────────────────────────────
    static string BuildHtml(List<string> matches, string searchTerm, string copyBatName)
    {
        // JSON array of file:/// URIs
        var uriJson = new StringBuilder("[");
        for (int idx = 0; idx < matches.Count; idx++)
        {
            if (idx > 0) uriJson.Append(',');
            uriJson.Append('"');
            uriJson.Append(new Uri(matches[idx]).AbsoluteUri.Replace("\"", "\\\""));
            uriJson.Append('"');
        }
        uriJson.Append(']');

        // JSON array of Windows paths
        var pathJson = new StringBuilder("[");
        for (int idx = 0; idx < matches.Count; idx++)
        {
            if (idx > 0) pathJson.Append(',');
            pathJson.Append('"');
            pathJson.Append(matches[idx].Replace("\\", "\\\\").Replace("\"", "\\\""));
            pathJson.Append('"');
        }
        pathJson.Append(']');

        string th = EscHtml(searchTerm);
        string bj = EscJs(copyBatName);
        string cnt = matches.Count.ToString();
        string matchWord = matches.Count == 1 ? "" : "s";
        string matchEs   = matches.Count == 1 ? "" : "es";

        var h = new StringBuilder();
        h.AppendLine("<!DOCTYPE html>");
        h.AppendLine("<html lang=\"en\">");
        h.AppendLine("<head>");
        h.AppendLine("<meta charset=\"UTF-8\">");
        h.AppendLine("<title>ImgSeek \"" + th + "\"</title>");
        h.AppendLine("<style>");
        h.AppendLine("*{box-sizing:border-box;margin:0;padding:0}");
        h.AppendLine("body{font-family:'Segoe UI',sans-serif;background:#0d0d0d;color:#e0e0e0;min-height:100vh}");
        h.AppendLine(".header{background:linear-gradient(135deg,#1a1a2e,#16213e,#0f3460);padding:28px 32px 20px;border-bottom:1px solid #ffffff18}");
        h.AppendLine(".header h1{font-size:1.7rem;font-weight:700;color:#fff}");
        h.AppendLine(".header h1 span{color:#4fc3f7}");
        h.AppendLine(".header p{margin-top:6px;color:#90caf9;font-size:.95rem}");
        h.AppendLine(".toolbar{display:flex;gap:12px;flex-wrap:wrap;padding:16px 32px;background:#111827;border-bottom:1px solid #ffffff12;align-items:center}");
        h.AppendLine(".btn{display:inline-flex;align-items:center;gap:8px;padding:10px 20px;border:none;border-radius:8px;font-size:.9rem;font-weight:600;cursor:pointer;transition:all .2s}");
        h.AppendLine(".btn-copy{background:linear-gradient(135deg,#1e88e5,#1565c0);color:#fff}");
        h.AppendLine(".btn-copy:hover{background:linear-gradient(135deg,#42a5f5,#1e88e5);transform:translateY(-1px);box-shadow:0 4px 15px #1e88e540}");
        h.AppendLine(".btn-download{background:linear-gradient(135deg,#10b981,#059669);color:#fff}");
        h.AppendLine(".btn-download:hover{background:linear-gradient(135deg,#34d399,#10b981);transform:translateY(-1px);box-shadow:0 4px 15px #10b98140}");
        h.AppendLine(".btn-paths{background:#1e2a3a;color:#90caf9;border:1px solid #1e88e540}");
        h.AppendLine(".btn-paths:hover{background:#253547;transform:translateY(-1px)}");
        h.AppendLine(".badge{margin-left:auto;background:#0f3460;padding:6px 14px;border-radius:20px;font-size:.85rem;color:#4fc3f7;font-weight:600;border:1px solid #4fc3f740}");
        h.AppendLine(".toast{position:fixed;bottom:28px;left:50%;transform:translateX(-50%) translateY(80px);background:#1e88e5;color:#fff;padding:12px 24px;border-radius:10px;font-weight:600;transition:transform .3s cubic-bezier(.34,1.56,.64,1);z-index:9999;pointer-events:none;box-shadow:0 4px 20px #0005}");
        h.AppendLine(".toast.show{transform:translateX(-50%) translateY(0)}");
        h.AppendLine(".panel{display:none;background:#111827;padding:16px 32px;border-bottom:1px solid #ffffff12}");
        h.AppendLine(".panel.open{display:block}");
        h.AppendLine(".panel textarea{width:100%;height:130px;background:#0d1117;color:#c9d1d9;border:1px solid #30363d;border-radius:8px;padding:10px;font-family:monospace;font-size:.82rem;resize:vertical}");
        h.AppendLine(".panel .hint{margin-top:8px;font-size:.8rem;color:#6e7681}");
        h.AppendLine(".panel .hint b{color:#4fc3f7}");
        h.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:14px;padding:24px 32px}");
        h.AppendLine(".card{background:#161b22;border-radius:10px;overflow:hidden;border:1px solid #ffffff0f;transition:transform .2s,box-shadow .2s,border-color .2s}");
        h.AppendLine(".card:hover{transform:translateY(-4px);box-shadow:0 8px 30px #0008;border-color:#1e88e540}");
        h.AppendLine(".card img{width:100%;height:170px;object-fit:cover;display:block;cursor:pointer}");
        h.AppendLine(".card-foot{padding:8px 10px;display:flex;align-items:center;gap:6px}");
        h.AppendLine(".card-name{font-size:.75rem;color:#8b949e;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;flex:1}");
        h.AppendLine(".cp-btn{background:#1e2a3a;border:1px solid #1e88e540;color:#4fc3f7;border-radius:5px;padding:3px 8px;font-size:.7rem;font-weight:600;cursor:pointer;flex-shrink:0;transition:background .15s}");
        h.AppendLine(".cp-btn:hover{background:#253547}");
        h.AppendLine(".lb{display:none;position:fixed;inset:0;background:rgba(0,0,0,.9);justify-content:center;align-items:center;z-index:1000;backdrop-filter:blur(4px)}");
        h.AppendLine(".lb.on{display:flex}");
        h.AppendLine(".lb img{max-width:92vw;max-height:88vh;border-radius:10px;box-shadow:0 0 60px #000a;object-fit:contain}");
        h.AppendLine(".lb-x{position:fixed;top:18px;right:26px;font-size:34px;color:#fff;cursor:pointer;opacity:.7;transition:opacity .15s}");
        h.AppendLine(".lb-x:hover{opacity:1}");
        h.AppendLine(".lb-p{position:fixed;bottom:20px;left:50%;transform:translateX(-50%);background:#161b22cc;backdrop-filter:blur(6px);padding:8px 18px;border-radius:8px;font-size:.8rem;color:#90caf9;max-width:90vw;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}");
        h.AppendLine("</style>");
        h.AppendLine("</head><body>");

        h.AppendLine("<div class=\"header\">");
        h.AppendLine("  <h1>ImgSeek &mdash; <span>&quot;" + th + "&quot;</span></h1>");
        h.AppendLine("  <p>Found " + cnt + " matching image" + matchWord + " using Windows OCR</p>");
        h.AppendLine("</div>");

        h.AppendLine("<div class=\"toolbar\">");
        h.AppendLine("  <button class=\"btn btn-copy\" onclick=\"copyAll()\">&#128190; Copy All Photos</button>");
        h.AppendLine("  <button class=\"btn btn-download\" onclick=\"downloadAll()\">&#128229; Download All</button>");
        h.AppendLine("  <button class=\"btn btn-paths\" id=\"btnP\" onclick=\"togglePaths()\">&#128196; Show File Paths</button>");
        h.AppendLine("  <span class=\"badge\">" + cnt + " match" + matchEs + "</span>");
        h.AppendLine("</div>");

        h.AppendLine("<div class=\"panel\" id=\"panel\">");
        h.AppendLine("  <textarea id=\"ptxt\" readonly></textarea>");
        h.AppendLine("  <p class=\"hint\"><b>Tip:</b> Ctrl+A then Ctrl+C to copy all paths &mdash; or click <b>Copy All Photos</b> to copy the actual files.</p>");
        h.AppendLine("</div>");

        h.AppendLine("<div class=\"grid\" id=\"grid\"></div>");

        h.AppendLine("<div class=\"lb\" id=\"lb\" onclick=\"closeLb(event)\">");
        h.AppendLine("  <span class=\"lb-x\" onclick=\"closeLb()\">&#10005;</span>");
        h.AppendLine("  <img id=\"lbImg\" src=\"\" alt=\"\">");
        h.AppendLine("  <div class=\"lb-p\" id=\"lbP\"></div>");
        h.AppendLine("</div>");

        h.AppendLine("<div class=\"toast\" id=\"toast\"></div>");

        h.AppendLine("<script>");
        h.AppendLine("var U=" + uriJson + ";");
        h.AppendLine("var P=" + pathJson + ";");
        h.AppendLine("var BAT='" + bj + "';");

        h.AppendLine("var g=document.getElementById('grid');");
        h.AppendLine("U.forEach(function(uri,i){");
        h.AppendLine("  var f=P[i].split(/[\\\\/]/).pop();");
        h.AppendLine("  var c=document.createElement('div'); c.className='card';");
        h.AppendLine("  var im=document.createElement('img');");
        h.AppendLine("  im.src=uri; im.alt=f; im.loading='lazy';");
        h.AppendLine("  im.onclick=(function(n){return function(){openLb(n);};})(i);");
        h.AppendLine("  var ft=document.createElement('div'); ft.className='card-foot';");
        h.AppendLine("  var nm=document.createElement('span'); nm.className='card-name'; nm.title=P[i]; nm.textContent=f;");
        h.AppendLine("  var cb=document.createElement('button'); cb.className='cp-btn'; cb.textContent='Copy path';");
        h.AppendLine("  cb.onclick=(function(n){return function(e){e.stopPropagation();navigator.clipboard.writeText(P[n]).then(function(){toast('Path copied!');});};})(i);");
        h.AppendLine("  ft.appendChild(nm); ft.appendChild(cb);");
        h.AppendLine("  c.appendChild(im); c.appendChild(ft);");
        h.AppendLine("  g.appendChild(c);");
        h.AppendLine("});");

        h.AppendLine("function openLb(i){");
        h.AppendLine("  document.getElementById('lbImg').src=U[i];");
        h.AppendLine("  document.getElementById('lbP').textContent=P[i];");
        h.AppendLine("  document.getElementById('lb').classList.add('on');");
        h.AppendLine("}");
        h.AppendLine("function closeLb(e){");
        h.AppendLine("  if(!e||e.target===document.getElementById('lb')||e.target.classList.contains('lb-x')){");
        h.AppendLine("    document.getElementById('lb').classList.remove('on');");
        h.AppendLine("    document.getElementById('lbImg').src='';");
        h.AppendLine("  }");
        h.AppendLine("}");
        h.AppendLine("document.addEventListener('keydown',function(e){if(e.key==='Escape')closeLb();});");

        h.AppendLine("function togglePaths(){");
        h.AppendLine("  var p=document.getElementById('panel'), b=document.getElementById('btnP');");
        h.AppendLine("  if(p.classList.toggle('open')){");
        h.AppendLine("    document.getElementById('ptxt').value=P.join('\\r\\n');");
        h.AppendLine("    b.textContent='Hide File Paths';");
        h.AppendLine("  } else { b.textContent='Show File Paths'; }");
        h.AppendLine("}");

        h.AppendLine("function copyAll(){");
        h.AppendLine("  var base=location.href.replace(/\\?.*/,'');");
        h.AppendLine("  var dir=base.substring(0,base.lastIndexOf('/')+1);");
        h.AppendLine("  var a=document.createElement('a');");
        h.AppendLine("  a.href=dir+BAT.replace(/ /g,'%20');");
        h.AppendLine("  a.click();");
        h.AppendLine("  toast('Opening copy tool...');");
        h.AppendLine("}");
        h.AppendLine("");
        h.AppendLine("function downloadAll(){");
        h.AppendLine("  if(U.length===0) return;");
        h.AppendLine("  U.forEach(function(uri,i){");
        h.AppendLine("    setTimeout(function(){");
        h.AppendLine("      var f=P[i].split(/[\\\\/]/).pop();");
        h.AppendLine("      var a=document.createElement('a');");
        h.AppendLine("      a.href=uri;");
        h.AppendLine("      a.download=f;");
        h.AppendLine("      document.body.appendChild(a);");
        h.AppendLine("      a.click();");
        h.AppendLine("      document.body.removeChild(a);");
        h.AppendLine("    },i*150);");
        h.AppendLine("  });");
        h.AppendLine("  toast('Downloading ' + U.length + ' photo(s)...');");
        h.AppendLine("}");

        h.AppendLine("var tt;");
        h.AppendLine("function toast(m){");
        h.AppendLine("  var t=document.getElementById('toast');");
        h.AppendLine("  t.textContent=m; t.classList.add('show');");
        h.AppendLine("  clearTimeout(tt);");
        h.AppendLine("  tt=setTimeout(function(){t.classList.remove('show');},2500);");
        h.AppendLine("}");
        h.AppendLine("</script>");
        h.AppendLine("</body></html>");

        return h.ToString();
    }

    // ─── Copy Batch Script ────────────────────────────────────────────────────
    static string BuildCopyBat(List<string> matches, string searchTerm)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("chcp 65001 >nul");
        sb.AppendLine("title ImgSeek - Copy matched photos");
        sb.AppendLine("echo.");
        sb.AppendLine("echo Found " + matches.Count + " photo(s) matching \"" + searchTerm + "\".");
        sb.AppendLine("echo.");
        sb.AppendLine("set /p \"DEST=Enter destination folder to copy photos into: \"");
        sb.AppendLine("echo.");
        sb.AppendLine("if not exist \"%DEST%\" (");
        sb.AppendLine("    mkdir \"%DEST%\"");
        sb.AppendLine("    echo Created folder: %DEST%");
        sb.AppendLine(")");
        sb.AppendLine("echo Copying " + matches.Count + " file(s)...");
        sb.AppendLine("echo.");
        foreach (var p in matches)
        {
            string escaped = p.Replace("%", "%%");
            sb.AppendLine("copy /Y \"" + escaped + "\" \"%DEST%\\\"");
        }
        sb.AppendLine("echo.");
        sb.AppendLine("echo Done! " + matches.Count + " file(s) copied to \"%DEST%\"");
        sb.AppendLine("echo.");
        sb.AppendLine("pause");
        return sb.ToString();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    static string SanitizeFileName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim();

    static string EscHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    static string EscJs(string s) =>
        s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
}
