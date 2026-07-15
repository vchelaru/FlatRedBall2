import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

function showBootError(message) {
    const loading = document.getElementById('loading');
    if (!loading) {
        console.error('[boot]', message);
        return;
    }
    loading.innerHTML = '';
    const box = document.createElement('div');
    box.className = 'boot-error';
    box.textContent = message;
    loading.appendChild(box);
}

window.addEventListener('unhandledrejection', (e) => {
    const reason = e.reason?.message ?? String(e.reason ?? 'unknown error');
    if (/wasm|framework|fetch|integrity|download/i.test(reason)) {
        showBootError(
            'Failed to load the .NET runtime (check DevTools Console).\n\n' +
            reason + '\n\n' +
            'This is usually a browser HTTP-cache mismatch (force-cache + SRI).\n' +
            'Hard refresh (Ctrl+Shift+R) after the server shows App url.'
        );
    }
});

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

try {
    // Root cause of endless spinner / SRI failures in local WasmAppHost:
    // boot assets use stable virtualPath URLs (AnimationEditor.Core.wasm) with cache:force-cache.
    // After a rebuild, Chrome serves stale bytes for that URL and SRI rejects them.
    // disableIntegrityCheck + no-store fetch (patched in index.html) make Debug boot reliable.
    const dotnetRuntime = await dotnet
        .withDiagnosticTracing(false)
        .withConfig({ disableIntegrityCheck: true })
        .withApplicationArgumentsFromQuery()
        .create();

    const config = dotnetRuntime.getConfig();

    await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
} catch (err) {
    const msg = err?.message ?? String(err);
    showBootError(
        'Animation Editor failed to start.\n\n' + msg + '\n\n' +
        'Confirm .\\run-browser.ps1 still shows App url, then Ctrl+Shift+R.'
    );
    throw err;
}
