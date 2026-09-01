# Capture one native Caves of Qud game window without activating it.
#
# The scenario launcher deliberately keeps scripted runs small and behind the operator's other
# windows.  A visual-evidence run calls this helper after its journal reaches a terminal row: the
# helper enlarges that same native window, lets Unity redraw, and captures the rendered window with
# PrintWindow.  It never synthesises, recolours, annotates, or judges the image.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Output,
    [string]$ProcessName = 'CoQ',
    [int]$Width = 2560,
    [int]$Height = 1440,
    [int]$RedrawMilliseconds = 2500
)

$ErrorActionPreference = 'Stop'
if ($Width -lt 800 -or $Width -gt 7680 -or $Height -lt 600 -or $Height -gt 4320) {
    throw "Capture dimensions are outside the supported 800x600..7680x4320 range"
}
if ($RedrawMilliseconds -lt 250 -or $RedrawMilliseconds -gt 15000) {
    throw "RedrawMilliseconds must be in 250..15000"
}

$parent = Split-Path -Parent $Output
if ([string]::IsNullOrWhiteSpace($parent) -or
    -not (Test-Path -LiteralPath $parent -PathType Container)) {
    throw "Capture output parent does not exist: $parent"
}
if (Test-Path -LiteralPath $Output) {
    throw "Capture output already exists: $Output"
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class TafNativeCapture {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr handle, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PrintWindow(IntPtr handle, IntPtr target, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr handle, IntPtr after,
        int x, int y, int width, int height, uint flags);
}
'@

$deadline = (Get-Date).AddSeconds(30)
$process = $null
while ((Get-Date) -lt $deadline) {
    $process = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } |
        Sort-Object StartTime | Select-Object -Last 1)
    if ($null -ne $process -and $process.Count -gt 0) { break }
    Start-Sleep -Milliseconds 250
}
if ($null -eq $process -or $process.Count -eq 0) {
    throw "No visible $ProcessName window appeared within 30 seconds"
}
$process = $process[0]
$handle = $process.MainWindowHandle

$area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$captureWidth = [Math]::Min($Width, $area.Width)
$captureHeight = [Math]::Min($Height, $area.Height)
$x = $area.Left + [Math]::Max(0, [int](($area.Width - $captureWidth) / 2))
$y = $area.Top + [Math]::Max(0, [int](($area.Height - $captureHeight) / 2))
# HWND_BOTTOM plus SWP_NOACTIVATE: keep the operator's foreground and capture the native window.
if (-not [TafNativeCapture]::SetWindowPos(
        $handle, [IntPtr]1, $x, $y, $captureWidth, $captureHeight, 0x10)) {
    throw "SetWindowPos failed with Win32 error $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
}
Start-Sleep -Milliseconds $RedrawMilliseconds
$process.Refresh()
$handle = $process.MainWindowHandle

$rect = [TafNativeCapture+RECT]::new()
if (-not [TafNativeCapture]::GetWindowRect($handle, [ref]$rect)) {
    throw "GetWindowRect failed with Win32 error $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
}
$bitmapWidth = $rect.Right - $rect.Left
$bitmapHeight = $rect.Bottom - $rect.Top
if ($bitmapWidth -lt 1 -or $bitmapHeight -lt 1) {
    throw "Native window reported invalid dimensions ${bitmapWidth}x${bitmapHeight}"
}

$bitmap = [System.Drawing.Bitmap]::new(
    $bitmapWidth, $bitmapHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$target = $graphics.GetHdc()
try {
    # PW_RENDERFULLCONTENT asks the owning application to render the complete native window.
    if (-not [TafNativeCapture]::PrintWindow($handle, $target, 2)) {
        throw "PrintWindow failed with Win32 error $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
    }
} finally {
    $graphics.ReleaseHdc($target)
    $graphics.Dispose()
}
try {
    $bitmap.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $bitmap.Dispose()
}

$saved = Get-Item -LiteralPath $Output
if ($saved.Length -lt 1000) {
    throw "Captured PNG is unexpectedly small: $($saved.Length) bytes"
}
Write-Host "Captured native $ProcessName window: $($saved.FullName) (${bitmapWidth}x${bitmapHeight}, $($saved.Length) bytes)"
