using System.Globalization;
using System.Text;
using AnimationEditor.Cli;

// The only process-wide setup: output that is the same on every machine. Commands never touch globals.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

return await CliApp.RunAsync(args, Console.Out, Console.Error, Console.In);
