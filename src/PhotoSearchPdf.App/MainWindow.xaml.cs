using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PhotoSearchPdf.Core;

namespace PhotoSearchPdf.App;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cancellation;
    private string? _completedPdf;

    public MainWindow() => InitializeComponent();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var folder = args.FirstOrDefault(Directory.Exists);
        if (folder is not null) InputFolderTextBox.Text = Path.GetFullPath(folder);

        var languageIndex = Array.FindIndex(args, arg => arg.Equals("--lang", StringComparison.OrdinalIgnoreCase));
        if (languageIndex < 0 || languageIndex + 1 >= args.Length) return;
        foreach (var item in LanguageComboBox.Items.OfType<ComboBoxItem>())
        {
            if (Equals(item.Tag, args[languageIndex + 1])) LanguageComboBox.SelectedItem = item;
        }
    }

    private void BrowseInput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Выберите папку с фотографиями" };
        if (Directory.Exists(InputFolderTextBox.Text)) dialog.InitialDirectory = InputFolderTextBox.Text;
        if (dialog.ShowDialog(this) == true) InputFolderTextBox.Text = dialog.FolderName;
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Куда сохранить searchable PDF",
            Filter = "PDF document (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = Path.GetFileName(OutputPdfTextBox.Text)
        };
        var directory = Path.GetDirectoryName(OutputPdfTextBox.Text);
        if (Directory.Exists(directory)) dialog.InitialDirectory = directory;
        if (dialog.ShowDialog(this) == true) OutputPdfTextBox.Text = dialog.FileName;
    }

    private void InputFolderTextBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshFolderSummary();

    private void RecursiveCheckBox_Changed(object sender, RoutedEventArgs e) => RefreshFolderSummary();

    private void RefreshFolderSummary()
    {
        var folder = InputFolderTextBox?.Text.Trim().Trim('"') ?? string.Empty;
        if (!Directory.Exists(folder))
        {
            if (ImageCountTextBlock is not null) ImageCountTextBlock.Text = "Папка не найдена";
            return;
        }

        try
        {
            var count = ImageDiscovery.FindImages(folder, RecursiveCheckBox?.IsChecked == true).Count;
            ImageCountTextBlock.Text = $"Найдено изображений: {count}";
            OutputPdfTextBox.Text = OutputPaths.ResolvePdfPath(folder);
        }
        catch (Exception error)
        {
            ImageCountTextBlock.Text = error.Message;
        }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var folder = InputFolderTextBox.Text.Trim().Trim('"');
        var output = OutputPdfTextBox.Text.Trim().Trim('"');
        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this, "Выберите существующую папку с фотографиями.", "PhotoSearch PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(output)) output = OutputPaths.ResolvePdfPath(folder);
        if (!output.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) output += ".pdf";
        if (File.Exists(output) && MessageBox.Show(this,
                "Этот PDF уже существует. Перезаписать его?",
                "Подтверждение перезаписи",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        var language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "rus+eng";

        _cancellation = new CancellationTokenSource();
        SetRunning(true);
        LogTextBox.Clear();
        AppendLog($"Input: {folder}");
        AppendLog($"Output: {output}");
        AppendLog($"OCR: {language}");

        try
        {
            using var converter = new PhotoPdfConverter(Path.Combine(AppContext.BaseDirectory, "tessdata"));
            var result = await converter.ConvertAsync(
                new ConversionOptions(folder, output, language, RecursiveCheckBox.IsChecked == true),
                new Progress<ConversionProgress>(UpdateProgress),
                _cancellation.Token);

            _completedPdf = result.PdfPath;
            OpenButton.IsEnabled = true;
            ProgressBar.Value = ProgressBar.Maximum;
            StatusTextBlock.Text = $"Готово: {result.PageCount} страниц";
            AppendLog($"PDF: {result.PdfPath}");
            AppendLog($"Markdown: {result.Sidecars.Markdown}");
            AppendLog("OCR-поиск и sidecar-файлы созданы.");
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Операция отменена";
            AppendLog("Отменено пользователем.");
        }
        catch (Exception error)
        {
            StatusTextBlock.Text = "Ошибка";
            AppendLog(error.ToString());
            MessageBox.Show(this, error.Message, "Не удалось создать PDF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            SetRunning(false);
        }
    }

    private void UpdateProgress(ConversionProgress progress)
    {
        ProgressBar.Maximum = Math.Max(1, progress.Total);
        ProgressBar.Value = progress.Stage == "Done" ? progress.Total : progress.Completed;
        StatusTextBlock.Text = string.IsNullOrEmpty(progress.CurrentFile)
            ? progress.Stage
            : $"{progress.Stage}: {progress.CurrentFile} ({progress.Completed + 1}/{progress.Total})";
        if (!string.IsNullOrEmpty(progress.CurrentFile)) AppendLog($"[{progress.Completed + 1}/{progress.Total}] {progress.CurrentFile}");
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cancellation?.Cancel();

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var completedPdf = _completedPdf;
        if (completedPdf is not null && File.Exists(completedPdf))
        {
            Process.Start(new ProcessStartInfo(completedPdf) { UseShellExecute = true });
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        var folder = paths?.FirstOrDefault(Directory.Exists);
        if (folder is not null) InputFolderTextBox.Text = folder;
    }

    private void SetRunning(bool running)
    {
        StartButton.IsEnabled = !running;
        CancelButton.IsEnabled = running;
        InputFolderTextBox.IsEnabled = !running;
        OutputPdfTextBox.IsEnabled = !running;
        LanguageComboBox.IsEnabled = !running;
        RecursiveCheckBox.IsEnabled = !running;
    }

    private void AppendLog(string line)
    {
        LogTextBox.AppendText(line + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }
}
