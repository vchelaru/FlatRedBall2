using System;
using System.Runtime.InteropServices;

namespace AnimationEditor.App;

/// <summary>
/// Applies macOS-specific Dock customisations directly via Objective-C runtime P/Invoke,
/// working around Avalonia 12 limitations with <c>WindowDecorations="None"</c>.
///
/// <para><b>Dock label</b> — <see cref="SetProcessName"/> must be called at the very
/// start of <c>Main()</c>, BEFORE any Avalonia code runs. The macOS Dock reads and
/// caches the app label when <c>[NSApplication sharedApplication]</c> is first called
/// (inside <c>UsePlatformDetect()</c>). Setting the process name after that point has
/// no visible effect. Foundation classes are available from the dyld shared cache on
/// macOS 12+ without an explicit <c>dlopen</c>.</para>
///
/// <para><b>Dock icon</b> — <see cref="Set"/> must be called via
/// <c>Dispatcher.UIThread.Post</c> inside <c>OnFrameworkInitializationCompleted</c>,
/// AFTER <c>desktop.MainWindow</c> is assigned, so it runs after Avalonia's own
/// NSApplication initialisation (which may clear the icon).</para>
/// </summary>
internal static class MacOSDockIcon
{
    // Multiple DllImport declarations can share the same EntryPoint with different
    // .NET names and return types — they resolve to the same native function.
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend(IntPtr self, IntPtr op);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend1(IntPtr self, IntPtr op, IntPtr a1);

    // 2-arg variant used for NSData.dataWithBytes:length: (buf ptr + NSUInteger length).
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend2(IntPtr self, IntPtr op, IntPtr a1, IntPtr a2);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void MsgSendVoid1(IntPtr self, IntPtr op, IntPtr a1);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr sel_registerName(string name);

    /// <summary>
    /// Sets the macOS Dock label by changing the process name BEFORE
    /// <c>[NSApplication sharedApplication]</c> is called. Call at the very start of
    /// <c>Main()</c>. Foundation classes (NSProcessInfo, NSString) are accessible from
    /// the dyld shared cache on macOS 12+ without an explicit framework load.
    /// Safe to call on non-macOS platforms (returns immediately).
    /// </summary>
    public static void SetProcessName(string name)
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var nameStr = ToNSString(name);
        if (nameStr == IntPtr.Zero)
            return;

        var processInfo = MsgSend(
            objc_getClass("NSProcessInfo"),
            sel_registerName("processInfo"));

        if (processInfo == IntPtr.Zero)
            return;

        MsgSendVoid1(processInfo, sel_registerName("setProcessName:"), nameStr);
    }

    /// <summary>
    /// Creates an NSImage from raw image bytes (PNG, TIFF, etc.) and assigns it to
    /// <c>NSApplication.sharedApplication.applicationIconImage</c>. Safe to call on
    /// non-macOS platforms (returns immediately) and with an empty array.
    /// </summary>
    public static void Set(byte[] imageData)
    {
        if (!OperatingSystem.IsMacOS() || imageData.Length == 0)
            return;

        // AppKit must be loaded before NSApplication / NSImage are addressable.
        NativeLibrary.TryLoad(
            "/System/Library/Frameworks/AppKit.framework/AppKit", out _);

        var buf = Marshal.AllocHGlobal(imageData.Length);
        try
        {
            Marshal.Copy(imageData, 0, buf, imageData.Length);

            var nsData = MsgSend2(
                objc_getClass("NSData"),
                sel_registerName("dataWithBytes:length:"),
                buf,
                (IntPtr)imageData.Length);

            if (nsData == IntPtr.Zero)
                return;

            var image = MsgSend1(
                MsgSend(objc_getClass("NSImage"), sel_registerName("alloc")),
                sel_registerName("initWithData:"),
                nsData);

            if (image == IntPtr.Zero)
                return;

            var sharedApp = MsgSend(
                objc_getClass("NSApplication"),
                sel_registerName("sharedApplication"));

            MsgSendVoid1(sharedApp, sel_registerName("setApplicationIconImage:"), image);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private static IntPtr ToNSString(string value)
    {
        var ptr = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return MsgSend1(
                objc_getClass("NSString"),
                sel_registerName("stringWithUTF8String:"),
                ptr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(ptr);
        }
    }
}
