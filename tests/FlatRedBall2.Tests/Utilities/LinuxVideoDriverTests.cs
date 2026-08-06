using System.Runtime.InteropServices;
using FlatRedBall2.Utilities;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Utilities;

public class LinuxVideoDriverTests
{
    // Environment.SetEnvironmentVariable only updates .NET's private environment table, not
    // the process's real C environment that a native library (SDL2, via getenv()) reads.
    // ApplyWaylandFallback must go through libc setenv() instead, so this checks the value via
    // getenv() directly rather than Environment.GetEnvironmentVariable. Only meaningful on
    // Linux (the divergence between the two stores is Unix-specific); no-ops elsewhere so the
    // suite still passes on a Windows dev box.
    [Fact]
    public void ApplyWaylandFallback_OnLinuxWithNoExistingValue_SetsRealProcessEnvironment()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        LinuxVideoDriver.ApplyWaylandFallback();

        Marshal.PtrToStringAnsi(getenv("SDL_VIDEODRIVER")).ShouldBe("wayland,x11");

        [DllImport("libc", EntryPoint = "getenv", CallingConvention = CallingConvention.Cdecl)]
        static extern System.IntPtr getenv(string name);
    }

    [Fact]
    public void GetVideoDriverToSet_NotLinux_ReturnsNull()
    {
        var result = LinuxVideoDriver.GetVideoDriverToSet(isLinux: false, existingSdlVideoDriver: null);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetVideoDriverToSet_LinuxWithNoExistingValue_ReturnsWaylandWithX11Fallback()
    {
        var result = LinuxVideoDriver.GetVideoDriverToSet(isLinux: true, existingSdlVideoDriver: null);

        result.ShouldBe("wayland,x11");
    }

    [Fact]
    public void GetVideoDriverToSet_LinuxWithExistingValue_ReturnsNullToRespectOverride()
    {
        var result = LinuxVideoDriver.GetVideoDriverToSet(isLinux: true, existingSdlVideoDriver: "x11");

        result.ShouldBeNull();
    }
}
