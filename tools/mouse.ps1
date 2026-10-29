# Real mouse input for UI tests: moves the cursor to a pixel in the game window's client area and clicks.
# Usage: mouse.ps1 <x> <y> [click|move]     (x, y from the client area's top-left, as the mod's "where" prints)
param([int]$x, [int]$y, [string]$what = "click")
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class M {
  [StructLayout(LayoutKind.Sequential)] public struct P { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref P p);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[M]::SetProcessDPIAware() | Out-Null   # game pixels, not scaled desktop pixels
$p = Get-Process Giggleland -ErrorAction Stop | Select-Object -First 1
$h = $p.MainWindowHandle
[M]::SetForegroundWindow($h) | Out-Null
$pt = New-Object M+P; $pt.X = $x; $pt.Y = $y
[M]::ClientToScreen($h, [ref]$pt) | Out-Null
[M]::SetCursorPos($pt.X - 6, $pt.Y - 4) | Out-Null; Start-Sleep -Milliseconds 120
[M]::SetCursorPos($pt.X, $pt.Y) | Out-Null; Start-Sleep -Milliseconds 200
if ($what -eq "click") {
  [M]::mouse_event(0x2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 80
  [M]::mouse_event(0x4, 0, 0, 0, [IntPtr]::Zero)
}
"$what at client $x,$y (screen $($pt.X),$($pt.Y))"
