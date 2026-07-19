// reze ma queen 🥀
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
        public bool MatchAllKeywords { get; set; }
    }

    public static class OcrScannerCore
    {
        public static async Task<Dictionary<string, List<string>>> RunScanAsync(
            string imageDir,
            string searchTerm,
            ScanOptions options,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken)
        {
            // reze ma queen 🥀
            var matches = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (options.UseRegex)
            {
                matches[searchTerm] = new List<string>();
            }
            else
            {
                var keywords = searchTerm.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (options.MatchAllKeywords)
                {
                    string combinedKey = string.Join(" & ", keywords);
                    matches[combinedKey] = new List<string>();
                }
                else
                {
                    foreach (var kw in keywords)
                    {
                        matches[kw] = new List<string>();
                    }
                }
            }

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
                regex = new System.Text.RegularExpressions.Regex(searchTerm, regexOptions, TimeSpan.FromMilliseconds(500));
            }

            int current = 0;
            int total = allImages.Count;

            // Determine target language
            Windows.Globalization.Language? targetLanguage = null;
            if (!string.IsNullOrEmpty(options.LanguageTag))
            {
                targetLanguage = new Windows.Globalization.Language(options.LanguageTag);
            }

            // reze ma queen 🥀
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
                        // reze ma queen 🥀
                        if (options.UseRegex && regex != null)
                        {
                            if (regex.IsMatch(result.Text))
                            {
                                lock (matches)
                                {
                                    if (!matches.ContainsKey(searchTerm))
                                        matches[searchTerm] = new List<string>();
                                    matches[searchTerm].Add(fullPath);
                                }
                                progress.Report(new ScanProgress { Current = thisIndex, Total = total, CurrentFile = name, IsMatch = true, MatchPath = fullPath });
                            }
                        }
                        else
                        {
                            var keywords = searchTerm.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (keywords.Length > 0)
                            {
                                if (options.MatchAllKeywords)
                                {
                                    bool allMatch = true;
                                    foreach (var kw in keywords)
                                    {
                                        bool hasKw = options.CaseSensitive
                                            ? result.Text.Contains(kw)
                                            : result.Text.ToLower().Contains(kw.ToLower());
                                        if (!hasKw) { allMatch = false; break; }
                                    }
                                    if (allMatch)
                                    {
                                        string combinedKey = string.Join(" & ", keywords);
                                        lock (matches)
                                        {
                                            if (!matches.ContainsKey(combinedKey))
                                                matches[combinedKey] = new List<string>();
                                            matches[combinedKey].Add(fullPath);
                                        }
                                        progress.Report(new ScanProgress { Current = thisIndex, Total = total, CurrentFile = name, IsMatch = true, MatchPath = fullPath });
                                    }
                                }
                                else
                                {
                                    bool matchedAny = false;
                                    foreach (var kw in keywords)
                                    {
                                        bool hasKw = options.CaseSensitive
                                            ? result.Text.Contains(kw)
                                            : result.Text.ToLower().Contains(kw.ToLower());
                                        if (hasKw)
                                        {
                                            lock (matches)
                                            {
                                                if (!matches.ContainsKey(kw))
                                                    matches[kw] = new List<string>();
                                                matches[kw].Add(fullPath);
                                            }
                                            matchedAny = true;
                                        }
                                    }
                                    if (matchedAny)
                                    {
                                        progress.Report(new ScanProgress { Current = thisIndex, Total = total, CurrentFile = name, IsMatch = true, MatchPath = fullPath });
                                    }
                                }
                            }
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

        public static string BuildHtml(Dictionary<string, List<string>> groups, string searchTerm, string copyBatName)
        {
            // reze ma queen 🥀
            var groupsJson = new StringBuilder("{");
            bool firstGroup = true;
            int totalCount = 0;
            foreach (var kvp in groups)
            {
                if (kvp.Value.Count == 0) continue;
                totalCount += kvp.Value.Count;

                if (!firstGroup) groupsJson.Append(',');
                firstGroup = false;

                groupsJson.Append('"').Append(EscJs(kvp.Key)).Append("\":{");
                
                var uris = new StringBuilder("[");
                var paths = new StringBuilder("[");
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    if (i > 0) { uris.Append(','); paths.Append(','); }
                    uris.Append('"').Append(new Uri(kvp.Value[i]).AbsoluteUri.Replace("\"", "\\\"")).Append('"');
                    paths.Append('"').Append(kvp.Value[i].Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                }
                uris.Append(']');
                paths.Append(']');

                groupsJson.Append("\"uris\":").Append(uris).Append(",\"paths\":").Append(paths).Append("}");
            }
            groupsJson.Append('}');

            string th = EscHtml(searchTerm), bj = EscJs(copyBatName), cnt = totalCount.ToString();
            string mw = totalCount == 1 ? "" : "s", me = totalCount == 1 ? "" : "es";

            var h = new StringBuilder();
            h.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\">");
            h.AppendLine($"<title>ImgSeek \"{th}\"</title><style>");
            h.AppendLine("*{box-sizing:border-box;margin:0;padding:0}");
            h.AppendLine("body{font-family:'Segoe UI',sans-serif;background:#0d0d0d;color:#e0e0e0;min-height:100vh}");
            h.AppendLine(".header{background:linear-gradient(135deg,#1f132e,#1a1029,#2e144a);padding:28px 32px 20px;border-bottom:1px solid #ffffff18}");
            h.AppendLine(".header h1{font-size:1.7rem;font-weight:700;color:#fff}.header h1 span{color:#d8b4fe}");
            h.AppendLine(".header p{margin-top:6px;color:#c084fc;font-size:.95rem}");
            h.AppendLine(".toolbar{display:flex;gap:12px;flex-wrap:wrap;padding:16px 32px;background:#111827;border-bottom:1px solid #ffffff12;align-items:center}");
            h.AppendLine(".btn{display:inline-flex;align-items:center;gap:8px;padding:10px 20px;border:none;border-radius:8px;font-size:.9rem;font-weight:600;cursor:pointer;transition:all .2s}");
            h.AppendLine(".btn-copy{background:linear-gradient(135deg,#a855f7,#8b5cf6);color:#fff}.btn-copy:hover{background:linear-gradient(135deg,#c084fc,#a855f7);transform:translateY(-1px)}");
            h.AppendLine(".btn-dl{background:linear-gradient(135deg,#10b981,#059669);color:#fff}.btn-dl:hover{background:linear-gradient(135deg,#34d399,#10b981);transform:translateY(-1px)}");
            h.AppendLine(".btn-paths{background:#211a2d;color:#d8b4fe;border:1px solid #8b5cf640}.btn-paths:hover{background:#2e2240;transform:translateY(-1px)}");
            h.AppendLine(".badge{margin-left:auto;background:#2e144a;padding:6px 14px;border-radius:20px;font-size:.85rem;color:#d8b4fe;font-weight:600;border:1px solid #d8b4fe40}");
            h.AppendLine(".toast{position:fixed;bottom:28px;left:50%;transform:translateX(-50%) translateY(80px);background:#8b5cf6;color:#fff;padding:12px 24px;border-radius:10px;font-weight:600;transition:transform .3s cubic-bezier(.34,1.56,.64,1);z-index:9999;pointer-events:none;box-shadow:0 4px 20px #0005}");
            h.AppendLine(".toast.show{transform:translateX(-50%) translateY(0)}");
            h.AppendLine(".panel{display:none;background:#111827;padding:16px 32px;border-bottom:1px solid #ffffff12}.panel.open{display:block}");
            h.AppendLine(".panel textarea{width:100%;height:130px;background:#0d1117;color:#c9d1d9;border:1px solid #30363d;border-radius:8px;padding:10px;font-family:monospace;font-size:.82rem;resize:vertical}");
            h.AppendLine(".section{margin-bottom:28px;background:#11162240;border-bottom:1px solid #ffffff06;padding-bottom:12px}");
            h.AppendLine(".section-header{font-size:1.2rem;font-weight:600;color:#94a3b8;padding:16px 32px 8px;border-bottom:1px solid #ffffff0a}");
            h.AppendLine(".section-header span{color:#a855f7}");
            h.AppendLine(".section-header .section-count{color:#475569;font-size:0.95rem;font-weight:normal;margin-left:8px}");
            h.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:14px;padding:16px 32px}");
            h.AppendLine(".card{background:#161b22;border-radius:10px;overflow:hidden;border:1px solid #ffffff0f;transition:transform .2s,box-shadow .2s,border-color .2s}");
            h.AppendLine(".card:hover{transform:translateY(-4px);box-shadow:0 8px 30px #0008;border-color:#8b5cf640}");
            h.AppendLine(".card img{width:100%;height:170px;object-fit:cover;display:block;cursor:pointer}");
            h.AppendLine(".card-foot{padding:8px 10px;display:flex;align-items:center;gap:6px}");
            h.AppendLine(".card-name{font-size:.75rem;color:#8b949e;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;flex:1}");
            h.AppendLine(".cp-btn{background:#211a2d;border:1px solid #8b5cf640;color:#d8b4fe;border-radius:5px;padding:3px 8px;font-size:.7rem;font-weight:600;cursor:pointer;flex-shrink:0}");
            h.AppendLine(".cp-btn:hover{background:#2e2240}");
            h.AppendLine(".lb{display:none;position:fixed;inset:0;background:rgba(0,0,0,.9);justify-content:center;align-items:center;z-index:1000;backdrop-filter:blur(4px)}.lb.on{display:flex}");
            h.AppendLine(".lb img{max-width:92vw;max-height:88vh;border-radius:10px;box-shadow:0 0 60px #000a;object-fit:contain}");
            h.AppendLine(".lb-x{position:fixed;top:18px;right:26px;font-size:34px;color:#fff;cursor:pointer;opacity:.7}.lb-x:hover{opacity:1}");
            h.AppendLine(".lb-p{position:fixed;bottom:20px;left:50%;transform:translateX(-50%);background:#161b22cc;backdrop-filter:blur(6px);padding:8px 18px;border-radius:8px;font-size:.8rem;color:#c084fc;max-width:90vw;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}");
            h.AppendLine("</style></head><body>");
            h.AppendLine($"<div class=\"header\"><h1>ImgSeek &mdash; <span>&quot;{th}&quot;</span></h1><p>Found {cnt} matching image{mw} using Windows OCR</p></div>");
            h.AppendLine($"<div class=\"toolbar\"><button class=\"btn btn-copy\" onclick=\"copyAll()\">&#128190; Copy All Photos</button><button class=\"btn btn-dl\" onclick=\"downloadAll()\">&#128229; Download All</button><button class=\"btn btn-paths\" id=\"btnP\" onclick=\"togglePaths()\">&#128196; Show File Paths</button><span class=\"badge\">{cnt} match{me}</span></div>");
            h.AppendLine("<div class=\"panel\" id=\"panel\"><textarea id=\"ptxt\" readonly></textarea></div>");
            h.AppendLine("<div id=\"sections\"></div>");
            h.AppendLine("<div class=\"lb\" id=\"lb\" onclick=\"closeLb(event)\"><span class=\"lb-x\" onclick=\"closeLb()\">&#10005;</span><img id=\"lbImg\" src=\"\" alt=\"\"><div class=\"lb-p\" id=\"lbP\"></div></div>");
            h.AppendLine("<div class=\"toast\" id=\"toast\"></div>");
            // reze ma queen 🥀
            h.AppendLine("<script>");
            h.AppendLine("function escHtml(s){return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/\"/g,'&quot;');}");
            h.AppendLine($"var GROUPS={groupsJson};var BAT='{bj}';");
            h.AppendLine("var container=document.getElementById('sections');");
            h.AppendLine("var globalIndex=0; var allUris=[]; var allPaths=[];");
            h.AppendLine("for(var groupName in GROUPS){");
            h.AppendLine("  if(!GROUPS.hasOwnProperty(groupName)) continue;");
            h.AppendLine("  var group = GROUPS[groupName];");
            h.AppendLine("  if(group.uris.length === 0) continue;");
            h.AppendLine("  var sec = document.createElement('div'); sec.className='section';");
            h.AppendLine("  var header = document.createElement('h2'); header.className='section-header';");
            h.AppendLine("  header.innerHTML = '🔑 Keyword: <span>' + escHtml(groupName) + '</span> <span class=\"section-count\">(' + group.uris.length + ' match' + (group.uris.length===1?'':'es') + ')</span>';");
            h.AppendLine("  sec.appendChild(header);");
            h.AppendLine("  var grid = document.createElement('div'); grid.className='grid';");
            h.AppendLine("  group.uris.forEach(function(uri,i){");
            h.AppendLine("    var path = group.paths[i];");
            h.AppendLine("    var f = path.split(/[\\\\/]/).pop();");
            h.AppendLine("    var localIndex = globalIndex++;");
            h.AppendLine("    allUris.push(uri); allPaths.push(path);");
            h.AppendLine("    var c = document.createElement('div'); c.className='card';");
            h.AppendLine("    var im = document.createElement('img'); im.src=uri; im.alt=f; im.loading='lazy';");
            h.AppendLine("    im.onclick = (function(idx){ return function(){ openLb(idx); }; })(localIndex);");
            h.AppendLine("    var ft = document.createElement('div'); ft.className='card-foot';");
            h.AppendLine("    var nm = document.createElement('span'); nm.className='card-name'; nm.title=path; nm.textContent=f;");
            h.AppendLine("    var cb = document.createElement('button'); cb.className='cp-btn'; cb.textContent='Copy path';");
            h.AppendLine("    cb.onclick = (function(p){ return function(e){ e.stopPropagation(); navigator.clipboard.writeText(p).then(function(){ toast('Path copied!'); }); }; })(path);");
            h.AppendLine("    ft.appendChild(nm); ft.appendChild(cb); c.appendChild(im); c.appendChild(ft); grid.appendChild(c);");
            h.AppendLine("  });");
            h.AppendLine("  sec.appendChild(grid); container.appendChild(sec);");
            h.AppendLine("}");
            h.AppendLine("function openLb(i){document.getElementById('lbImg').src=allUris[i];document.getElementById('lbP').textContent=allPaths[i];document.getElementById('lb').classList.add('on');}");
            h.AppendLine("function closeLb(e){if(!e||e.target===document.getElementById('lb')||e.target.classList.contains('lb-x')){document.getElementById('lb').classList.remove('on');document.getElementById('lbImg').src='';}}");
            h.AppendLine("document.addEventListener('keydown',function(e){if(e.key==='Escape')closeLb();});");
            h.AppendLine("function togglePaths(){var p=document.getElementById('panel'),b=document.getElementById('btnP');if(p.classList.toggle('open')){document.getElementById('ptxt').value=allPaths.join('\\r\\n');b.textContent='Hide File Paths';}else{b.textContent='Show File Paths';}}");
            h.AppendLine("function copyAll(){var base=location.href.replace(/\\?.*/,'');var dir=base.substring(0,base.lastIndexOf('/')+1);var a=document.createElement('a');a.href=dir+BAT.replace(/ /g,'%20');a.click();toast('Opening copy tool...');}");
            h.AppendLine("function downloadAll(){if(allUris.length===0)return;allUris.forEach(function(uri,i){setTimeout(function(){var f=allPaths[i].split(/[\\\\/]/).pop();var a=document.createElement('a');a.href=uri;a.download=f;document.body.appendChild(a);a.click();document.body.removeChild(a);},i*150);});toast('Downloading '+allUris.length+' photo(s)...');}");
            h.AppendLine("var tt;function toast(m){var t=document.getElementById('toast');t.textContent=m;t.classList.add('show');clearTimeout(tt);tt=setTimeout(function(){t.classList.remove('show');},2500);}");
            h.AppendLine("</script></body></html>");
            return h.ToString();
        }

        public static string BuildCopyBat(List<string> matches, string searchTerm)
        {
            var sb = new StringBuilder();
            sb.AppendLine("@echo off\nchcp 65001 >nul\ntitle ImgSeek - Copy matched photos");
            
            // Sanitize search term to prevent batch file command injection
            string safeSearchTerm = searchTerm.Replace("\"", "").Replace("%", "%%").Replace("&", "^&");
            sb.AppendLine($"echo Found {matches.Count} photo(s) matching \"{safeSearchTerm}\".\necho.");
            sb.AppendLine("set /p \"DEST=Enter destination folder to copy photos into: \"\necho.");
            sb.AppendLine("if not exist \"%DEST%\" (mkdir \"%DEST%\"\necho Created folder: %DEST%\n)");
            sb.AppendLine($"echo Copying {matches.Count} file(s)...\necho.");
            foreach (var p in matches)
            {
                // Sanitize match paths to prevent quote escaping/injection
                string safePath = p.Replace("\"", "").Replace("%", "%%");
                sb.AppendLine($"copy /Y \"{safePath}\" \"%DEST%\\\"");
            }
            sb.AppendLine($"echo.\necho Done! {matches.Count} file(s) copied to \"%DEST%\"\necho.\npause");
            return sb.ToString();
        }

        // reze ma queen 🥀
        public static string SanitizeFileName(string name) =>
            string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim();

        private static string EscHtml(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        private static string EscJs(string s) =>
            s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
    }
}
