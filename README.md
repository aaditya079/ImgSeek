# ImgSeek

![ImgSeek Repository Banner](imgseek_github_banner.png)

A state-of-the-art Windows utility that **scans directories of images using native hardware OCR** to instantly locate files containing specific text. Perfect for finding names, phrases, or receipts inside chat screenshots, document scans, Discord logs, memes, and photos.

---

## 🎥 App Showcase

> [!TIP]
> **To add a 30-second Showcase Demo GIF here:**
> 1. Run `ImgSeek` and perform a search.
> 2. Capture your screen with a free utility like [ShareX](https://getsharex.com/) or [ScreenToGif](https://www.screentogif.com/).
> 3. Save the animation as `showcase_demo.gif` in the repository's root folder and push to GitHub! It will automatically load and display your showcase here.

*Double-click the `.exe` to open the gorgeous Fluent GUI, or run it via command line to process images in automated headless scripts.*

---

## 🌟 Key Features

*   **🎨 Stunning WinUI 3 Desktop App**: Implements Microsoft’s latest Fluent Design language natively with rounded corners, Segoe Fluent Icons, a glowing dark theme, and a gorgeous semi-transparent **Mica Backdrop** material.
*   **🔄 Intelligent Dual-Mode Engine**: 
    *   **GUI Mode**: Launched without arguments, opening a beautiful interactive window with folder browsing, cancellation support, and a live results grid.
    *   **CLI Mode**: Launched with arguments, attaching directly to the calling console/terminal (e.g. PowerShell/CMD) for headless, automated scripting and CI/CD pipelines (fully backward compatible).
*   **⚡ Hardware-Accelerated Local OCR**: Powered by the native Windows `Windows.Media.Ocr` engine. Runs entirely offline with zero internet access, zero external APIs, and zero privacy leaks.
*   **🖼️ Live Results & Interactive Cards**: As images scan in the background, matching results animate onto your screen in real-time. Copy file paths, view images directly in your system viewer, or perform bulk operations.
*   **📦 Zero-Dependency Portable Build**: Can be published as a self-contained release that runs anywhere on Windows 10 or 11 out of the box—no .NET runtimes, frameworks, or installers required.
*   **📁 Web Gallery & Copy Tools**: Export matched images as an interactive HTML gallery with a responsive click-to-enlarge lightbox and dynamic bulk copy batch scripts.

---

## 🛠️ Portfolio Strategy & Architecture

### 1. Robust Exception Management
ImgSeek is designed with resilient defensive programming to handle all potential runtime edge cases without breaking the user experience:
*   **Empty or Non-Existent Folders**: Instantly validated before scanning. Users receive a clean, friendly notification banner rather than generic crash dialogs.
*   **Permission-Denied Files**: If the system encounters locked system files or folders with denied access, it logs the warning and gracefully bypasses them to continue the scan.
*   **Corrupted or Unreadable Images**: Files with invalid headers, 0-byte sizes, or unsupported encodings are intercepted and skipped smoothly while preserving scan continuity.
*   **System OCR Absence**: If the Windows OCR engine is missing or disabled (e.g. customized headless server installs), it displays an educational setup prompt guiding the user to install a Windows Language Pack.

### 2. File Structure
*   [Program.cs](file:///d:/ImgSeek-main/ImgSeek-main/Program.cs): Dual-mode entry point with native Win32 `AttachConsole` P/Invoke mapping and WinUI 3 bootstrapper orchestration.
*   [OcrScannerCore.cs](file:///d:/ImgSeek-main/ImgSeek-main/OcrScannerCore.cs): Thread-safe scanning, OCR extraction, and HTML/BAT file export template logic.
*   [MainWindow.xaml](file:///d:/ImgSeek-main/ImgSeek-main/MainWindow.xaml) & [MainWindow.xaml.cs](file:///d:/ImgSeek-main/ImgSeek-main/MainWindow.xaml.cs): The WinUI 3 presentation layer, containing modern UI controls and async task worker thread dispatchers.
*   [App.xaml](file:///d:/ImgSeek-main/ImgSeek-main/App.xaml) & [App.xaml.cs](file:///d:/ImgSeek-main/ImgSeek-main/App.xaml.cs): The WinUI 3 application resource and window lifecycle mapping.

---

## 🚀 Getting Started

### Requirements
*   **OS**: Windows 10 (Build 17763 or later) / Windows 11
*   **Development**: [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)

### Run from Source
1.  Clone the repository and open the terminal inside the root directory.
2.  Launch the **WinUI 3 GUI**:
    ```bash
    dotnet run
    ```
3.  Launch the **Command-Line Interface**:
    ```bash
    dotnet run -- "C:\path\to\images" "search-keyword"
    ```

---

## 📦 How to Publish a Self-Contained `.exe` Release

To compile a 100% portable, optimized single-folder distribution of ImgSeek that you can zip and release on GitHub:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true
```

### What this does:
*   `-c Release`: Compiles highly optimized production-grade binaries.
*   `-r win-x64`: Targets 64-bit Windows platforms.
*   `--self-contained true`: Embeds the complete .NET runtime and Windows App SDK DLLs. The end-user **does not need to install anything** to run it.
*   `-p:PublishReadyToRun=true`: Ahead-Of-Time (AOT) compiles assembly code to native machine instructions, decreasing startup times by up to 50%!

The output will be generated in:
`bin\Release\<TargetFramework>\win-x64\publish\` (e.g., `bin\Release\net6.0-windows10.0.19041.0\win-x64\publish\`)

> [!TIP]
> Zip the contents of the `publish/` folder and upload it directly as a **GitHub Release**! It is a massive portfolio booster that makes your repository instantly usable by anyone, not just developers.

---

## ⚖️ License

Distributed under the MIT License. See `LICENSE` for details.
