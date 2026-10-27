using System;
using System.IO;
using System.Linq;

var smokeTest = args.Contains("--smoke-test", StringComparer.Ordinal);
try
{
    using var game = new DarkLoot.Game.DarkLootGame(smokeTest);
    game.Run();
}
catch (Exception exception)
{
    var diagnostic = exception.ToString();
    var logPath = Path.Combine(AppContext.BaseDirectory, smokeTest ? "smoke-test.log" : "startup-error.log");
    if (smokeTest)
    {
        File.AppendAllText(logPath, diagnostic);
    }
    else
    {
        File.WriteAllText(logPath, diagnostic);
    }
    Console.Error.WriteLine(diagnostic);
    Environment.ExitCode = 1;
}
