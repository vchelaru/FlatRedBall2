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
    public void FormatBytes_Sizes_UsesKbBelowOneMbAndOneDecimalMbAbove()
    {
        var result = RunNode(@"out([b.formatBytes(0), b.formatBytes(2048), b.formatBytes(32715571)]);");

        result.ShouldBe("[\"\",\"2 KB\",\"31.2 MB\"]");
    }
}
