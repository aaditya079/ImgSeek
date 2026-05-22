# ImgSeek

A Windows tool that **scans any folder of images using OCR** (Windows built-in OCR engine) and finds all photos where a given name or keyword appears — in chat screenshots, Discord messages, captions, etc.

## Features

- 🔍 Search any name/keyword across thousands of images instantly
- 🖼️ Auto-generates a **beautiful HTML gallery** of all matches
- ⚡ Uses Windows' native OCR engine — no API keys or internet required
- 📁 Works on any folder, supports PNG, JPG, JPEG, WEBP, BMP

## Requirements

- Windows 10/11
- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)

## Usage

### Option 1 — Double-click launcher

Run `SearchImagesByName.bat` and follow the prompts:

```
Enter folder path to scan: C:\Users\you\Pictures
Enter name to search for: john
```

A gallery will automatically open in your browser showing all matched images.

### Option 2 — Command line

```bash
dotnet run --project OcrScanner.csproj -c Release -- "C:\path\to\images" "name" "output.html"
```

| Argument | Description |
|----------|-------------|
| `C:\path\to\images` | Folder to scan (searched recursively) |
| `name` | Name/keyword to look for inside images |
| `output.html` | *(Optional)* Path to save the HTML gallery |

### 📁 Output Location

By default, the generated HTML gallery and the companion `_CopyFiles.bat` script are saved to your Windows Temporary folder:
`C:\Users\<YourUsername>\AppData\Local\Temp\`

* **Quick Access**: Press `Win + R`, type `%temp%`, and hit **Enter** to open the directory directly.
* **Custom Location**: You can optionally specify a custom path for the HTML file as the 3rd command-line argument. The `_CopyFiles.bat` file will be generated in that same custom directory.

## How it works

1. Recursively finds all image files in the given folder
2. Runs each image through the **Windows.Media.Ocr** engine
3. Checks if the extracted text contains your search term
4. Generates an HTML gallery with click-to-enlarge lightbox for all matches

## Building

```bash
dotnet build OcrScanner.csproj -c Release
```
