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
                var storageFile = await StorageFile.GetFileFromPathAsync(imgPath);
                using var stream = await storageFile.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var bitmap  = await decoder.GetSoftwareBitmapAsync();
                var result  = await engine.RecognizeAsync(bitmap);
                string text = result.Text.ToLower();

                if (text.Contains(termLower))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ MATCH");
                    Console.ResetColor();
                    matches.Add(imgPath);
                }
                else
                {
                    Console.WriteLine();
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("[skip]");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"=== Done! Found {matches.Count} match(es) for \"{searchTerm}\" ===");
        Console.ResetColor();

        // Write matches to stdout as paths (one per line), preceded by a separator
        Console.WriteLine("---MATCHES---");
        foreach (var m in matches)
            Console.WriteLine(m);

        // Generate HTML gallery if output path given
        if (!string.IsNullOrWhiteSpace(outputHtml))
            WriteGallery(outputHtml, searchTerm, matches);

        return 0;
    }

    static void WriteGallery(string path, string term, List<string> matches)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'>");
        sb.AppendLine($"<title>Gallery — \"{term}\" ({matches.Count} results)</title>");
        sb.AppendLine(@"<style>
*{margin:0;padding:0;box-sizing:border-box}
body{background:#0f0f13;color:#eee;font-family:'Segoe UI',sans-serif;padding:24px}
h1{font-size:1.6rem;font-weight:700;margin-bottom:4px;color:#fff}
p.sub{color:#888;font-size:.9rem;margin-bottom:24px}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:14px}
.card{background:#1a1a23;border-radius:10px;overflow:hidden;border:1px solid #2a2a35;cursor:pointer;transition:transform .15s,border-color .15s}
.card:hover{transform:translateY(-3px);border-color:#6c63ff}
.card img{width:100%;height:200px;object-fit:cover;display:block;background:#111}
.card .label{padding:8px 12px;font-size:.72rem;color:#aaa;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.card .label span{color:#6c63ff;font-weight:600}
#lb{display:none;position:fixed;inset:0;background:rgba(0,0,0,.92);z-index:999;align-items:center;justify-content:center;flex-direction:column;gap:12px}
#lb.active{display:flex}
#lb img{max-width:90vw;max-height:85vh;border-radius:8px;box-shadow:0 0 40px #0008}
#lb .lb-name{color:#aaa;font-size:.8rem}
#lb .close{position:fixed;top:16px;right:20px;font-size:2rem;cursor:pointer;color:#fff;line-height:1}
#lb .nav{display:flex;gap:20px}
#lb .nav button{background:#2a2a35;border:none;color:#fff;padding:8px 20px;border-radius:6px;cursor:pointer;font-size:1rem}
#lb .nav button:hover{background:#6c63ff}
.empty{text-align:center;padding:60px;color:#555;font-size:1.1rem}
</style></head><body>");
        sb.AppendLine($"<h1>🔍 Results for \"{term}\"</h1>");
        sb.AppendLine($"<p class='sub'>{matches.Count} image(s) found containing this name via OCR</p>");

        if (matches.Count == 0)
        {
            sb.AppendLine("<div class='empty'>No matches found.</div>");
        }
        else
        {
            sb.AppendLine("<div class='grid' id='grid'></div>");
            sb.AppendLine("<div id='lb'><span class='close' onclick='closeLb()'>✕</span>");
            sb.AppendLine("<img id='lb-img' src='' alt=''><div class='lb-name' id='lb-name'></div>");
            sb.AppendLine("<div class='nav'><button onclick='navLb(-1)'>◀ Prev</button><button onclick='navLb(1)'>Next ▶</button></div></div>");
            sb.AppendLine("<script>");
            sb.AppendLine("const images=[");
            foreach (var m in matches)
                sb.AppendLine($"  {System.Text.Json.JsonSerializer.Serialize(m)},");
            sb.AppendLine("];");
            sb.AppendLine(@"
let cur=0;
const grid=document.getElementById('grid');
images.forEach((p,i)=>{
  const name=p.split('\\').pop();
  const src='file:///'+p.replace(/\\/g,'/');
  const card=document.createElement('div');
  card.className='card';
  card.innerHTML=`<img src='${src}' loading='lazy'><div class='label'>#${i+1} — <span>${name}</span></div>`;
  card.onclick=()=>openLb(i);
  grid.appendChild(card);
});
function openLb(i){cur=i;const p=images[i];document.getElementById('lb-img').src='file:///'+p.replace(/\\/g,'/');document.getElementById('lb-name').textContent=p.split('\\').pop();document.getElementById('lb').classList.add('active');}
function closeLb(){document.getElementById('lb').classList.remove('active');}
function navLb(d){cur=(cur+d+images.length)%images.length;openLb(cur);}
document.addEventListener('keydown',e=>{if(e.key==='Escape')closeLb();if(e.key==='ArrowRight')navLb(1);if(e.key==='ArrowLeft')navLb(-1);});
</script>");
        }

        sb.AppendLine("</body></html>");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"Gallery saved → {path}");
    }
}
