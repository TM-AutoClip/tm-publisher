using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TMPublisher.Background.Os;

/// <summary>The few Win32 calls of the hidden message window and the tray icon (source-generated P/Invoke: no runtime marshalling code).</summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class Win32Native
{
    public const uint WmDestroy = 0x0002;
    public const uint WmClose = 0x0010;
    public const uint WmQueryEndSession = 0x0011;
    public const uint WmEndSession = 0x0016;
    public const uint WmTimeChange = 0x001E;
    public const uint WmPowerBroadcast = 0x0218;
    public const uint WmApp = 0x8000;
    public const uint WmLeftButtonUp = 0x0202;
    public const uint WmLeftButtonDoubleClick = 0x0203;
    public const uint WmRightButtonUp = 0x0205;
    public const uint WmContextMenu = 0x007B;
    public const nint PowerResumeAutomatic = 0x12;
    public const uint NotifyAdd = 0;
    public const uint NotifyModify = 1;
    public const uint NotifyDelete = 2;
    public const uint NotifyFlagMessage = 0x1;
    public const uint NotifyFlagIcon = 0x2;
    public const uint NotifyFlagTip = 0x4;
    public const uint MenuString = 0x0;
    public const uint MenuSeparator = 0x800;
    public const uint TrackRightButton = 0x2;
    public const uint TrackReturnCommand = 0x100;
    public const uint TrackBottomAlign = 0x20;

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW")]
    public static partial nint GetModuleHandle(nint name);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    public static partial ushort RegisterClass(Win32WindowClass* windowClass);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true)]
    public static partial nint CreateWindow(int exStyle, char* className, char* windowName, int style, int x, int y, int width, int height, nint parent,
        nint menu, nint instance, nint parameter);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static partial nint DefaultWindowProc(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    public static partial int GetMessage(Win32Message* message, nint window, uint min, uint max);

    [LibraryImport("user32.dll", EntryPoint = "TranslateMessage")]
    public static partial int TranslateMessage(Win32Message* message);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    public static partial nint DispatchMessage(Win32Message* message);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    public static partial int PostMessage(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "PostQuitMessage")]
    public static partial void PostQuitMessage(int exitCode);

    [LibraryImport("user32.dll", EntryPoint = "DestroyWindow", SetLastError = true)]
    public static partial int DestroyWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessage(string name);

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    public static partial int ShellNotifyIcon(uint message, Win32NotifyIconData* data);

    [LibraryImport("shell32.dll", EntryPoint = "ExtractIconExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint ExtractIcons(string file, int index, nint* large, nint* small, uint count);

    [LibraryImport("user32.dll", EntryPoint = "LoadIconW")]
    public static partial nint LoadIcon(nint instance, nint name);

    [LibraryImport("user32.dll", EntryPoint = "DestroyIcon")]
    public static partial int DestroyIcon(nint icon);

    [LibraryImport("user32.dll", EntryPoint = "CreatePopupMenu", SetLastError = true)]
    public static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial int AppendMenu(nint menu, uint flags, nuint id, string? text);

    [LibraryImport("user32.dll", EntryPoint = "TrackPopupMenu", SetLastError = true)]
    public static partial int TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint window, nint rectangle);

    [LibraryImport("user32.dll", EntryPoint = "DestroyMenu")]
    public static partial int DestroyMenu(nint menu);

    [LibraryImport("user32.dll", EntryPoint = "GetCursorPos")]
    public static partial int GetCursorPos(Win32Point* point);

    [LibraryImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    public static partial int SetForegroundWindow(nint window);
}
