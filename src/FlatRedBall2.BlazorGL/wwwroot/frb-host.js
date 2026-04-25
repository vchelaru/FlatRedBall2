// FlatRedBall2.BlazorGL host script. Replaces ~40 lines of per-sample <script> boilerplate
// (KNI WASM shim tags + tickJS + initRenderJS) with one <script src="..."> in index.html.
//
// Loaded synchronously after blazor.webassembly.js. Uses document.write to inject the
// nkast.Wasm.* shim tags so the version pin lives in exactly one place. document.write
// during initial parse is the supported mechanism for sync-ordered script insertion;
// browsers warn only when called from cross-origin parser-blocking scripts (not us).

(function () {
    var v = '8.0.11';
    var shims = [
        ['nkast.Wasm.JSInterop', 'JSObject'],
        ['nkast.Wasm.Dom',       'Window'],
        ['nkast.Wasm.Dom',       'Document'],
        ['nkast.Wasm.Dom',       'Navigator'],
        ['nkast.Wasm.Dom',       'Gamepad'],
        ['nkast.Wasm.Dom',       'Media'],
        ['nkast.Wasm.XHR',       'XHR'],
        ['nkast.Wasm.Canvas',    'Canvas'],
        ['nkast.Wasm.Canvas',    'CanvasGLContext'],
        ['nkast.Wasm.Audio',     'Audio'],
        ['nkast.Wasm.XR',        'XR']
    ];
    for (var i = 0; i < shims.length; i++) {
        // Split </script> string so this file itself parses correctly when inlined.
        document.write('<script src="_content/' + shims[i][0] + '/js/' + shims[i][1] + '.' + v + '.js"></scr' + 'ipt>');
    }
})();

// Per-frame loop. window.frbBeforeTick is an optional hook for fixed-canvas pattern users
// who need to re-pin canvas.width/height each frame (see multiplatform-conversion skill,
// Pattern B).
function tickJS() {
    if (typeof window.frbBeforeTick === 'function') window.frbBeforeTick();
    window.theInstance.invokeMethod('TickDotNet');
    window.requestAnimationFrame(tickJS);
}

// Called from Blazor's OnAfterRender via JSInterop. window.frbAfterInit is an optional
// hook for consumers that need to capture canvas/holder dimensions at init time.
window.initRenderJS = function (instance) {
    window.theInstance = instance;
    var canvas = document.getElementById('theCanvas');
    var holder = document.getElementById('canvasHolder');
    canvas.width = holder.clientWidth;
    canvas.height = holder.clientHeight;
    canvas.addEventListener("contextmenu", function (e) { e.preventDefault(); });
    if (typeof window.frbAfterInit === 'function') window.frbAfterInit(canvas, holder);
    window.requestAnimationFrame(tickJS);
};
