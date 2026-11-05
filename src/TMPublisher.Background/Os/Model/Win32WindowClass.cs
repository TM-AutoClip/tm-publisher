using System.Runtime.InteropServices;

namespace TMPublisher.Background.Os;

/// <summary>Win32 <c>WNDCLASSEXW</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Win32WindowClass
{
    public uint Size;
    public uint Style;
    public delegate* unmanaged<nint, uint, nint, nint, nint> WindowProc;
    public int ClassExtra;
    public int WindowExtra;
    public nint Instance;
    public nint Icon;
    public nint Cursor;
    public nint Background;
    public char* MenuName;
    public char* ClassName;
    public nint SmallIcon;
}
