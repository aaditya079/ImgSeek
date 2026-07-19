# ImgSeek

![ImgSeek Repository Banner](imgseek_github_banner.png)

<div align="center">

[![Download Portable GUI](https://img.shields.io/badge/Download-ImgSeek.exe%20(Portable)-007acc?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/aaditya079/ImgSeek/releases/latest)

</div>

ImgSeek is a local Windows utility that **scans folders of images using built-in Windows hardware OCR** to find files containing specific text. It helps locate screenshots, receipts, scanned documents, or photos by searching for keywords or phrases inside them.

---

## 🌟 Key Features

*   **🎨 Premium Purple Theme**: A gorgeous, modern dark-mode desktop app styled with vibrant purple/lilac and violet accents, featuring smooth scale-hover transitions and type-colored badges.
*   **🔑 Grouped Multiple Keywords**: Enter multiple search keywords separated by commas `,` or semicolons `;`. ImgSeek runs scans and visually separates the results into distinct, labeled sections (e.g. `Aadi` and `nyte` appear in their own rows).
*   **🧠 AND/OR Matching Modes**: Toggle between searching for *any* keyword (OR logic) or requiring *all* keywords to match (AND logic).
*   **⚡ Local Hardware OCR**: Powered by the native Windows `Windows.Media.Ocr` engine. Runs entirely offline with zero internet access, zero external APIs, and no data leaks.
*   **🖼️ Real-Time Results**: Matches populate live in a grid as the folder is scanned. You can view files, copy file paths, or copy matches to another folder in bulk.
*   **📁 Web Gallery Export**: Export matches into a local interactive HTML gallery (which preserves your keyword groupings, has a click-to-enlarge lightbox, and includes a batch script to copy matched files in one click).
*   **🔒 Security Hardened**: Safeguarded against ReDoS (Regular Expression Denial of Service) via strict timeout policies, and shielded against command injection by sanitizing all shell operators and quotes.
*   **📦 Portable Single-File Executable**: Built self-contained so you can copy and run the app on Windows 10 or 11 without installing any .NET runtimes or SDKs.

---

## 🚀 How to Run

ImgSeek can be run in two ways:

### 1. Interactive Desktop App (GUI Mode) 🎨
Launch the desktop application to browse folders, see real-time progress, and view image result cards.

1. Go to the [**Latest GitHub Releases**](https://github.com/aaditya079/ImgSeek/releases/latest).
2. Download and double-click the portable **`ImgSeek.exe`**.
3. Choose a folder to scan, enter your search keywords (separated by commas or semicolons), and click **Scan**!
   > [!TIP]
   > The app is built as a single-file executable, meaning you can copy `ImgSeek.exe` anywhere (like your Desktop or USB drive) and run it standalone.
   > 
   > *If you prefer to compile it yourself: open the terminal in the root directory and run `dotnet publish -c Release -r win-x64 --self-contained true`.*

### 2. Quick Command-Line Script (CLI Mode) ⚡
Run a fast scan directly from your terminal using our pre-configured helper script or the executable directly.

1. Navigate to the root directory of the repository.
2. Double-click or run **`SearchImagesByName.bat`** from CMD/PowerShell.
3. Enter your folder path and keyword when prompted to view search matches directly in the terminal!
   *   *CLI Arguments*:
       *   `-d`, `--dir`: Semi-colon separated folder paths to scan.
       *   `-s`, `--search`: Search term (comma or semicolon separated keywords).
       *   `-a`, `--match-all`: Require all keywords to match (AND logic).
       *   `-c`, `--case`: Enable case-sensitive matching.
       *   `-r`, `--regex`: Treat search term as a regular expression.
       *   `-o`, `--output`: Path to write the exported HTML gallery.

---

## 🛠️ Design & Resilience

### 1. Error Handling
The application handles common runtime issues gracefully without crashing or interrupting scans:
*   **Empty or Non-Existent Folders**: Instantly validated before scanning. Users receive a clean notification banner rather than generic crash dialogs.
*   **Permission-Denied Files**: If the system encounters locked system files or folders with denied access, it logs a warning and bypasses them to continue the scan.
*   **Corrupted or Unreadable Images**: Files with invalid headers, 0-byte sizes, or unsupported encodings are skipped smoothly while preserving scan continuity.
*   **System OCR Absence**: If the Windows OCR engine is missing or disabled, it displays a prompt guiding the user to install a Windows Language Pack.

### 2. Project File Structure
*   **`SearchImagesByName.bat`**: Convenient command-line entry script that runs console scans.
*   **`Program.cs`**: Hybrid application entry point that detects CLI arguments, handles terminal output, or boots the GUI.
*   **`App.xaml` & `App.xaml.cs`**: WPF application startup configuration.
*   **`MainWindow.xaml` & `MainWindow.xaml.cs`**: GUI layout, Purple dark theme styling, and async search task orchestration.
*   **`OcrScannerCore.cs`**: Shared core OCR engine scanning logic and helper methods to build HTML galleries.
*   **`ImgSeek.csproj`**: Project build configuration.

---

## ⚖️ License

Distributed under the MIT License. See `LICENSE` for details.
