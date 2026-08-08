using System.Windows;
using PhotoSearchPdf.Core;

namespace PhotoSearchPdf.App;

public partial class App : Application
{
    static App() => WindowsEnvironment.EnsureWindir();
}
