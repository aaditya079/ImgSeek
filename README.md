# ImgSeek

![ImgSeek Repository Banner](imgseek_github_banner.png)

<div align="center">

[![Download Portable GUI](https://img.shields.io/badge/Download-ImgSeek.exe%20(Portable)-007acc?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/aaditya079/ImgSeek/releases/latest)

</div>

A state-of-the-art Windows utility that **scans directories of images using native hardware OCR** to instantly locate files containing specific text. Perfect for finding names, phrases, or receipts inside chat screenshots, document scans, Discord logs, memes, and photos.

---

## 🌟 Key Features

*   **🎨 Stunning Dark-Mode Desktop App**: Implements a premium, modern design with a deep navy gradient palette, smooth transition animations, vertical layout alignment, and interactive image hover effects.
*   **⚡ Hardware-Accelerated Local OCR**: Powered by the native Windows `Windows.Media.Ocr` engine. Runs entirely offline with zero internet access, zero external APIs, and zero privacy leaks.
*   **🖼️ Real-Time Animated Results**: Matching images animate onto the screen in real-time as the scan progresses. Copy file paths, open files instantly in your system viewer, or perform bulk actions.
*   **📦 Zero-Dependency Portable Build**: Compiled as a fully self-contained single-file executable (`PublishSingleFile`). It runs instantly on any Windows 10 or 11 system out of the box—no extra runtimes, SDKs, or installations required.
*   **📁 Web Gallery & Copy Tools**: Export matched images as an interactive HTML gallery with a responsive click-to-enlarge lightbox and dynamic bulk copy batch scripts.

---

## 🚀 How to Run (Getting Started)

ImgSeek can be accessed and run in two extremely simple ways:

### Step 1: Interactive Desktop App (GUI Mode) 🎨
Launch the modern desktop application to browse folders, see real-time progress, and view image result cards interactively.

1. Go to the [**Latest GitHub Releases**](https://github.com/aaditya079/ImgSeek/releases/latest).
2. Download and double-click the portable **`ImgSeek.exe`**.
3. Choose a folder to scan, enter your search term, and click **Scan**!
   > [!TIP]
   > The app is built as a single-file executable, meaning you can copy `ImgSeek.exe` anywhere (like your Desktop or USB drive) and run it standalone.
   > 
   > *If you prefer to compile it yourself: open terminal in `ImgSeek-WPF/` and run `dotnet publish -c Release -r win-x64 --self-contained true`.*

### Step 2: Quick Command-Line Script (CLI Mode) ⚡
Run a fast, lightweight scan directly from your terminal using our pre-configured helper script.

1. Navigate to the root directory.
2. Double-click or run **`SearchImagesByName.bat`** from CMD/PowerShell.
3. Enter your folder path and keyword when prompted to view search matches directly in the terminal!

---

## 🛠️ Architecture & Robust Design

### 1. Robust Exception Management
ImgSeek is designed with resilient defensive programming to handle all potential runtime edge cases without breaking the user experience:
*   **Empty or Non-Existent Folders**: Instantly validated before scanning. Users receive a clean, friendly notification banner rather than generic crash dialogs.
*   **Permission-Denied Files**: If the system encounters locked system files or folders with denied access, it logs the warning and gracefully bypasses them to continue the scan.
*   **Corrupted or Unreadable Images**: Files with invalid headers, 0-byte sizes, or unsupported encodings are intercepted and skipped smoothly while preserving scan continuity.
*   **System OCR Absence**: If the Windows OCR engine is missing or disabled, it displays an educational setup prompt guiding the user to install a Windows Language Pack.

### 2. Project File Structure
*   **`SearchImagesByName.bat`**: Convenient command-line entry script that runs console scans.
*   **`ImgSeek-WPF/`**: The modern desktop application codebase:
    *   [MainWindow.xaml](file:///d:/ImgSeek-main/ImgSeek-main/ImgSeek-WPF/MainWindow.xaml) & [MainWindow.xaml.cs](file:///d:/ImgSeek-main/ImgSeek-main/ImgSeek-WPF/MainWindow.xaml.cs): Premium UI design, styling, and async task orchestrations.
    *   [OcrScannerCore.cs](file:///d:/ImgSeek-main/ImgSeek-main/ImgSeek-WPF/OcrScannerCore.cs): High-performance thread-safe offline OCR scanner.
    *   [ImgSeek.csproj](file:///d:/ImgSeek-main/ImgSeek-main/ImgSeek-WPF/ImgSeek.csproj): Single-file self-contained compilation configuration.

---

## ⚖️ License

Distributed under the MIT License. See `LICENSE` for details.
