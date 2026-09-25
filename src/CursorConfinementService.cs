using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace GammaControl;

internal sealed class CursorConfinementService : IDisposable
{
    private Rectangle? _ownedBounds;
    private Rectangle? _lastOwnedBounds;

    internal bool IsActive => _ownedBounds.HasValue;
    internal Rectangle? CurrentBounds => _ownedBounds;

    internal void Confine(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds));
        }

        var rect = ToNative(bounds);
        if (!NativeMethods.ClipCursor(ref rect))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        _ownedBounds = bounds;
        _lastOwnedBounds = bounds;
    }

    internal void Release()
    {
        if (_ownedBounds is not { } bounds)
        {
            // Windows may collapse a released clip later, when the former target is
            // minimized. Clear only a point clip inside our last owned rectangle.
            if (_lastOwnedBounds is { } lastBounds)
            {
                if (!NativeMethods.GetClipCursor(out var laterClip))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (ShouldClearCollapsedClip(lastBounds, laterClip) &&
                    !NativeMethods.ClipCursor(IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return;
        }

        // Windows can collapse a clip to a single point while its window is minimized.
        // Clear that unusable clip, but leave a different valid clip owned by another app.
        if (!NativeMethods.GetClipCursor(out var current))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if ((current.Equals(ToNative(bounds)) ||
             current.Right <= current.Left || current.Bottom <= current.Top) &&
            !NativeMethods.ClipCursor(IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        _ownedBounds = null;
    }

    internal static NativeMethods.NativeRect ToNative(Rectangle bounds) => new()
    {
        Left = bounds.Left,
        Top = bounds.Top,
        Right = bounds.Right,
        Bottom = bounds.Bottom
    };

    internal static bool ShouldClearCollapsedClip(Rectangle lastOwnedBounds, NativeMethods.NativeRect current) =>
        (current.Right <= current.Left || current.Bottom <= current.Top) &&
        lastOwnedBounds.Contains(current.Left, current.Top);

    public void Dispose() => Release();
}
