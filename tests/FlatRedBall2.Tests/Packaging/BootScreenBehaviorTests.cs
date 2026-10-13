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
    public void FormatBytes_Sizes_UsesKbBelowOneMbAndOneDecimalMbAbove()
    {
        var result = RunNode(@"out([b.formatBytes(0), b.formatBytes(2048), b.formatBytes(32715571)]);");

        result.ShouldBe("[\"\",\"2 KB\",\"31.2 MB\"]");
    }
}
