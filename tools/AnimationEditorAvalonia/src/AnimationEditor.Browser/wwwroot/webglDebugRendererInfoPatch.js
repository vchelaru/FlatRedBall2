// #761: SkiaSharp's WebGL backend probes UNMASKED_VENDOR_WEBGL / UNMASKED_RENDERER_WEBGL
// via getParameter without enabling WEBGL_debug_renderer_info first. WebKit then logs:
//   WebGL: INVALID_ENUM: getParameter: invalid parameter name,
//   WEBGL_debug_renderer_info not enabled
// (twice at Avalonia.Browser startup). Cosmetic only — canvas still works — but noisy.
// Enable the extension before those probes, or fall back to masked VENDOR/RENDERER when
// the browser withholds the extension for privacy. Must load before Avalonia/Skia creates
// a WebGL context (see index.html).
(function (root) {
    var UNMASKED_VENDOR_WEBGL = 0x9245;
    var UNMASKED_RENDERER_WEBGL = 0x9246;

    function createSafeGetParameter(originalGetParameter, getExtension) {
        return function (pname) {
            if (pname === UNMASKED_VENDOR_WEBGL || pname === UNMASKED_RENDERER_WEBGL) {
                var getExt = getExtension || this.getExtension;
                var ext = typeof getExt === 'function'
                    ? getExt.call(this, 'WEBGL_debug_renderer_info')
                    : null;
                if (!ext) {
                    var fallback = pname === UNMASKED_VENDOR_WEBGL ? this.VENDOR : this.RENDERER;
                    return originalGetParameter.call(this, fallback);
                }
            }
            return originalGetParameter.call(this, pname);
        };
    }

    function patchPrototype(proto) {
        if (!proto || proto.__frbWebglDebugRendererPatched) return;
        if (typeof proto.getParameter !== 'function') return;
        proto.getParameter = createSafeGetParameter(proto.getParameter);
        proto.__frbWebglDebugRendererPatched = true;
    }

    function install(globalObj) {
        var g = globalObj || root;
        if (g.WebGLRenderingContext) patchPrototype(g.WebGLRenderingContext.prototype);
        if (g.WebGL2RenderingContext) patchPrototype(g.WebGL2RenderingContext.prototype);
    }

    root.__frbWebglDebugRendererInfoPatch = {
        UNMASKED_VENDOR_WEBGL: UNMASKED_VENDOR_WEBGL,
        UNMASKED_RENDERER_WEBGL: UNMASKED_RENDERER_WEBGL,
        createSafeGetParameter: createSafeGetParameter,
        install: install
    };

    if (typeof root.WebGLRenderingContext !== 'undefined' ||
        typeof root.WebGL2RenderingContext !== 'undefined') {
        install(root);
    }
})(typeof globalThis !== 'undefined' ? globalThis : this);
