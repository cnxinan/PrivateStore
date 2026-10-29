using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DarkLoot.Game;
using DarkLoot.Gameplay.Persistence;

var persistenceOnlySmoke = args.Contains("--persistence-smoke-test", StringComparer.Ordinal);
var smokeTest = persistenceOnlySmoke || args.Contains("--smoke-test", StringComparer.Ordinal);
try
{
    ApplicationConfiguration.Initialize();
    using var persistenceSmoke = smokeTest
        ? new PersistenceSmokeTest(Path.Combine(AppContext.BaseDirectory, "smoke-test.log"))
        : null;
    var savePath = persistenceSmoke?.SavePath ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DarkLoot", "save.json");
    using (var game = new DarkLootGame(smokeTest, savePath, persistenceOnlySmoke: persistenceOnlySmoke))
    {
        game.Run();
        if (persistenceSmoke is not null)
            game.RecordSmokeProgress(persistenceSmoke);
    }
    if (persistenceSmoke is not null)
    {
        using var restored = new DarkLootGame(smokeTest, savePath, persistenceSmoke);
        restored.Run();
    }
}
catch (Exception exception)
{
    var diagnostic = exception.ToString();
    Console.Error.WriteLine(diagnostic);
    Environment.ExitCode = 1;
    if (!smokeTest && exception is SaveGameException)
        MessageBox.Show(GameText.SaveError + Environment.NewLine + Environment.NewLine + exception.Message,
            GameText.SaveErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
    var logPath = Path.Combine(AppContext.BaseDirectory, smokeTest ? "smoke-test.log" : "startup-error.log");
    try
    {
        if (smokeTest)
            File.AppendAllText(logPath, diagnostic);
        else
            File.WriteAllText(logPath, diagnostic);
    }
    catch (Exception logException) when (logException is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Cannot write diagnostic log '{logPath}': {logException}");
    }
}
