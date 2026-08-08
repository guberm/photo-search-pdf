# PhotoSearch PDF

Windows-приложение, которое превращает папку с фотографиями в один PDF с невидимым OCR-слоем. Текст в PDF можно искать, выделять и извлекать. Во второй вкладке можно задавать вопросы по документу через подписку ChatGPT — без API key и отдельного API-биллинга.

## Возможности

- полностью локальные OCR и создание PDF без отправки фотографий в облако;
- OCR на русском, английском или сразу на двух языках;
- JPG, JPEG, PNG, BMP, TIFF и WebP;
- естественная сортировка файлов: `page2.jpg` идёт перед `page10.jpg`;
- опциональная обработка подпапок;
- один searchable PDF с оригинальным изображением каждой страницы;
- sidecar-файлы `.md`, `.txt` и `.ocr.json` с разделением по страницам;
- встроенные вопросы по документу через официальный Codex CLI и вход ChatGPT Subscription;
- ответы с обязательными ссылками вида `[стр. 12]`;
- для больших документов локально выбираются наиболее релевантные страницы;
- drag-and-drop папки, отмена процесса и запуск с путём в командной строке.

## Скачать и запустить

1. Откройте [последний Release](../../releases/latest).
2. Скачайте `PhotoSearchPdf-v1.1.0-win-x64.zip`.
3. Распакуйте архив и запустите `PhotoSearchPdf.exe`.
4. Выберите папку, язык OCR и нажмите **Создать searchable PDF**.

Сборка self-contained: устанавливать .NET не нужно. Для нативного OCR требуется Microsoft Visual C++ 2015–2022 Redistributable, который уже установлен на большинстве актуальных Windows-систем.

## Вопросы через подписку ChatGPT

PhotoSearch PDF не использует OpenAI API key. Для вопросов нужен установленный [Codex CLI или Codex desktop app](https://learn.chatgpt.com/docs/codex/cli), доступный как команда `codex`.

1. Откройте вкладку **Вопросы к документу**.
2. Нажмите **Войти через ChatGPT** и завершите вход в браузере. Если вход уже выполнен, приложение покажет «Подключено через подписку ChatGPT».
3. Выберите созданный приложением PDF, введите вопрос и нажмите **Задать вопрос**.

Приложение проверяет `codex login status` и принимает только вход через ChatGPT. Авторизация API key намеренно не используется. Вызов выполняется через официальный `codex exec` в read-only и ephemeral режиме; пользовательские правила, плагины и MCP отключаются для этого запроса. OpenAI официально описывает [вход через ChatGPT как subscription access](https://learn.chatgpt.com/docs/auth) и [`codex exec` как режим автоматизации](https://learn.chatgpt.com/docs/non-interactive-mode).

Путь можно передать при запуске:

```powershell
PhotoSearchPdf.exe "C:\Scans\Contract" --lang rus+eng
```

Можно сразу открыть вкладку вопросов, передав созданный PDF:

```powershell
PhotoSearchPdf.exe "C:\Scans\Contract\Contract-searchable.pdf"
```

## Что создаётся

Для `Contract-searchable.pdf` приложение также создаёт:

- `Contract-searchable.md` — удобный LLM-ready Markdown с границами страниц;
- `Contract-searchable.txt` — чистый текст;
- `Contract-searchable.ocr.json` — текст, размеры страниц и нормализованные координаты строк.

Такой набор можно использовать во встроенных вопросах, загрузить в ChatGPT/Claude или проиндексировать в локальной RAG-системе.

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
powershell -ExecutionPolicy Bypass -File scripts\package-release.ps1 -Version 1.1.0
```

Архитектура:

- `PhotoSearchPdf.Core` — discovery, Tesseract OCR, PDF text layer, sidecars, выбор контекста и безопасный запуск Codex;
- `PhotoSearchPdf.App` — WPF UI;
- `PhotoSearchPdf.Tests` — unit и end-to-end OCR/PDF tests.

## Privacy

OCR, создание PDF и локальный выбор релевантных страниц не выполняют сетевых запросов. Фотографии и PDF остаются на компьютере. Только после нажатия **Задать вопрос** выбранный OCR-текст и вопрос отправляются в OpenAI через Codex; применяются настройки хранения данных вашего ChatGPT workspace/плана.

## License

MIT. Сведения о сторонних компонентах — в [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
