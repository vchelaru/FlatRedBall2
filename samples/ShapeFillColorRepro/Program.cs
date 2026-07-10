// This is a bug repro, so it defaults to SHOWING the bug: a plain `dotnet run` reproduces #663
// (blue rectangle on an affected Mac). The engine reads FRB2_DISABLE_FILL_PRIME in
// ShapesBatch.Initialize; defaulting it to "1" here (only when the caller hasn't set it) turns the
// fix off for this sample. Run `FRB2_DISABLE_FILL_PRIME=0 dotnet run` to see the fix (black). The
// engine's own default stays fix-ON, so no other sample or game is affected.
if (Environment.GetEnvironmentVariable("FRB2_DISABLE_FILL_PRIME") is null)
    Environment.SetEnvironmentVariable("FRB2_DISABLE_FILL_PRIME", "1");

using var game = new ShapeFillColorRepro.Game1();
game.Run();
