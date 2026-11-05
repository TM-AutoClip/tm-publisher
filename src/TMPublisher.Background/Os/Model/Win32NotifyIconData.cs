using System.Runtime.InteropServices;

namespace TMPublisher.Background.Os;

/// <summary>Win32 <c>NOTIFYICONDATAW</c> (Vista and later layout).</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct Win32NotifyIconData
{
    public uint Size;
    public nint Window;
    public uint Id;
    public uint Flags;
    public uint CallbackMessage;
    public nint Icon;
    public fixed char Tip[128];
    public uint State;
    public uint StateMask;
    public fixed char Info[256];
    public uint TimeoutOrVersion;
    public fixed char InfoTitle[64];
    public uint InfoFlags;
    public Guid Item;
    public nint BalloonIcon;
}
