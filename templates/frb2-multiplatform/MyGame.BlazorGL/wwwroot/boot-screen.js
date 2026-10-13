// FlatRedBall2.BlazorGL pre-boot loading screen. Opt in with one <script> in index.html, placed
// BEFORE _framework/blazor.webassembly.js (it has to be installed before the runtime download
// starts to count it). It injects its own DOM and CSS, so index.html carries no markup.
//
// What it shows: the logo, an indeterminate bar, and "Loading" with the running size of the
// _framework/ download. There is deliberately no status text for engine init: it runs as one
// synchronous block, so the browser could not repaint a changing line while it ran. The bar
// animates with a transform, which keeps moving while the main thread is busy. Nothing in
// Content/ is counted; frb-host.js prefetches that concurrently and it is not what the player is
// waiting on.
//
// Safety rules this file follows, all of them load-bearing:
//   - It chains window.frbBeforeTick and never replaces it, in either load order (see
//     installBeforeTickChain), so Pattern B canvas re-pinning keeps working.
//   - It removes itself after the game has completed a couple of frames, and on timers if the
//     frame signal never arrives, so a stale copy cannot cover a working game.
//   - It asks the host (frb-host.js, through window.frbSkipTick) to skip the first tick, so the
//     browser paints the screen before the engine's synchronous init blocks the thread. A host
//     that never asks simply does not skip.
//   - It never asks for a click: the game's own click-to-start stays the only audio gate.
//   - A boot failure stays on screen with a short message and a Reload button (see the comment
//     in createView for why it is manual) instead of hanging, and the console
//     gets one "[frb-boot-screen] boot failure: rule=... url=... status=..." line naming why.
//     Only files the runtime cannot start without count (see classifyFrameworkUrl), and nothing
//     latches once a frame has completed.
//
// This file is yours when vendored (like frb-host.js). There is no customization API; to change
// the look, edit the CSS in createView.
(function (root) {
    'use strict';

    // Completed game ticks before the screen goes away. The first real tick builds the Game and
    // runs all engine and Gum init synchronously, so a completed tick means a frame has been drawn.
    // A tick the host skipped (see skipTick) draws nothing and does not count.
    var FRAMES_BEFORE_HIDE = 2;
    // The host loop is up (window.theInstance exists) but frbBeforeTick never reached us.
    var HOST_GRACE_MS = 5000;
    // Nothing happened at all, e.g. frb-host.js was never loaded. Long enough for a slow
    // connection pulling a large AOT runtime.
    var MAX_VISIBLE_MS = 600000;
    var POLL_MS = 100;
    var LOADING_TEXT = 'Loading';
    var FAILED_TEXT = 'Failed to start. Check the browser console.';
    var RELOAD_TEXT = 'Reload';

    function formatBytes(n) {
        if (!(n > 0)) return '';
        if (n < 1024 * 1024) return Math.max(1, Math.round(n / 1024)) + ' KB';
        return (n / (1024 * 1024)).toFixed(1) + ' MB';
    }

    function isFrameworkUrl(url, base) {
        try { return new URL(url, base).pathname.indexOf('/_framework/') >= 0; }
        catch (e) { return false; }
    }

    var ESSENTIAL = [/\.(wasm|dll)$/i, /\/dotnet(\.[^\/]*)?\.js$/i, /\/blazor\.webassembly(\.[^\/]*)?\.js$/i, /\/blazor\.boot\.json$/i];

    // 'none' for anything outside _framework/, 'essential' for what the runtime cannot start
    // without, 'optional' for the rest (symbols, source maps, dev-server and hot-reload helpers,
    // ICU data). A failed optional file must not put a failure screen over a game that boots.
    function classifyFrameworkUrl(url, base) {
        var path;
        try { path = new URL(url, base).pathname; } catch (e) { return 'none'; }
        if (path.indexOf('/_framework/') < 0) return 'none';
        for (var i = 0; i < ESSENTIAL.length; i++) if (ESSENTIAL[i].test(path)) return 'essential';
        return 'optional';
    }

    function describe(rule, info) {
        var text = 'rule=' + rule;
        if (info) {
            if (info.url !== undefined) text += ' url=' + info.url;
            if (info.status !== undefined) text += ' status=' + info.status;
            if (info.message !== undefined) text += ' message=' + info.message;
        }
        return text;
    }

    // env: { now(): ms, onHide(): void, log(level, message): void, onSkip(): void }. Pure state machine; the DOM
    // and timers live in start().
    function createController(env) {
        var c = {
            bytes: 0,
            failed: false,
            failure: null,
            hidden: false,
            dirty: true,
            beforeTickCalls: 0,
            skippedTicks: 0,
            hostReadyAt: null,
            startedAt: env.now()
        };
        var log = env.log || function () { };
        var ignoredOnce = {};

        c.addBytes = function (n) {
            if (c.hidden) return;
            c.bytes += n;
            c.dirty = true;
        };

        // Real (non-skipped) ticks that have finished, as of the current frbBeforeTick. Call N means
        // N-1 ticks have been through the host loop, and skipped ones drew nothing.
        function completedTicks() { return Math.max(0, c.beforeTickCalls - 1 - c.skippedTicks); }

        // A failure sticks: neither the frame count nor the timers dismiss it. The exception is a
        // failure reported after a real tick has completed: the game is running, so it is logged
        // and dropped. (The first real tick that throws reports before the next frbBeforeTick, so
        // it still latches.) rule is a short id; info optionally has url, status and message.
        c.fail = function (rule, info) {
            if (c.hidden || c.failed) return;
            rule = rule || 'unknown';
            if (completedTicks() >= 1) {
                if (!ignoredOnce[rule]) {
                    ignoredOnce[rule] = true;
                    log('warn', 'boot failure ignored, game already running: ' + describe(rule, info));
                }
                return;
            }
            c.failed = true;
            c.failure = { rule: rule, url: info && info.url, status: info && info.status, message: info && info.message };
            c.dirty = true;
            log('error', 'boot failure: ' + describe(rule, info));
        };

        // A failed request for a file the boot does not depend on: logged, never fatal.
        c.note = function (rule, info) {
            if (c.hidden) return;
            log('warn', 'optional request failed, ignored: ' + describe(rule, info));
        };

        c.hide = function () {
            if (c.hidden) return;
            c.hidden = true;
            env.onHide();
        };

        // One call per frbBeforeTick, which runs before that frame's TickDotNet. So call N means
        // N-1 ticks have gone through the host loop.
        c.beforeTick = function () {
            if (c.hidden) return;
            c.beforeTickCalls++;
            if (!c.failed && completedTicks() >= FRAMES_BEFORE_HIDE) c.hide();
        };

        // Asked by the host once per frame, right after frbBeforeTick. True exactly once, on the
        // first frame: the host then skips that frame's TickDotNet, so the browser gets a frame to
        // paint this screen's latest state before init blocks the thread. onSkip lets the DOM side
        // render synchronously, because the poll timer cannot run once init starts.
        c.skipTick = function () {
            if (c.hidden || c.failed || c.skippedTicks > 0 || c.beforeTickCalls !== 1) return false;
            c.skippedTicks = 1;
            if (env.onSkip) env.onSkip();
            return true;
        };

        // probe: { hostReady: bool, errorUiVisible: bool }
        c.poll = function (probe) {
            if (c.hidden) return;
            var now = env.now();
            if (probe.errorUiVisible && completedTicks() < 1) c.fail('blazor-error-ui', { message: probe.errorUiText });
            if (c.failed) return;
            if (probe.hostReady && c.hostReadyAt === null) c.hostReadyAt = now;
            if (c.hostReadyAt !== null && c.beforeTickCalls === 0 && now - c.hostReadyAt > HOST_GRACE_MS) c.hide();
            else if (now - c.startedAt > MAX_VISIBLE_MS) c.hide();
        };

        return c;
    }

    // Makes win.frbBeforeTick call onTick and then whatever the page assigns to it, whether the
    // page assigns before or after this runs. A setter keeps the assignment from replacing us;
    // the re-entrancy guard stops the common "prev = window.frbBeforeTick; window.frbBeforeTick =
    // function () { prev(); ... }" pattern from recursing into itself. Returns a function that
    // puts a plain property back.
    function installBeforeTickChain(win, onTick) {
        var user = win.frbBeforeTick;
        var busy = false;
        var wrapper = function () {
            if (busy) return;
            busy = true;
            try {
                onTick();
                if (typeof user === 'function') return user.apply(this, arguments);
            } finally {
                busy = false;
            }
        };
        try {
            Object.defineProperty(win, 'frbBeforeTick', {
                configurable: true,
                enumerable: true,
                get: function () { return wrapper; },
                set: function (fn) { user = fn; }
            });
        } catch (e) {
            return function () { };
        }
        return function restore() {
            Object.defineProperty(win, 'frbBeforeTick', {
                configurable: true, enumerable: true, writable: true, value: user
            });
        };
    }

    // Counts bytes of _framework/ responses by reading a clone of each one. The page still gets
    // the original promise and response untouched, so the runtime behaves the same with or
    // without this. isActive() lets the wrapper go inert after the screen is gone.
    function installFetchCounter(win, handlers, isActive) {
        var original = win.fetch;
        if (typeof original !== 'function') return function () { };
        var base = win.location && win.location.href;

        var wrapped = function (input) {
            var promise = original.apply(this, arguments);
            if (!isActive()) return promise;
            var url;
            try { url = typeof input === 'string' ? input : (input && input.url) || String(input); }
            catch (e) { return promise; }
            if (!isFrameworkUrl(url, base)) return promise;

            var kind = classifyFrameworkUrl(url, base);
            var report = function (rule, info) {
                if (kind === 'essential') handlers.onFail(rule, info);
                else if (handlers.onIgnored) handlers.onIgnored(rule, info);
            };

            promise.then(function (response) {
                if (!response.ok) { report('fetch-non-ok', { url: url, status: response.status }); return; }
                var body = response.clone().body;
                var reader = body && body.getReader();
                if (!reader) return;
                (function pump() {
                    reader.read().then(function (chunk) {
                        if (chunk.done) return;
                        handlers.onBytes(chunk.value.length);
                        pump();
                    }, function () { });
                })();
            }, function (error) {
                report('fetch-network-error', { url: url, message: error && error.message });
            });
            return promise;
        };
        win.fetch = wrapped;
        return function restore() {
            if (win.fetch === wrapped) win.fetch = original;
        };
    }

    // onReload: what the failure screen's Reload button does (start() passes location.reload).
    function createView(doc, onReload) {
        var css =
            '#frb-boot-screen{position:fixed;top:0;left:0;right:0;bottom:0;z-index:900;background:#000;color:#ddd;' +
            'display:flex;flex-direction:column;align-items:center;justify-content:center;' +
            'font-family:system-ui,-apple-system,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif}' +
            '#frb-boot-screen img{width:56%;max-width:340px;height:auto}' +
            '#frb-boot-screen .frb-bar{width:56%;max-width:340px;height:4px;border-radius:2px;margin-top:28px;' +
            'overflow:hidden;position:relative;background:#2a2a2a}' +
            // transform, not left: a transform animation keeps moving while the main thread is
            // busy inside the engine's synchronous init.
            '#frb-boot-screen .frb-fill{position:absolute;top:0;left:0;height:100%;width:35%;background:#e3141a;' +
            'border-radius:2px;will-change:transform;animation:frb-boot-slide 1.4s ease-in-out infinite}' +
            '@keyframes frb-boot-slide{from{transform:translateX(-100%)}to{transform:translateX(286%)}}' +
            '@media (prefers-reduced-motion:reduce){#frb-boot-screen .frb-fill{animation:none;width:100%;opacity:.5}}' +
            '#frb-boot-screen .frb-meta{margin-top:12px;font-size:13px;display:flex;gap:14px;' +
            'font-variant-numeric:tabular-nums;opacity:.85}' +
            '#frb-boot-screen .frb-err{color:#ff6b6b;text-align:center;font-size:13px;margin-top:22px;max-width:70%}' +
            // Same font as the rest of the screen (inherit), so no font is added. A real <button> so
            // Tab, Enter and Space work with no extra code; :focus-visible keeps the ring for
            // keyboard users only.
            '#frb-boot-screen .frb-reload{margin-top:16px;font:inherit;font-size:13px;color:#ddd;background:transparent;' +
            'border:1px solid #555;border-radius:4px;padding:6px 18px;cursor:pointer}' +
            '#frb-boot-screen .frb-reload:hover{border-color:#e3141a;color:#fff}' +
            '#frb-boot-screen .frb-reload:focus-visible{outline:2px solid #e3141a;outline-offset:2px}';

        function el(tag, className, text) {
            var e = doc.createElement(tag);
            if (className) e.className = className;
            if (text) e.textContent = text;
            return e;
        }

        var rootEl = el('div');
        rootEl.id = 'frb-boot-screen';
        rootEl.setAttribute('role', 'status');
        var style = el('style');
        style.textContent = css;
        var logo = el('img');
        logo.alt = 'FlatRedBall';
        logo.src = LOGO;
        // A blocked or broken logo just disappears; the rest of the screen still works.
        logo.onerror = function () { logo.style.display = 'none'; };
        var bar = el('div', 'frb-bar');
        bar.appendChild(el('div', 'frb-fill'));
        var meta = el('div', 'frb-meta');
        var label = el('span', '', LOADING_TEXT);
        var size = el('span');
        meta.appendChild(label);
        meta.appendChild(size);
        var err = el('div', 'frb-err', FAILED_TEXT);
        err.style.display = 'none';

        // ---------------------------------------------------------------------------------------
        // Reload button: shown ONLY on the failure screen.
        //
        // What it is for: on a first load over https, the page can fail to start with
        // "net::ERR_CERT_VERIFIER_CHANGED" on a burst of _framework/ requests, and a plain page
        // reload fixes it. Without this button the player sees "Failed to start" with nothing to
        // do about it.
        //
        // Why it happens (full write-up and experiments in the issue linked below): a freshly
        // launched browser rebuilds its certificate verifier several times in its first few
        // seconds. When that happens Chromium makes every current HTTP/2 session go away, and the
        // requests still queued in the session waiting for a stream id (a server allows about 100
        // concurrent streams, Blazor requests every _framework/ file at once) fail with that error.
        // Blazor's loader does not retry a failed fetch, so the runtime never starts. It is common
        // on a Debug dev server over https (~270 requests), rare on a Release publish (~120
        // requests, 1 failure in 60 test loads), and needs an HTTP/2 host plus a browser that
        // started moments before the page loaded.
        //
        // Deliberately a MANUAL reload, not an automatic retry: the decision (recorded in the
        // issue) was not to paper over a failure whose trigger is outside our control and only
        // partly understood. The player gets an honest failure and a one-click way out. If this
        // shows up in the wild, the issue lists the next options (a bounded one-shot retry of an
        // idempotent _framework/ fetch in installFetchCounter, or fewer concurrent requests).
        //
        // https://github.com/vchelaru/FlatRedBall2/issues/1467
        //
        // Keyboard: a <button> is focusable and fires click on Enter and Space, so there is no key
        // handling here. It is display:none (so also out of the Tab order) in every other state.
        // ---------------------------------------------------------------------------------------
        var reload = el('button', 'frb-reload', RELOAD_TEXT);
        reload.setAttribute('type', 'button');
        reload.addEventListener('click', function () { if (onReload) onReload(); });
        reload.style.display = 'none';
        var reloadFocused = false;

        [style, logo, bar, meta, err, reload].forEach(function (child) { rootEl.appendChild(child); });
        (doc.body || doc.documentElement).appendChild(rootEl);

        return {
            render: function (state) {
                size.textContent = formatBytes(state.bytes);
                bar.style.display = meta.style.display = state.failed ? 'none' : '';
                err.style.display = state.failed ? '' : 'none';
                reload.style.display = state.failed ? '' : 'none';
                // Put keyboard focus on it the first time the failure shows: nothing else on this
                // screen is interactive, and the canvas that used to hold focus never started.
                if (state.failed && !reloadFocused) {
                    reloadFocused = true;
                    try { reload.focus(); } catch (e) { }
                }
                // For tooling: a script can read why without scraping the console.
                rootEl.setAttribute('data-state', state.failed ? 'failed' : 'loading');
                if (state.failure) rootEl.setAttribute('data-failure', describe(state.failure.rule, state.failure));
            },
            remove: function () {
                if (rootEl.parentNode) rootEl.parentNode.removeChild(rootEl);
            }
        };
    }

    function start(win) {
        if (win.__frbBootScreen) return;
        win.__frbBootScreen = true;
        var doc = win.document;
        var view = createView(doc, function () { win.location.reload(); });

        var restoreChain = function () { };
        var restoreFetch = function () { };
        var timer = 0;
        var controller = createController({
            log: function (level, message) {
                try { win.console[level]('[frb-boot-screen] ' + message); } catch (e) { }
            },
            now: function () { return win.performance && win.performance.now ? win.performance.now() : Date.now(); },
            // Called as the host skips the first tick: paint the latest byte count now, since the
            // poll timer cannot run once the engine's synchronous init starts.
            onSkip: function () {
                if (controller.dirty) { controller.dirty = false; view.render(controller); }
            },
            onHide: function () {
                win.clearInterval(timer);
                win.removeEventListener('error', onError, true);
                restoreFetch();
                restoreChain();
                view.remove();
            }
        });

        function onError(e) {
            var target = e.target;
            var src = (target && (target.src || target.href)) || e.filename || '';
            var kind = classifyFrameworkUrl(src, win.location.href);
            if (kind === 'essential') controller.fail('script-error', { url: src, message: e.message });
            else if (kind === 'optional') controller.note('script-error', { url: src, message: e.message });
        }

        restoreChain = installBeforeTickChain(win, controller.beforeTick);
        restoreFetch = installFetchCounter(win, {
            onBytes: controller.addBytes,
            onFail: controller.fail,
            onIgnored: controller.note
        }, function () { return !controller.hidden; });
        win.addEventListener('error', onError, true);

        // Asked by frb-host.js once per frame; true only for the first one.
        win.frbSkipTick = controller.skipTick;
        // Called from the engine through Pages/Index.razor.cs; no-ops once the screen is gone.
        win.frbBootFailed = function (message) { controller.fail('frbBootFailed', { message: message }); };

        view.render(controller);
        timer = win.setInterval(function () {
            var errorUi = doc.getElementById('blazor-error-ui');
            controller.poll({
                hostReady: !!win.theInstance,
                errorUiVisible: !!errorUi && win.getComputedStyle(errorUi).display !== 'none',
                errorUiText: errorUi ? (errorUi.textContent || '').replace(/\s+/g, ' ').trim() : ''
            });
            if (!controller.hidden && controller.dirty) {
                controller.dirty = false;
                view.render(controller);
            }
        }, POLL_MS);
    }

    var LOGO = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAgAAAABYCAYAAAH0PX/fAAAAGXRFWHRTb2Z0d2FyZQBBZG9iZSBJbWFnZVJlYWR5ccllPAAAAyZpVFh0WE1MOmNvbS5hZG9iZS54bXAAAAAAADw/eHBhY2tldCBiZWdpbj0i77u/IiBpZD0iVzVNME1wQ2VoaUh6cmVTek5UY3prYzlkIj8+IDx4OnhtcG1ldGEgeG1sbnM6eD0iYWRvYmU6bnM6bWV0YS8iIHg6eG1wdGs9IkFkb2JlIFhNUCBDb3JlIDUuNi1jMTExIDc5LjE1ODMyNSwgMjAxNS8wOS8xMC0wMToxMDoyMCAgICAgICAgIj4gPHJkZjpSREYgeG1sbnM6cmRmPSJodHRwOi8vd3d3LnczLm9yZy8xOTk5LzAyLzIyLXJkZi1zeW50YXgtbnMjIj4gPHJkZjpEZXNjcmlwdGlvbiByZGY6YWJvdXQ9IiIgeG1sbnM6eG1wPSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvIiB4bWxuczp4bXBNTT0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wL21tLyIgeG1sbnM6c3RSZWY9Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC9zVHlwZS9SZXNvdXJjZVJlZiMiIHhtcDpDcmVhdG9yVG9vbD0iQWRvYmUgUGhvdG9zaG9wIENDIDIwMTUgKFdpbmRvd3MpIiB4bXBNTTpJbnN0YW5jZUlEPSJ4bXAuaWlkOjM1MzU5Q0NCRDBFODExRTVBMzQ2OUMwRkFDQkVFQzlFIiB4bXBNTTpEb2N1bWVudElEPSJ4bXAuZGlkOjM1MzU5Q0NDRDBFODExRTVBMzQ2OUMwRkFDQkVFQzlFIj4gPHhtcE1NOkRlcml2ZWRGcm9tIHN0UmVmOmluc3RhbmNlSUQ9InhtcC5paWQ6MzUzNTlDQzlEMEU4MTFFNUEzNDY5QzBGQUNCRUVDOUUiIHN0UmVmOmRvY3VtZW50SUQ9InhtcC5kaWQ6MzUzNTlDQ0FEMEU4MTFFNUEzNDY5QzBGQUNCRUVDOUUiLz4gPC9yZGY6RGVzY3JpcHRpb24+IDwvcmRmOlJERj4gPC94OnhtcG1ldGE+IDw/eHBhY2tldCBlbmQ9InIiPz5vWk38AABQC0lEQVR42uRW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';

    var api = {
        formatBytes: formatBytes,
        isFrameworkUrl: isFrameworkUrl,
        classifyFrameworkUrl: classifyFrameworkUrl,
        createController: createController,
        createView: createView,
        installBeforeTickChain: installBeforeTickChain,
        installFetchCounter: installFetchCounter,
        constants: { FRAMES_BEFORE_HIDE: FRAMES_BEFORE_HIDE, HOST_GRACE_MS: HOST_GRACE_MS, MAX_VISIBLE_MS: MAX_VISIBLE_MS }
    };

    // Under node (the repo's Packaging tests) the file is a module and does not touch a page.
    if (typeof module !== 'undefined' && module.exports) { module.exports = api; return; }
    if (root && root.document) start(root);
})(typeof window !== 'undefined' ? window : undefined);
