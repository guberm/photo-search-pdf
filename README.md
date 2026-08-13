# PhotoSearch PDF

PhotoSearch PDF is a Windows app that turns a folder of photos into one PDF with an invisible OCR text layer. The resulting PDF can be searched, selected, and copied. A second tab lets you ask questions about the document through your ChatGPT subscription, without an API key or separate API billing.

## Features

- local OCR and PDF creation without uploading the photos;
- Russian, English, or combined Russian + English OCR;
- automatic correction of sideways and upside-down photos before OCR;
- JPG, JPEG, PNG, BMP, TIFF, and WebP input;
- natural file ordering, so `page2.jpg` comes before `page10.jpg`;
- optional subfolder processing;
- one searchable PDF with the original image on every page;
- `.md`, `.txt`, and `.ocr.json` sidecars with page boundaries;
- built-in document Q&A through the official Codex CLI and ChatGPT sign-in;
- direct Q&A for any PDF that already contains searchable text, without requiring a sidecar file;
- automatic installation of the official Codex CLI through Windows Package Manager when it is missing;
- grounded answers with page citations such as `[page 12]`;
- local relevance selection for documents that are too large to send in full;
- folder drag-and-drop, cancellation, and command-line path support.

## Download and run

1. Open the [latest Release](../../releases/latest).
2. Download `PhotoSearchPdf-v1.1.3-win-x64.zip`.
3. Extract the ZIP and run `PhotoSearchPdf.exe`.
4. Choose a photo folder and OCR language, then select **Create searchable PDF**.

The release is self-contained, so .NET does not need to be installed. Native OCR requires the Microsoft Visual C++ 2015–2022 Redistributable, which is already present on most current Windows systems.

You can also pass a folder when starting the app:

```powershell
PhotoSearchPdf.exe "C:\Scans\Contract" --lang rus+eng
```

Pass any searchable PDF to open the Q&A tab directly:

```powershell
PhotoSearchPdf.exe "C:\Scans\Contract\Contract-searchable.pdf"
```

## Ask questions with a ChatGPT subscription

PhotoSearch PDF does not use an OpenAI API key. It uses the official [Codex CLI or Codex desktop app](https://learn.chatgpt.com/docs/codex/cli). No terminal commands are required:

1. Open **Ask the document**.
2. Select **Connect OpenAI**.
3. If Codex is missing, approve the automatic installation of the official `OpenAI.Codex` package through Windows Package Manager (`winget`).
4. Complete the ChatGPT sign-in in your browser.
5. After the app shows **Connected as your-email@example.com (ChatGPT plan)**, choose any searchable PDF, enter a question, and select **Ask question**.

If you skip setup and select **Ask question**, the same installation and sign-in wizard starts automatically. On the uncommon Windows system without `winget`, the app opens the official OpenAI installation guide.

When a matching `.ocr.json` sidecar exists, the app uses its high-quality OCR page data. Otherwise, it extracts the embedded text from the PDF itself. If an external PDF is an image-only scan with no text layer, use **Create PDF** to run OCR first.

The connection panel displays the ChatGPT account email and plan returned by the official Codex account interface. Select **Disconnect** to sign Codex out after confirming that this also affects other Codex apps in the same Windows account.

The app checks `codex login status` and accepts only ChatGPT sign-in. API-key authentication is intentionally rejected. Requests use official `codex exec` in read-only and ephemeral mode, with user rules, plugins, and MCP disabled for the run. OpenAI documents [ChatGPT sign-in as subscription access](https://learn.chatgpt.com/docs/auth) and [`codex exec` as the supported non-interactive mode](https://learn.chatgpt.com/docs/non-interactive-mode).

## Generated files

For `Contract-searchable.pdf`, the app also writes:

- `Contract-searchable.md` — LLM-ready Markdown with page boundaries;
- `Contract-searchable.txt` — plain OCR text;
- `Contract-searchable.ocr.json` — OCR text, page dimensions, and normalized line coordinates.

These files can be used by the built-in Q&A workflow, uploaded to another LLM, or indexed by a local RAG system.

## Firecrawl notes

[Firecrawl `pdf-inspector`](https://github.com/firecrawl/pdf-inspector) is useful for fast PDF classification and Markdown extraction when a PDF already contains text. It intentionally does not perform OCR. PhotoSearch PDF handles the preceding stage by creating the text layer locally. In a larger document pipeline, `pdf-inspector` can serve as a validator and extractor before chunking or embedding.

## Development

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet test PhotoSearchPdf.slnx --configuration Release
dotnet build src\PhotoSearchPdf.App\PhotoSearchPdf.App.csproj --configuration Release
```

Build the release ZIP:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package-release.ps1 -Version 1.1.3
```

Architecture:

- `PhotoSearchPdf.Core` — discovery, Tesseract OCR, PDF text layer, sidecars, context selection, Codex setup, and safe Codex execution;
- `PhotoSearchPdf.App` — WPF user interface;
- `PhotoSearchPdf.Tests` — unit and end-to-end OCR/PDF tests.

## Privacy

OCR, PDF creation, PDF text extraction, and relevant-page selection are local operations. Photos and the PDF stay on the computer. Only after **Ask question** is selected are the question and selected document text sent to OpenAI through Codex. The data controls of the signed-in ChatGPT plan or workspace apply.

## License

MIT. Third-party component details are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
