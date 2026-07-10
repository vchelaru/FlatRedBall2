import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

// #535 M4 load indicator: Avalonia appends its canvas/native-host/input elements as siblings
// inside #out rather than replacing what's there, so the #loading div (also a child of #out)
// never goes away on its own. Watch for the canvas appearing and remove it then -- runMain()
// below never resolves for a long-lived SPA, so there's no "app is ready" await to hang this off.
const out = document.getElementById('out');
const loadingObserver = new MutationObserver(() => {
    if (out.querySelector('canvas')) {
        document.getElementById('loading')?.remove();
        loadingObserver.disconnect();
    }
});
loadingObserver.observe(out, { childList: true });

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
