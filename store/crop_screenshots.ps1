# Makes raw phone screenshots Play-compliant.
#
# Play requires the longest side to be at most TWICE the shortest. A typical
# 1080x2340 phone screenshot is 2.17x and is rejected as-is, so this trims the
# excess evenly off the top and bottom -- which on most phones happens to remove
# exactly the status bar and the gesture bar. Screenshots also may not carry an
# alpha channel, so output is flattened to 24-bit.
#
#   drop raw captures in  store/screenshots/raw/
#   run this
#   upload from          store/screenshots/
param(
  [string]$Raw = (Join-Path $PSScriptRoot "screenshots\raw"),
  [string]$Out = (Join-Path $PSScriptRoot "screenshots"),
  # 0.5 splits the crop evenly; raise toward 1.0 to keep more of the TOP
  # (useful if your phone's gesture bar is thicker than its status bar).
  [double]$TopKeep = 0.5
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $Raw)) { New-Item -ItemType Directory -Path $Raw -Force | Out-Null }
if (-not (Test-Path $Out)) { New-Item -ItemType Directory -Path $Out -Force | Out-Null }

$files = Get-ChildItem -Path $Raw -Include *.png, *.jpg, *.jpeg -File -Recurse
if ($files.Count -eq 0) {
  Write-Host "No screenshots found in $Raw"
  Write-Host "Capture with Power + Volume Down, copy them there, and re-run."
  return
}

$n = 0
foreach ($f in $files) {
  $src = New-Object System.Drawing.Bitmap $f.FullName
  try {
    $w = $src.Width; $h = $src.Height

    # Clamp the LONG side to twice the short side.
    $cw = $w; $ch = $h
    if ($h -gt 2 * $w) { $ch = 2 * $w }
    if ($w -gt 2 * $h) { $cw = 2 * $h }

    $x = [int](($w - $cw) * 0.5)
    $y = [int](($h - $ch) * (1.0 - $TopKeep))

    $short = [Math]::Min($cw, $ch); $long = [Math]::Max($cw, $ch)
    if ($short -lt 320 -or $long -gt 3840) {
      Write-Warning ("{0}: {1}x{2} is outside Play's 320-3840 px bounds after cropping - skipped." -f $f.Name, $cw, $ch)
      continue
    }

    # 24bpp = no alpha channel, which is what Play requires for screenshots.
    $dst = New-Object System.Drawing.Bitmap $cw, $ch, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($dst)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($src,
      (New-Object System.Drawing.Rectangle 0, 0, $cw, $ch),
      (New-Object System.Drawing.Rectangle $x, $y, $cw, $ch),
      [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()

    $name = [System.IO.Path]::GetFileNameWithoutExtension($f.Name) + ".png"
    $target = Join-Path $Out $name
    $dst.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
    $dst.Dispose()

    $n++
    "{0,-28} {1}x{2} -> {3}x{4}  (ratio {5:N2}:1)" -f $f.Name, $w, $h, $cw, $ch, ($long / $short)
  }
  finally { $src.Dispose() }
}

Write-Host ""
Write-Host "$n screenshot(s) written to $Out"
if ($n -lt 2) { Write-Warning "Play needs at least 2 phone screenshots; 4+ at 1080px wide for promotional consideration." }
if ($n -gt 8) { Write-Warning "Play accepts at most 8 phone screenshots - upload your best 8." }
