using System.Runtime.InteropServices;

namespace TMPublisher.Background.Os;

/// <summary>Win32 <c>POINT</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Win32Point
{
    public int X;
    public int Y;
}
