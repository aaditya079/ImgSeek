# ImgSeek

![ImgSeek Repository Banner](imgseek_github_banner.png)

<div align="center">

[![Download Portable GUI](https://img.shields.io/badge/Download-ImgSeek.exe%20(Portable)-007acc?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/aaditya079/ImgSeek/releases/latest)

</div>

ImgSeek is a local Windows utility that **scans folders of images using built-in Windows hardware OCR** to find files containing specific text. It helps locate screenshots, receipts, scanned documents, or photos by searching for keywords or phrases inside them.

---

## 🌟 Key Features

*   **🎨 Dark-Mode GUI**: A clean dark-themed desktop app to browse folders, monitor scan progress in real-time, and view results.
*   **⚡ Local Hardware OCR**: Powered by the native Windows `Windows.Media.Ocr` engine. Runs entirely offline with zero internet access, zero external APIs, and no data leaks.
*   **🖼️ Real-Time Results**: Matches populate live in a grid as the folder is scanned. You can view files, copy file paths, or copy matches to another folder in bulk.
*   **📦 Portable Single-File Executable**: Built self-contained so you can copy and run the app on Windows 10 or 11 without installing any .NET runtimes or SDKs.
*   **📁 Web Gallery Export**: Export matches into a local interactive HTML gallery with a click-to-enlarge lightbox, along with a batch script to copy matched files in one click.

---

## 🚀 How to Run

ImgSeek can be run in two ways:

### 1. Interactive Desktop App (GUI Mode) 🎨
Launch the desktop application to browse folders, see real-time progress, and view image result cards.

1. Go to the [**Latest GitHub Releases**](https://github.com/aaditya079/ImgSeek/releases/latest).
2. Download and double-click the portable **`ImgSeek.exe`**.
3. Choose a folder to scan, enter your search term, and click **Scan**!
   > [!TIP]
   > The app is built as a single-file executable, meaning you can copy `ImgSeek.exe` anywhere (like your Desktop or USB drive) and run it standalone.
   > 
   > *If you prefer to compile it yourself: open the terminal in `ImgSeek-WPF/` and run `dotnet publish -c Release -r win-x64 --self-contained true`.*

### 2. Quick Command-Line Script (CLI Mode) ⚡
Run a fast scan directly from your terminal using our pre-configured helper script.

1. Navigate to the root directory of the repository.
2. Double-click or run **`SearchImagesByName.bat`** from CMD/PowerShell.
3. Enter your folder path and keyword when prompted to view search matches directly in the terminal!

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
*   **`ImgSeek-WPF/`**: The desktop application code:
    *   [MainWindow.xaml](file:///d:/ImgSeek-main/ImgSeek-main/ImgSeek-WPF/MainWindow.xaml) & [MainWindow.xaml.cs](file:///d:/ImgSeek-main/ImgSeek-main/ImgSeek-WPF/MainWindow.xaml.cs): UI layout, styling, and async search task orchestration.
    *   [OcrScannerCore.cs](file:///d:/ImgSeek-main/ImgSeek-main/ImgSeek-WPF/OcrScannerCore.cs): Core OCR scanning logic.
    *   [ImgSeek.csproj](file:///d:/ImgSeek-main/ImgSeek-main/ImgSeek-WPF/ImgSeek.csproj): Project build configuration.

---

## ⚖️ License

Distributed under the MIT License. See `LICENSE` for details.
