// Must run before the Game constructor: SDL picks its video driver there.
FlatRedBall2.Utilities.LinuxVideoDriver.ApplyWaylandFallback();

using var game = new GameNamespace.Game1();
game.Run();
