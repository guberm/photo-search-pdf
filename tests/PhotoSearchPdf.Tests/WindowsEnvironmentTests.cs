using PhotoSearchPdf.Core;

namespace PhotoSearchPdf.Tests;

public sealed class WindowsEnvironmentTests
{
    [Fact]
    public void EnsureWindir_RepairsMissingVariableFromSystemRoot()
    {
        var original = Environment.GetEnvironmentVariable("windir");
        try
        {
            Environment.SetEnvironmentVariable("windir", null);

            WindowsEnvironment.EnsureWindir();

            Assert.Equal(Environment.GetEnvironmentVariable("SystemRoot"), Environment.GetEnvironmentVariable("windir"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("windir", original);
        }
    }
}
