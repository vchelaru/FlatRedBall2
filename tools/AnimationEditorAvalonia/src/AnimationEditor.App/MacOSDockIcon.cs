using System;
using System.Runtime.InteropServices;

namespace AnimationEditor.App;

/// <summary>
/// Applies macOS-specific app metadata directly via Objective-C runtime P/Invoke.
///
/// Avalonia 12 with <c>WindowDecorations="None"</c> does not propagate
/// <c>Window.Icon</c> to <c>NSApplication.sharedApplication.applicationIconImage</c>
/// because it only does so through the native NSWindow title-bar code path, which is
/// skipped when the native chrome is hidden. We bypass Avalonia entirely and call
/// AppKit directly.
///
/// Call order matters:
/// <list type="bullet">
///   <item><see cref="SetProcessName"/> — call at the start of <c>Main()</c>, before
///   Avalonia starts, so the Dock label is correct from the first frame.</item>
///   <item><see cref="Set"/> — call via <c>Dispatcher.UIThread.Post</c> inside
///   <c>OnFrameworkInitializationCompleted</c>, AFTER <c>desktop.MainWindow</c> is
///   assigned. Posting to the next UI tick ensures Avalonia's own NSApplication
///   initialisation (which may clear the icon) has already completed.</item>
/// </list>
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

    /// <summary>
    /// Sets the macOS process name that the Dock displays under the app icon. Call
    /// this at the start of <c>Main()</c>, before any Avalonia initialisation, so the
    /// label is correct from first launch.
    /// </summary>
    public static void SetProcessName(string name)
    {
        if (!OperatingSystem.IsMacOS())
            return;

        // Foundation is available even before AppKit, and NSProcessInfo lives there.
        NativeLibrary.TryLoad(
            "/System/Library/Frameworks/Foundation.framework/Foundation", out _);

        var nameStr = ToNSString(name);
        if (nameStr == IntPtr.Zero)
            return;

        var processInfo = MsgSend(
            objc_getClass("NSProcessInfo"),
            sel_registerName("processInfo"));

        MsgSendVoid1(processInfo, sel_registerName("setProcessName:"), nameStr);
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
