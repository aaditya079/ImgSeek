using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ImgSeek
{
    public class ScanProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string CurrentFile { get; set; } = "";
        public bool IsMatch { get; set; }
        public string? MatchPath { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ScanOptions
    {
        public bool CaseSensitive { get; set; }
        public bool UseRegex { get; set; }
        public string? LanguageTag { get; set; }
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
    }

    public static class OcrScannerCore
    {
        public static async Task<List<string>> RunScanAsync(
            string imageDir,
            string searchTerm,
            ScanOptions options,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken)
        {
            var matches = new List<string>();

            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };

            var allImages = new List<string>();
            var dirs = imageDir.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir))
                    throw new DirectoryNotFoundException($"Directory not found: {dir}");

                foreach (var file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (extensions.Contains(Path.GetExtension(file)))
                        allImages.Add(file);
                }
            }

            if (allImages.Count == 0) return matches;

            // Prepare search term matching helper
            System.Text.RegularExpressions.Regex? regex = null;
            string termLower = searchTerm.ToLower();
            if (options.UseRegex)
            {
                var regexOptions = options.CaseSensitive
                    ? System.Text.RegularExpressions.RegexOptions.None
                    : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
                regex = new System.Text.RegularExpressions.Regex(searchTerm, regexOptions);
            }

            int current = 0;
            int total = allImages.Count;

            // Determine target language
            Windows.Globalization.Language? targetLanguage = null;
            if (!string.IsNullOrEmpty(options.LanguageTag))
            {
                targetLanguage = new Windows.Globalization.Language(options.LanguageTag);
            }

            int dop = options.MaxDegreeOfParallelism > 0 ? options.MaxDegreeOfParallelism : Environment.ProcessorCount;
            using var semaphore = new SemaphoreSlim(dop);
            var tasks = new List<Task>();

            foreach (var imgPath in allImages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int thisIndex = Interlocked.Increment(ref current);
                        string name = Path.GetFileName(imgPath);

                        // Initial report for reading progress
                        progress.Report(new ScanProgress { Current = thisIndex, Total = total, CurrentFile = name });

                        string fullPath = Path.GetFullPath(imgPath);
                        var storageFile = await StorageFile.GetFileFromPathAsync(fullPath);
                        using var stream = await storageFile.OpenAsync(FileAccessMode.Read);
                        var decoder = await BitmapDecoder.CreateAsync(stream);
                        var bitmap = await decoder.GetSoftwareBitmapAsync();

                        // Instantiate local OcrEngine for thread safety
                        OcrEngine? engine = null;
                        if (targetLanguage != null && OcrEngine.IsLanguageSupported(targetLanguage))
                        {
                            engine = OcrEngine.TryCreateFromLanguage(targetLanguage);
                        }
                        
                        if (engine == null)
                        {
                            engine = OcrEngine.TryCreateFromUserProfileLanguages();
                        }

                        if (engine == null)
                        {
                            throw new InvalidOperationException("Could not create Windows OCR engine. Ensure a Windows Language Pack with OCR support is installed.");
                        }

                        var result = await engine.RecognizeAsync(bitmap);
                        bool isMatch = false;

                        if (options.UseRegex && regex != null)
                        {
                            isMatch = regex.IsMatch(result.Text);
                        }
                        else
                        {
                            if (options.CaseSensitive)
                            {
                                isMatch = result.Text.Contains(searchTerm);
                            }
                            else
                            {
                                isMatch = result.Text.ToLower().Contains(termLower);
                            }
                        }

                        if (isMatch)
                        {
                            lock (matches)
                            {
                                matches.Add(fullPath);
                            }
                            progress.Report(new ScanProgress { Current = thisIndex, Total = total, CurrentFile = name, IsMatch = true, MatchPath = fullPath });
                        }
                    }
                    catch (Exception ex)
                    {
                        int thisIndex = Interlocked.CompareExchange(ref current, 0, 0);
                        string name = Path.GetFileName(imgPath);
                        progress.Report(new ScanProgress { Current = thisIndex, Total = total, CurrentFile = name, ErrorMessage = ex.Message });
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            return matches;
        }

        public static string BuildHtml(List<string> matches, string searchTerm, string copyBatName)
        {
            var uriJson = new StringBuilder("[");
            var pathJson = new StringBuilder("[");
            for (int i = 0; i < matches.Count; i++)
            {
                if (i > 0) { uriJson.Append(','); pathJson.Append(','); }
                uriJson.Append('"').Append(new Uri(matches[i]).AbsoluteUri.Replace("\"", "\\\"")).Append('"');
                pathJson.Append('"').Append(matches[i].Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            }
            uriJson.Append(']'); pathJson.Append(']');

            string th = EscHtml(searchTerm), bj = EscJs(copyBatName), cnt = matches.Count.ToString();
            string mw = matches.Count == 1 ? "" : "s", me = matches.Count == 1 ? "" : "es";

            var h = new StringBuilder();
            h.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\">");
            h.AppendLine($"<title>ImgSeek \"{th}\"</title><style>");
            h.AppendLine("*{box-sizing:border-box;margin:0;padding:0}");
            h.AppendLine("body{font-family:'Segoe UI',sans-serif;background:#0d0d0d;color:#e0e0e0;min-height:100vh}");
            h.AppendLine(".header{background:linear-gradient(135deg,#1a1a2e,#16213e,#0f3460);padding:28px 32px 20px;border-bottom:1px solid #ffffff18}");
            h.AppendLine(".header h1{font-size:1.7rem;font-weight:700;color:#fff}.header h1 span{color:#4fc3f7}");
            h.AppendLine(".header p{margin-top:6px;color:#90caf9;font-size:.95rem}");
            h.AppendLine(".toolbar{display:flex;gap:12px;flex-wrap:wrap;padding:16px 32px;background:#111827;border-bottom:1px solid #ffffff12;align-items:center}");
            h.AppendLine(".btn{display:inline-flex;align-items:center;gap:8px;padding:10px 20px;border:none;border-radius:8px;font-size:.9rem;font-weight:600;cursor:pointer;transition:all .2s}");
            h.AppendLine(".btn-copy{background:linear-gradient(135deg,#1e88e5,#1565c0);color:#fff}.btn-copy:hover{background:linear-gradient(135deg,#42a5f5,#1e88e5);transform:translateY(-1px)}");
            h.AppendLine(".btn-dl{background:linear-gradient(135deg,#10b981,#059669);color:#fff}.btn-dl:hover{background:linear-gradient(135deg,#34d399,#10b981);transform:translateY(-1px)}");
            h.AppendLine(".btn-paths{background:#1e2a3a;color:#90caf9;border:1px solid #1e88e540}.btn-paths:hover{background:#253547;transform:translateY(-1px)}");
            h.AppendLine(".badge{margin-left:auto;background:#0f3460;padding:6px 14px;border-radius:20px;font-size:.85rem;color:#4fc3f7;font-weight:600;border:1px solid #4fc3f740}");
            h.AppendLine(".toast{position:fixed;bottom:28px;left:50%;transform:translateX(-50%) translateY(80px);background:#1e88e5;color:#fff;padding:12px 24px;border-radius:10px;font-weight:600;transition:transform .3s cubic-bezier(.34,1.56,.64,1);z-index:9999;pointer-events:none;box-shadow:0 4px 20px #0005}");
            h.AppendLine(".toast.show{transform:translateX(-50%) translateY(0)}");
            h.AppendLine(".panel{display:none;background:#111827;padding:16px 32px;border-bottom:1px solid #ffffff12}.panel.open{display:block}");
            h.AppendLine(".panel textarea{width:100%;height:130px;background:#0d1117;color:#c9d1d9;border:1px solid #30363d;border-radius:8px;padding:10px;font-family:monospace;font-size:.82rem;resize:vertical}");
            h.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:14px;padding:24px 32px}");
            h.AppendLine(".card{background:#161b22;border-radius:10px;overflow:hidden;border:1px solid #ffffff0f;transition:transform .2s,box-shadow .2s,border-color .2s}");
            h.AppendLine(".card:hover{transform:translateY(-4px);box-shadow:0 8px 30px #0008;border-color:#1e88e540}");
            h.AppendLine(".card img{width:100%;height:170px;object-fit:cover;display:block;cursor:pointer}");
            h.AppendLine(".card-foot{padding:8px 10px;display:flex;align-items:center;gap:6px}");
            h.AppendLine(".card-name{font-size:.75rem;color:#8b949e;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;flex:1}");
            h.AppendLine(".cp-btn{background:#1e2a3a;border:1px solid #1e88e540;color:#4fc3f7;border-radius:5px;padding:3px 8px;font-size:.7rem;font-weight:600;cursor:pointer;flex-shrink:0}");
            h.AppendLine(".cp-btn:hover{background:#253547}");
            h.AppendLine(".lb{display:none;position:fixed;inset:0;background:rgba(0,0,0,.9);justify-content:center;align-items:center;z-index:1000;backdrop-filter:blur(4px)}.lb.on{display:flex}");
            h.AppendLine(".lb img{max-width:92vw;max-height:88vh;border-radius:10px;box-shadow:0 0 60px #000a;object-fit:contain}");
            h.AppendLine(".lb-x{position:fixed;top:18px;right:26px;font-size:34px;color:#fff;cursor:pointer;opacity:.7}.lb-x:hover{opacity:1}");
            h.AppendLine(".lb-p{position:fixed;bottom:20px;left:50%;transform:translateX(-50%);background:#161b22cc;backdrop-filter:blur(6px);padding:8px 18px;border-radius:8px;font-size:.8rem;color:#90caf9;max-width:90vw;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}");
            h.AppendLine("</style></head><body>");
            h.AppendLine($"<div class=\"header\"><h1>ImgSeek &mdash; <span>&quot;{th}&quot;</span></h1><p>Found {cnt} matching image{mw} using Windows OCR</p></div>");
            h.AppendLine($"<div class=\"toolbar\"><button class=\"btn btn-copy\" onclick=\"copyAll()\">&#128190; Copy All Photos</button><button class=\"btn btn-dl\" onclick=\"downloadAll()\">&#128229; Download All</button><button class=\"btn btn-paths\" id=\"btnP\" onclick=\"togglePaths()\">&#128196; Show File Paths</button><span class=\"badge\">{cnt} match{me}</span></div>");
            h.AppendLine("<div class=\"panel\" id=\"panel\"><textarea id=\"ptxt\" readonly></textarea></div>");
            h.AppendLine("<div class=\"grid\" id=\"grid\"></div>");
            h.AppendLine("<div class=\"lb\" id=\"lb\" onclick=\"closeLb(event)\"><span class=\"lb-x\" onclick=\"closeLb()\">&#10005;</span><img id=\"lbImg\" src=\"\" alt=\"\"><div class=\"lb-p\" id=\"lbP\"></div></div>");
            h.AppendLine("<div class=\"toast\" id=\"toast\"></div>");
            h.AppendLine("<script>");
            h.AppendLine($"var U={uriJson};var P={pathJson};var BAT='{bj}';");
            h.AppendLine("var g=document.getElementById('grid');");
            h.AppendLine("U.forEach(function(uri,i){var f=P[i].split(/[\\\\/]/).pop();var c=document.createElement('div');c.className='card';var im=document.createElement('img');im.src=uri;im.alt=f;im.loading='lazy';im.onclick=(function(n){return function(){openLb(n);};})(i);var ft=document.createElement('div');ft.className='card-foot';var nm=document.createElement('span');nm.className='card-name';nm.title=P[i];nm.textContent=f;var cb=document.createElement('button');cb.className='cp-btn';cb.textContent='Copy path';cb.onclick=(function(n){return function(e){e.stopPropagation();navigator.clipboard.writeText(P[n]).then(function(){toast('Path copied!');});};})(i);ft.appendChild(nm);ft.appendChild(cb);c.appendChild(im);c.appendChild(ft);g.appendChild(c);});");
            h.AppendLine("function openLb(i){document.getElementById('lbImg').src=U[i];document.getElementById('lbP').textContent=P[i];document.getElementById('lb').classList.add('on');}");
            h.AppendLine("function closeLb(e){if(!e||e.target===document.getElementById('lb')||e.target.classList.contains('lb-x')){document.getElementById('lb').classList.remove('on');document.getElementById('lbImg').src='';}}");
            h.AppendLine("document.addEventListener('keydown',function(e){if(e.key==='Escape')closeLb();});");
            h.AppendLine("function togglePaths(){var p=document.getElementById('panel'),b=document.getElementById('btnP');if(p.classList.toggle('open')){document.getElementById('ptxt').value=P.join('\\r\\n');b.textContent='Hide File Paths';}else{b.textContent='Show File Paths';}}");
            h.AppendLine("function copyAll(){var base=location.href.replace(/\\?.*/,'');var dir=base.substring(0,base.lastIndexOf('/')+1);var a=document.createElement('a');a.href=dir+BAT.replace(/ /g,'%20');a.click();toast('Opening copy tool...');}");
            h.AppendLine("function downloadAll(){if(U.length===0)return;U.forEach(function(uri,i){setTimeout(function(){var f=P[i].split(/[\\\\/]/).pop();var a=document.createElement('a');a.href=uri;a.download=f;document.body.appendChild(a);a.click();document.body.removeChild(a);},i*150);});toast('Downloading '+U.length+' photo(s)...');}");
            h.AppendLine("var tt;function toast(m){var t=document.getElementById('toast');t.textContent=m;t.classList.add('show');clearTimeout(tt);tt=setTimeout(function(){t.classList.remove('show');},2500);}");
            h.AppendLine("</script></body></html>");
            return h.ToString();
        }

        public static string BuildCopyBat(List<string> matches, string searchTerm)
        {
            var sb = new StringBuilder();
            sb.AppendLine("@echo off\nchcp 65001 >nul\ntitle ImgSeek - Copy matched photos");
            sb.AppendLine($"echo Found {matches.Count} photo(s) matching \"{searchTerm}\".\necho.");
            sb.AppendLine("set /p \"DEST=Enter destination folder to copy photos into: \"\necho.");
            sb.AppendLine("if not exist \"%DEST%\" (mkdir \"%DEST%\"\necho Created folder: %DEST%\n)");
            sb.AppendLine($"echo Copying {matches.Count} file(s)...\necho.");
            foreach (var p in matches)
                sb.AppendLine($"copy /Y \"{p.Replace("%", "%%")}\" \"%DEST%\\\"");
            sb.AppendLine($"echo.\necho Done! {matches.Count} file(s) copied to \"%DEST%\"\necho.\npause");
            return sb.ToString();
        }

        public static string SanitizeFileName(string name) =>
            string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim();

        private static string EscHtml(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        private static string EscJs(string s) =>
            s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
    }
}
