using System.Runtime.InteropServices;

namespace TMPublisher.Background.Os;

/// <summary>Win32 <c>MSG</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Win32Message
{
    public nint Window;
    public uint Message;
    public nint WParam;
    public nint LParam;
    public uint Time;
    public Win32Point Point;
    public uint Private;
}
