// "--selftest" runs the match logic checks and exits. "--screenshots <dir>" scripts a few states, saves back-buffer PNGs, then exits.
string? screenshotDir = null;
bool selfTest = args.Contains("--selftest");
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--screenshots") screenshotDir = args[i + 1];
}

using var game = new Beefball.Game1(screenshotDir, selfTest);
game.Run();
