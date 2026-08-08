# PhotoSearch PDF

Windows-приложение, которое превращает папку с фотографиями в один PDF с невидимым OCR-слоем. Текст в PDF можно искать, выделять и извлекать. Рядом автоматически создаются Markdown, plain text и JSON для будущей загрузки в LLM/RAG.

## Возможности

- полностью локальная обработка без API-ключей и отправки фотографий в облако;
- OCR на русском, английском или сразу на двух языках;
- JPG, JPEG, PNG, BMP, TIFF и WebP;
- естественная сортировка файлов: `page2.jpg` идёт перед `page10.jpg`;
- опциональная обработка подпапок;
- один searchable PDF с оригинальным изображением каждой страницы;
- sidecar-файлы `.md`, `.txt` и `.ocr.json` с разделением по страницам;
- drag-and-drop папки, отмена процесса и запуск с путём в командной строке.

## Скачать и запустить

1. Откройте [последний Release](../../releases/latest).
2. Скачайте `PhotoSearchPdf-v1.0.0-win-x64.zip`.
3. Распакуйте архив и запустите `PhotoSearchPdf.exe`.
4. Выберите папку, язык OCR и нажмите **Создать searchable PDF**.

Сборка self-contained: устанавливать .NET не нужно. Для нативного OCR требуется Microsoft Visual C++ 2015–2022 Redistributable, который уже установлен на большинстве актуальных Windows-систем.

Путь можно передать при запуске:

```powershell
PhotoSearchPdf.exe "C:\Scans\Contract" --lang rus+eng
```

## Что создаётся

Для `Contract-searchable.pdf` приложение также создаёт:

- `Contract-searchable.md` — удобный LLM-ready Markdown с границами страниц;
- `Contract-searchable.txt` — чистый текст;
- `Contract-searchable.ocr.json` — текст, размеры страниц и нормализованные координаты строк.

Такой набор уже можно загрузить в ChatGPT/Claude или проиндексировать в локальной RAG-системе. Следующий логичный этап — встроенный чат по документу с цитатами на номера страниц.

## Почему не Firecrawl OCR

[Firecrawl `pdf-inspector`](https://github.com/firecrawl/pdf-inspector) — хороший инструмент для быстрой классификации PDF и извлечения Markdown из PDF, где текст уже существует. Он намеренно не делает OCR. PhotoSearch PDF решает предыдущий этап: создаёт текстовый слой локально. В будущей document-Q&A pipeline `pdf-inspector` можно использовать как быстрый валидатор и extractor перед chunking/embedding.

## Разработка

Требования: Windows и .NET 8 SDK.

```powershell
dotnet test PhotoSearchPdf.slnx --configuration Release
dotnet build src\PhotoSearchPdf.App\PhotoSearchPdf.App.csproj --configuration Release
```

Сборка релизного ZIP:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package-release.ps1 -Version 1.0.0
```

Архитектура:

- `PhotoSearchPdf.Core` — discovery, Tesseract OCR, PDF text layer, sidecars, conversion pipeline;
- `PhotoSearchPdf.App` — WPF UI;
- `PhotoSearchPdf.Tests` — unit и end-to-end OCR/PDF tests.

## Privacy

Приложение не выполняет сетевые запросы во время работы. Пути, фотографии и распознанный текст остаются на компьютере пользователя.

## License

MIT. Сведения о сторонних компонентах — в [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
