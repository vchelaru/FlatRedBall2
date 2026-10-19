using System;
using System.Diagnostics;
using System.IO;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

// boot-screen.js is plain browser JS with its decision logic (frame counting, failsafe timers,
// frbBeforeTick chaining, _framework byte counting) separated from the DOM, and it exports that
// logic as a node module when loaded outside a page. These tests run it under node, which the CI
// runner has; a machine without node fails here rather than silently skipping the coverage.
public class BootScreenBehaviorTests
{
    private static string BootScreenPath => Path.Combine(
        TemplatePackageReferenceTests.RepoRootForTests, "src", "FlatRedBall2.BlazorGL", "wwwroot", "boot-screen.js");

    private static string RunNode(string script)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"frb-boot-screen-{Guid.NewGuid():N}.js");
        File.WriteAllText(scriptPath,
            "const b = require(process.env.BOOT_SCREEN_PATH);\n" +
            "const out = (v) => process.stdout.write(JSON.stringify(v));\n" + script);
        try
        {
            var startInfo = new ProcessStartInfo("node", $"\"{scriptPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.Environment["BOOT_SCREEN_PATH"] = BootScreenPath;
            using var process = Process.Start(startInfo)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            process.ExitCode.ShouldBe(0, stderr);
            return stdout;
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    private static string RunNodeRaw(string script, string hostPath)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"frb-host-{Guid.NewGuid():N}.js");
        File.WriteAllText(scriptPath, script);
        try
        {
            var startInfo = new ProcessStartInfo("node", $"\"{scriptPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.Environment["HOST_PATH"] = hostPath;
            using var process = Process.Start(startInfo)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            process.ExitCode.ShouldBe(0, stderr);
            return stdout;
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [Fact]
    public void InstallBeforeTickChain_PageAssignsHookAfterInstall_RunsBothAndKeepsPageHook()
    {
        // Pattern B pages assign window.frbBeforeTick in an inline script after frb-host.js, i.e.
        // after this file installed itself. A plain property would be replaced and the screen
        // would never see a frame.
        var result = RunNode(@"
            const win = {}; let ours = 0, theirs = 0;
            b.installBeforeTickChain(win, () => ours++);
            win.frbBeforeTick = () => theirs++;
            win.frbBeforeTick(); win.frbBeforeTick();
            out({ ours, theirs });");

        result.ShouldBe("{\"ours\":2,\"theirs\":2}");
    }

    [Fact]
    public void InstallBeforeTickChain_PageHookPresentBeforeInstall_StillRuns()
    {
        var result = RunNode(@"
            let ours = 0, theirs = 0;
            const win = { frbBeforeTick: () => theirs++ };
            b.installBeforeTickChain(win, () => ours++);
            win.frbBeforeTick();
            out({ ours, theirs });");

        result.ShouldBe("{\"ours\":1,\"theirs\":1}");
    }

    [Fact]
    public void InstallBeforeTickChain_PageWrapsPreviousHook_DoesNotRecurse()
    {
        // The usual chaining idiom reads the old value and calls it from the new one. The old
        // value is our wrapper, which would call the new hook, which would call it again.
        var result = RunNode(@"
            const win = {}; let ours = 0, theirs = 0;
            b.installBeforeTickChain(win, () => ours++);
            const prev = win.frbBeforeTick;
            win.frbBeforeTick = function () { prev(); theirs++; };
            win.frbBeforeTick();
            out({ ours, theirs });");

        result.ShouldBe("{\"ours\":1,\"theirs\":1}");
    }

    [Fact]
    public void InstallBeforeTickChain_Restore_LeavesPlainPropertyHoldingPageHook()
    {
        // With the screen gone the page must see exactly what it assigned, as a plain property.
        var result = RunNode(@"
            const win = {}; const mine = () => {};
            const restore = b.installBeforeTickChain(win, () => {});
            win.frbBeforeTick = mine;
            restore();
            const d = Object.getOwnPropertyDescriptor(win, 'frbBeforeTick');
            out({ same: win.frbBeforeTick === mine, plain: 'value' in d });");

        result.ShouldBe("{\"same\":true,\"plain\":true}");
    }

    [Fact]
    public void Controller_BeforeTickCalls_HidesOnlyAfterTwoCompletedFrames()
    {
        // The first frbBeforeTick runs before the first TickDotNet, which is where the Game is
        // built; call N means N-1 ticks are done. Hiding on call 1 or 2 would expose a black page
        // through engine and Gum init.
        var result = RunNode(@"
            let hidden = 0;
            const c = b.createController({ now: () => 0, onHide: () => hidden++ });
            const states = [];
            for (let i = 0; i < 3; i++) { c.beforeTick(); states.push(c.hidden); }
            out({ states, hidden });");

        result.ShouldBe("{\"states\":[false,false,true],\"hidden\":1}");
    }

    [Fact]
    public void Controller_FailedThenFramesAndTimeouts_StaysVisible()
    {
        var result = RunNode(@"
            let now = 0;
            const c = b.createController({ now: () => now, onHide: () => {} });
            c.fail();
            for (let i = 0; i < 10; i++) c.beforeTick();
            now = b.constants.MAX_VISIBLE_MS * 2;
            c.poll({ hostReady: true, errorUiVisible: false });
            out({ hidden: c.hidden, failed: c.failed });");

        result.ShouldBe("{\"hidden\":false,\"failed\":true}");
    }

    [Fact]
    public void Controller_HostReadyButHookNeverCalled_HidesAfterGrace()
    {
        // A host whose loop never reaches frbBeforeTick must not leave the screen over a game.
        var result = RunNode(@"
            let now = 1000;
            const c = b.createController({ now: () => now, onHide: () => {} });
            c.poll({ hostReady: true, errorUiVisible: false });
            now += b.constants.HOST_GRACE_MS - 1;
            c.poll({ hostReady: true, errorUiVisible: false });
            const before = c.hidden;
            now += 2;
            c.poll({ hostReady: true, errorUiVisible: false });
            out({ before, after: c.hidden });");

        result.ShouldBe("{\"before\":false,\"after\":true}");
    }

    [Fact]
    public void Controller_NeverAnyHostSignal_HidesAtAbsoluteCap()
    {
        var result = RunNode(@"
            let now = 0;
            const c = b.createController({ now: () => now, onHide: () => {} });
            now = b.constants.MAX_VISIBLE_MS + 1;
            c.poll({ hostReady: false, errorUiVisible: false });
            out(c.hidden);");

        result.ShouldBe("true");
    }

    [Fact]
    public void InstallFetchCounter_FrameworkAndContentResponses_CountsOnlyFramework()
    {
        // initRenderJS prefetches every Content/ file during the runtime download; counting that
        // would overstate what the player is waiting on.
        var result = RunNode(@"
            const make = (n) => new Response(new Uint8Array(n));
            const win = {
                location: { href: 'https://game.test/play/index.html' },
                fetch: (url) => Promise.resolve(make(url.includes('_framework') ? 1000 : 5000)),
            };
            let bytes = 0;
            b.installFetchCounter(win, { onBytes: (n) => bytes += n, onFail: () => {} }, () => true);
            (async () => {
                await win.fetch('_framework/dotnet.native.wasm');
                await win.fetch('Content/music.ogg');
                await new Promise((r) => setTimeout(r, 50));
                out(bytes);
            })();");

        result.ShouldBe("1000");
    }

    [Fact]
    public void InstallFetchCounter_FrameworkRequestFails_ReportsFailureAndLeavesRejectionToCaller()
    {
        var result = RunNode(@"
            let failures = 0, callerSawRejection = false;
            const win = {
                location: { href: 'https://game.test/index.html' },
                fetch: () => Promise.reject(new Error('net')),
            };
            b.installFetchCounter(win, { onBytes: () => {}, onFail: () => failures++ }, () => true);
            win.fetch('_framework/blazor.boot.json').catch(() => callerSawRejection = true)
                .then(() => out({ failures, callerSawRejection }));");

        result.ShouldBe("{\"failures\":1,\"callerSawRejection\":true}");
    }

    // Chromium reports ERR_FAILED for a request bound to an HTTP/2 session that goes away at a
    // certificate-verifier rebuild and never retries it (#1467). The wrapper retries once, so the
    // caller (Blazor's loader) gets the retried result and the screen never sees the first failure.
    private const string RetryHarness = @"
        const run = async (fetchImpl, call) => {
            let calls = 0, failures = 0, bytes = 0;
            const win = { location: { href: 'https://game.test/index.html' },
                fetch: (...a) => { calls++; return fetchImpl(calls, ...a); } };
            b.installFetchCounter(win, { onBytes: (n) => bytes += n, onFail: () => failures++ }, () => true);
            let outcome;
            try { const r = await call(win); outcome = 'status ' + r.status; } catch (e) { outcome = 'rejected ' + e.message; }
            await new Promise((r) => setTimeout(r, 30));
            return { outcome, calls, failures, bytes };
        };";

    [Fact]
    public void InstallFetchCounter_FrameworkGetRejectsOnce_RetriesAndReturnsSecondResultWithoutFailing()
    {
        var result = RunNode(RetryHarness + @"
            run((n) => n === 1 ? Promise.reject(new TypeError('Failed to fetch')) : Promise.resolve(new Response(new Uint8Array(1000))),
                (win) => win.fetch('https://game.test/_framework/System.Xml.wasm')).then(out);");

        result.ShouldBe("{\"outcome\":\"status 200\",\"calls\":2,\"failures\":0,\"bytes\":1000}");
    }

    [Fact]
    public void InstallFetchCounter_FrameworkGetRejectsTwice_RetriesOnlyOnceAndFailsTheBoot()
    {
        var result = RunNode(RetryHarness + @"
            run(() => Promise.reject(new TypeError('Failed to fetch')),
                (win) => win.fetch('https://game.test/_framework/System.Xml.wasm')).then(out);");

        result.ShouldBe("{\"outcome\":\"rejected Failed to fetch\",\"calls\":2,\"failures\":1,\"bytes\":0}");
    }

    [Fact]
    public void InstallFetchCounter_RequestsThatMustNotBeRetried_AreSentOnce()
    {
        var result = RunNode(RetryHarness + @"
            (async () => {
                const reject = () => Promise.reject(new TypeError('Failed to fetch'));
                out({
                    outsideFramework: await run(reject, (win) => win.fetch('https://game.test/Content/level.tmx')),
                    post: await run(reject, (win) => win.fetch('https://game.test/_framework/x.wasm', { method: 'POST' })),
                    postRequestObject: await run(reject, (win) => win.fetch({ url: 'https://game.test/_framework/x.wasm', method: 'POST' })),
                    httpError: await run(() => Promise.resolve(new Response('', { status: 500 })), (win) => win.fetch('https://game.test/_framework/x.wasm')),
                });
            })();");

        result.ShouldBe("{\"outsideFramework\":{\"outcome\":\"rejected Failed to fetch\",\"calls\":1,\"failures\":0,\"bytes\":0}," +
            "\"post\":{\"outcome\":\"rejected Failed to fetch\",\"calls\":1,\"failures\":1,\"bytes\":0}," +
            "\"postRequestObject\":{\"outcome\":\"rejected Failed to fetch\",\"calls\":1,\"failures\":1,\"bytes\":0}," +
            "\"httpError\":{\"outcome\":\"status 500\",\"calls\":1,\"failures\":1,\"bytes\":0}}");
    }

    [Fact]
    public void Controller_Fail_LogsRuleUrlAndStatusAndKeepsFirstReason()
    {
        // The failed screen only says "check the console"; the console has to say what happened.
        var result = RunNode(@"
            const logs = [];
            const c = b.createController({ now: () => 0, onHide: () => {}, log: (level, msg) => logs.push(level + ' ' + msg) });
            c.fail('fetch-non-ok', { url: 'https://game.test/_framework/dotnet.native.wasm', status: 404 });
            c.fail('script-error', { url: 'https://game.test/_framework/other.js' });
            out({ failed: c.failed, rule: c.failure.rule, status: c.failure.status, logs });");

        result.ShouldBe("{\"failed\":true,\"rule\":\"fetch-non-ok\",\"status\":404,\"logs\":[\"error boot failure: rule=fetch-non-ok url=https://game.test/_framework/dotnet.native.wasm status=404\"]}");
    }

    [Fact]
    public void Controller_FailAfterAFrameCompleted_DoesNotLatchAndLogsWhyItWasIgnored()
    {
        // Once a tick has completed the game is running; a late 404 or script error must not
        // put a failure message over it.
        var result = RunNode(@"
            const logs = [];
            const c = b.createController({ now: () => 0, onHide: () => {}, log: (level, msg) => logs.push(level + ' ' + msg) });
            c.beforeTick(); c.beforeTick();
            c.fail('blazor-error-ui', { message: 'late' });
            c.beforeTick();
            out({ failed: c.failed, hidden: c.hidden, logs });");

        result.ShouldBe("{\"failed\":false,\"hidden\":true,\"logs\":[\"warn boot failure ignored, game already running: rule=blazor-error-ui message=late\"]}");
    }

    [Fact]
    public void Controller_FailFirstTickThrew_StillLatches()
    {
        // The C# side reports a first-tick exception while the next frbBeforeTick (call 2) has not
        // run yet, so it must still count as a boot failure.
        var result = RunNode(@"
            const c = b.createController({ now: () => 0, onHide: () => {}, log: () => {} });
            c.beforeTick();
            c.fail('frbBootFailed', { message: 'InvalidOperationException: boom' });
            for (let i = 0; i < 5; i++) c.beforeTick();
            out({ failed: c.failed, hidden: c.hidden });");

        result.ShouldBe("{\"failed\":true,\"hidden\":false}");
    }

    [Theory]
    [InlineData("_framework/dotnet.native.abc123.wasm", "essential")]
    [InlineData("_framework/Game.abc123.wasm", "essential")]
    [InlineData("_framework/dotnet.js", "essential")]
    [InlineData("_framework/blazor.webassembly.js", "essential")]
    [InlineData("_framework/blazor.boot.json", "essential")]
    [InlineData("_framework/Game.pdb", "optional")]
    [InlineData("_framework/dotnet.js.map", "optional")]
    [InlineData("_framework/aspnetcore-browser-refresh.js", "optional")]
    [InlineData("_framework/blazor-hotreload.js", "optional")]
    [InlineData("_framework/icudt.dat", "optional")]
    [InlineData("Content/Shader.xnb", "none")]
    public void ClassifyFrameworkUrl_Url_SeparatesBootEssentialFromOptional(string url, string expected)
    {
        // Only a file the runtime cannot start without may latch the failure screen; symbols,
        // source maps and dev-server helpers are optional.
        var result = RunNode($"out(b.classifyFrameworkUrl('{url}', 'https://game.test/index.html'));");

        result.ShouldBe($"\"{expected}\"");
    }

    [Fact]
    public void InstallFetchCounter_OptionalFrameworkFile404_IsReportedAsIgnoredNotFailure()
    {
        var result = RunNode(@"
            let failures = 0; const ignored = [];
            const win = {
                location: { href: 'https://game.test/index.html' },
                fetch: () => Promise.resolve(new Response('', { status: 404 })),
            };
            b.installFetchCounter(win, { onBytes: () => {}, onFail: () => failures++, onIgnored: (rule, info) => ignored.push([rule, info.status]) }, () => true);
            win.fetch('_framework/Game.pdb').then(() => new Promise((r) => setTimeout(r, 20))).then(() => out({ failures, ignored }));");

        result.ShouldBe("{\"failures\":0,\"ignored\":[[\"fetch-non-ok\",404]]}");
    }

    [Fact]
    public void InstallFetchCounter_EssentialFile404_ReportsRuleUrlAndStatus()
    {
        var result = RunNode(@"
            let reported;
            const win = {
                location: { href: 'https://game.test/index.html' },
                fetch: () => Promise.resolve(new Response('', { status: 404 })),
            };
            b.installFetchCounter(win, { onBytes: () => {}, onFail: (rule, info) => reported = [rule, info.status, info.url] }, () => true);
            win.fetch('_framework/dotnet.native.abc.wasm').then(() => new Promise((r) => setTimeout(r, 20))).then(() => out(reported));");

        result.ShouldBe("[\"fetch-non-ok\",404,\"_framework/dotnet.native.abc.wasm\"]");
    }

    [Fact]
    public void Controller_SkipTick_SkipsTheFirstTickOnlyOnce()
    {
        // The host asks before every tick. Skipping the first one gives the browser a frame to
        // paint the screen before engine init blocks the thread; skipping more would just stall.
        var result = RunNode(@"
            const c = b.createController({ now: () => 0, onHide: () => {} });
            const asked = [];
            for (let i = 0; i < 4; i++) { c.beforeTick(); asked.push(c.skipTick()); }
            out(asked);");

        result.ShouldBe("[true,false,false,false]");
    }

    [Fact]
    public void Controller_SkipTick_HidesOnlyAfterTwoRealFrames()
    {
        // The skipped tick draws nothing, so it must not count toward the frames that prove the
        // game is on screen: hide on call 4, not call 3.
        var result = RunNode(@"
            const c = b.createController({ now: () => 0, onHide: () => {} });
            const states = [];
            for (let i = 0; i < 4; i++) { c.beforeTick(); c.skipTick(); states.push(c.hidden); }
            out(states);");

        result.ShouldBe("[false,false,false,true]");
    }

    [Fact]
    public void Controller_SkipTick_FirstRealTickThrew_StillLatches()
    {
        // With the first tick skipped, the Game is built in tick 2, so its exception arrives
        // after the second frbBeforeTick. That is still a boot failure, not a running game.
        var result = RunNode(@"
            const c = b.createController({ now: () => 0, onHide: () => {}, log: () => {} });
            c.beforeTick(); c.skipTick();
            c.beforeTick(); c.skipTick();
            c.fail('frbBootFailed', { message: 'InvalidOperationException: boom' });
            for (let i = 0; i < 5; i++) { c.beforeTick(); c.skipTick(); }
            out({ failed: c.failed, hidden: c.hidden });");

        result.ShouldBe("{\"failed\":true,\"hidden\":false}");
    }

    [Fact]
    public void Controller_SkipTick_FailureAfterOneRealFrame_IsIgnored()
    {
        var result = RunNode(@"
            const c = b.createController({ now: () => 0, onHide: () => {}, log: () => {} });
            c.beforeTick(); c.skipTick();
            c.beforeTick(); c.skipTick();
            c.beforeTick(); c.skipTick();
            c.fail('blazor-error-ui', { message: 'late' });
            out({ failed: c.failed });");

        result.ShouldBe("{\"failed\":false}");
    }

    [Fact]
    public void Controller_AlreadyFailed_DoesNotSkipATick()
    {
        var result = RunNode(@"
            const c = b.createController({ now: () => 0, onHide: () => {}, log: () => {} });
            c.fail('x');
            c.beforeTick();
            out(c.skipTick());");

        result.ShouldBe("false");
    }

    [Fact]
    public void Controller_HasNoPhaseState()
    {
        // The engine's init is one synchronous block, so a phase line could never paint while it
        // changes. The screen shows "Loading" and the download size only.
        var result = RunNode(@"
            const c = b.createController({ now: () => 0, onHide: () => {} });
            out({ setPhase: typeof c.setPhase, phase: 'phase' in c });");

        result.ShouldBe("{\"setPhase\":\"undefined\",\"phase\":false}");
    }

    // A fake DOM just big enough for createView: elements record their children, style, attributes
    // and click listeners, so a test can render a state and then "click" the Reload button.
    private const string FakeDom = @"
            const made = [];
            const node = (tag) => { const n = { tag, className: '', style: {}, children: [], attrs: {}, listeners: {}, textContent: '',
                appendChild(c) { this.children.push(c); return c; }, setAttribute(k, v) { this.attrs[k] = v; },
                addEventListener(type, fn) { this.listeners[type] = fn; }, focus() { this.focused = true; }, parentNode: null }; made.push(n); return n; };
            const doc = { createElement: node, body: node('body') };
            const reloadNode = () => made.find((n) => n.className === 'frb-reload');";

    [Fact]
    public void CreateView_Render_NotFailed_ReloadControlIsHidden()
    {
        // Reload exists only for the failure screen. While loading it must not be visible or
        // reachable by Tab (display:none removes it from both).
        var result = RunNode(FakeDom + @"
            const view = b.createView(doc, () => {});
            view.render({ bytes: 2048, failed: false, failure: null });
            out({ tag: reloadNode().tag, display: reloadNode().style.display });");

        result.ShouldBe("{\"tag\":\"button\",\"display\":\"none\"}");
    }

    [Fact]
    public void CreateView_Render_Failed_ShowsReloadButtonLabelledReload()
    {
        var result = RunNode(FakeDom + @"
            const view = b.createView(doc, () => {});
            view.render({ bytes: 0, failed: true, failure: { rule: 'x' } });
            const r = reloadNode();
            out({ display: r.style.display, text: r.textContent, type: r.attrs.type });");

        result.ShouldBe("{\"display\":\"\",\"text\":\"Reload\",\"type\":\"button\"}");
    }

    [Fact]
    public void CreateView_Failed_ActivatingReload_CallsTheReloadCallbackOnce()
    {
        // A real <button> turns Enter and Space into a click, so the click handler is the whole
        // keyboard story.
        var result = RunNode(FakeDom + @"
            let reloads = 0;
            const view = b.createView(doc, () => reloads++);
            view.render({ bytes: 0, failed: true, failure: { rule: 'x' } });
            reloadNode().listeners.click();
            out(reloads);");

        result.ShouldBe("1");
    }

    [Fact]
    public void CreateView_FailedThenLoadingState_NeverShowsReloadWhileLoading()
    {
        var result = RunNode(FakeDom + @"
            const view = b.createView(doc, () => {});
            const seen = [];
            for (const failed of [false, true, false]) { view.render({ bytes: 0, failed, failure: failed ? { rule: 'x' } : null }); seen.push(reloadNode().style.display); }
            out(seen);");

        result.ShouldBe("[\"none\",\"\",\"none\"]");
    }

    [Fact]
    public void CreateView_Render_ShowsLoadingAndSizeWithNoPhaseLine()
    {
        var result = RunNode(FakeDom + @"
            const view = b.createView(doc, () => {});
            view.render({ bytes: 2048, failed: false, failure: null });
            const classes = made.map((n) => n.className).filter(Boolean);
            const texts = made.filter((n) => n.style.display !== 'none').map((n) => n.textContent).filter((t) => t && !t.includes('{'));
            out({ phaseElement: classes.includes('frb-phase'), texts });");

        result.ShouldBe("{\"phaseElement\":false,\"texts\":[\"Loading\",\"2 KB\"]}");
    }

    [Theory]
    [InlineData("src/FlatRedBall2.BlazorGL/wwwroot/frb-host.js")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/wwwroot/frb-host.js")]
    public void FrbHost_TickJs_SkipsTheTickWhenTheHookSaysSo(string relativePath)
    {
        // The loop still schedules the next frame when a tick is skipped; only the .NET call is
        // withheld. Without the hook (no boot screen, or an older one) every frame ticks.
        var hostPath = Path.Combine(TemplatePackageReferenceTests.RepoRootForTests, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var result = RunNodeRaw(@"
            const vm = require('vm'); const fs = require('fs');
            const run = (skipHook) => {
                let ticks = 0, scheduled = 0;
                const win = { addEventListener: () => {}, requestAnimationFrame: () => scheduled++, theInstance: { invokeMethod: () => ticks++ } };
                if (skipHook) { let first = true; win.frbSkipTick = () => { const s = first; first = false; return s; }; }
                const ctx = { window: win, document: { write: () => {}, querySelector: () => null }, console };
                vm.runInNewContext(fs.readFileSync(process.env.HOST_PATH, 'utf8') + '\n;globalThis.__tick = tickJS;', ctx);
                ctx.__tick(); ctx.__tick();
                return { ticks, scheduled };
            };
            process.stdout.write(JSON.stringify({ withHook: run(true), withoutHook: run(false) }));", hostPath);

        result.ShouldBe("{\"withHook\":{\"ticks\":1,\"scheduled\":2},\"withoutHook\":{\"ticks\":2,\"scheduled\":2}}");
    }

    private const string HostStartHarness = @"
        const vm = require('vm'); const fs = require('fs');
        // autostart: the value of the blazor script tag's autostart attribute (null = tag has none,
        // undefined = no tag at all).
        const run = (autostart, search = '') => {
            const started = [], configs = [];
            const tag = autostart === undefined ? null : { getAttribute: (n) => (n === 'autostart' ? autostart : null) };
            const win = { addEventListener: () => {}, requestAnimationFrame: () => {}, location: { search },
                Blazor: { start: (o) => { started.push(o); return Promise.resolve(); } } };
            const ctx = { window: win, document: { write: () => {}, querySelector: () => tag }, console };
            vm.runInNewContext(fs.readFileSync(process.env.HOST_PATH, 'utf8'), ctx);
            if (started.length) started[0].configureRuntime({ withConfig: (c) => configs.push(c) });
            return { starts: started.length, configs };
        };";

    [Theory]
    [InlineData("src/FlatRedBall2.BlazorGL/wwwroot/frb-host.js")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/wwwroot/frb-host.js")]
    public void FrbHost_AutostartDisabled_StartsBlazorWithParallelDownloadsBelowKestrelStreamLimit(string relativePath)
    {
        // Blazor's own loader sets maxParallelDownloads to 1,000,000 and disables retry, so a dev
        // server with 100 HTTP/2 streams queues the rest of the _framework/ burst, and a browser
        // cert-verifier rebuild then fails every queued request (#1467). The cap must stay under 100.
        var hostPath = Path.Combine(TemplatePackageReferenceTests.RepoRootForTests, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var result = RunNodeRaw(HostStartHarness + @"
            const r = run('false');
            const caps = r.configs.map((c) => c.maxParallelDownloads);
            process.stdout.write(JSON.stringify({ starts: r.starts, capped: caps.length === 1 && caps[0] > 0 && caps[0] < 100 }));", hostPath);

        result.ShouldBe("{\"starts\":1,\"capped\":true}");
    }

    [Theory]
    [InlineData("src/FlatRedBall2.BlazorGL/wwwroot/frb-host.js")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/wwwroot/frb-host.js")]
    public void FrbHost_AutostartDisabled_TurnsDebuggerSupportOffUnlessTheUrlAsksForIt(string relativePath)
    {
        // The dev server's runtime config has debugLevel -1 (debugger support), which makes interpreted
        // code 2-6x slower, start-up included. The host defaults it off; ?debug brings it back.
        var hostPath = Path.Combine(TemplatePackageReferenceTests.RepoRootForTests, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var result = RunNodeRaw(HostStartHarness + @"
            const level = (search) => run('false', search).configs[0].debugLevel;
            process.stdout.write(JSON.stringify([level(''), level('?debug'), level('?x=1&debug'), level('?debugger=1')]));", hostPath);

        result.ShouldBe("[0,-1,-1,0]");
    }

    [Theory]
    [InlineData("src/FlatRedBall2.BlazorGL/wwwroot/frb-host.js")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/wwwroot/frb-host.js")]
    public void FrbHost_AutostartNotDisabled_LeavesBlazorAloneInsteadOfStartingItTwice(string relativePath)
    {
        // A page that still lets Blazor autostart has already started it; a second Blazor.start
        // throws "Blazor has already started".
        var hostPath = Path.Combine(TemplatePackageReferenceTests.RepoRootForTests, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var result = RunNodeRaw(HostStartHarness + @"
            process.stdout.write(JSON.stringify({ noAttribute: run(null).starts, noTag: run(undefined).starts }));", hostPath);

        result.ShouldBe("{\"noAttribute\":0,\"noTag\":0}");
    }

    [Fact]
    public void FormatBytes_Sizes_UsesKbBelowOneMbAndOneDecimalMbAbove()
    {
        var result = RunNode(@"out([b.formatBytes(0), b.formatBytes(2048), b.formatBytes(32715571)]);");

        result.ShouldBe("[\"\",\"2 KB\",\"31.2 MB\"]");
    }
}
