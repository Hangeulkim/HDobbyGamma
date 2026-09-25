using System.Runtime.InteropServices;
using System.Text;

namespace GammaControl;

internal static class NativeMethods
{
    internal const uint MonitorInfoPrimary = 0x00000001;
    internal const uint EddGetDeviceInterfaceName = 0x00000001;
    internal const uint QdcOnlyActivePaths = 0x00000002;
    internal const uint DisplayConfigDeviceInfoGetSourceName = 1;
    internal const uint DisplayConfigDeviceInfoGetTargetName = 2;
    internal const int ErrorSuccess = 0;
    internal const int ErrorInsufficientBuffer = 122;
    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModNoRepeat = 0x4000;
    internal const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(IntPtr window, ref PointL point);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryFullProcessImageName(
        IntPtr process, uint flags, StringBuilder path, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr handle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClipCursor(ref NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClipCursor(IntPtr rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClipCursor(out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr window, int id);

    internal delegate bool MonitorEnumProc(
        IntPtr monitor,
        IntPtr monitorDc,
        ref NativeRect monitorRect,
        IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr clipRect,
        MonitorEnumProc callback,
        IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(
        IntPtr monitor,
        ref MonitorInfoEx monitorInfo);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayDevices(
        string? device,
        uint deviceIndex,
        ref DisplayDevice displayDevice,
        uint flags);

    [DllImport("user32.dll")]
    internal static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint pathCount,
        out uint modeCount);

    [DllImport("user32.dll")]
    internal static extern int QueryDisplayConfig(
        uint flags,
        ref uint pathCount,
        [In, Out] DisplayConfigPathInfo[] paths,
        ref uint modeCount,
        [In, Out] DisplayConfigModeInfo[] modes,
        IntPtr currentTopologyId);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    internal static extern int DisplayConfigGetSourceDeviceName(ref DisplayConfigSourceDeviceName request);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    internal static extern int DisplayConfigGetTargetDeviceName(ref DisplayConfigTargetDeviceName request);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateDC(
        string driver,
        string device,
        string? output,
        IntPtr initData);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetDeviceGammaRamp(IntPtr dc, IntPtr ramp);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetDeviceGammaRamp(IntPtr dc, IntPtr ramp);

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfoEx
    {
        internal int Size;
        internal NativeRect Monitor;
        internal NativeRect WorkArea;
        internal uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayDevice
    {
        internal int Size;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string DeviceString;

        internal uint StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string DeviceId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Luid
    {
        internal uint LowPart;
        internal int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigRational
    {
        internal uint Numerator;
        internal uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigRegion
    {
        internal uint Width;
        internal uint Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PointL
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RectL
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigPathSourceInfo
    {
        internal Luid AdapterId;
        internal uint Id;
        internal uint ModeInfoIndex;
        internal uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigPathTargetInfo
    {
        internal Luid AdapterId;
        internal uint Id;
        internal uint ModeInfoIndex;
        internal uint OutputTechnology;
        internal uint Rotation;
        internal uint Scaling;
        internal DisplayConfigRational RefreshRate;
        internal uint ScanLineOrdering;
        internal int TargetAvailable;
        internal uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigPathInfo
    {
        internal DisplayConfigPathSourceInfo SourceInfo;
        internal DisplayConfigPathTargetInfo TargetInfo;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigVideoSignalInfo
    {
        internal ulong PixelRate;
        internal DisplayConfigRational HorizontalSyncFrequency;
        internal DisplayConfigRational VerticalSyncFrequency;
        internal DisplayConfigRegion ActiveSize;
        internal DisplayConfigRegion TotalSize;
        internal uint VideoStandard;
        internal uint ScanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigTargetMode
    {
        internal DisplayConfigVideoSignalInfo TargetVideoSignalInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigSourceMode
    {
        internal uint Width;
        internal uint Height;
        internal uint PixelFormat;
        internal PointL Position;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigDesktopImageInfo
    {
        internal PointL PathSourceSize;
        internal RectL DesktopImageRegion;
        internal RectL DesktopImageClip;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct DisplayConfigModeUnion
    {
        [FieldOffset(0)] internal DisplayConfigTargetMode TargetMode;
        [FieldOffset(0)] internal DisplayConfigSourceMode SourceMode;
        [FieldOffset(0)] internal DisplayConfigDesktopImageInfo DesktopImageInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigModeInfo
    {
        internal uint InfoType;
        internal uint Id;
        internal Luid AdapterId;
        internal DisplayConfigModeUnion Mode;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigDeviceInfoHeader
    {
        internal uint Type;
        internal uint Size;
        internal Luid AdapterId;
        internal uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayConfigSourceDeviceName
    {
        internal DisplayConfigDeviceInfoHeader Header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayConfigTargetDeviceName
    {
        internal DisplayConfigDeviceInfoHeader Header;
        internal uint Flags;
        internal uint OutputTechnology;
        internal ushort EdidManufactureId;
        internal ushort EdidProductCodeId;
        internal uint ConnectorInstance;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        internal string MonitorFriendlyDeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string MonitorDevicePath;
    }
}
