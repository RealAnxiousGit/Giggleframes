# Crops a strip out of in-game screenshots (BepInEx\shots\<name>.png) for quick checks.
# Usage: crop.ps1 <y> <height> <name> [name...]   -> %TEMP%\claude\<name>-crop.png
param([int]$y, [int]$h)
Add-Type -AssemblyName System.Drawing
foreach ($n in $args) {
  $f = "F:\SteamLibrary\steamapps\common\Giggleland\BepInEx\shots\$n.png"
  for ($t = 0; $t -lt 50 -and -not (Test-Path $f); $t++) { Start-Sleep -Milliseconds 200 }   # written at end of frame
  Start-Sleep -Milliseconds 300
  $i =[Drawing.Image]::FromFile("F:\SteamLibrary\steamapps\common\Giggleland\BepInEx\shots\$n.png")
  $b = New-Object Drawing.Bitmap 1000, $h
  $g = [Drawing.Graphics]::FromImage($b)
  $g.DrawImage($i, (New-Object Drawing.Rectangle 0, 0, 1000, $h), (New-Object Drawing.Rectangle 0, $y, 1000, $h), [Drawing.GraphicsUnit]::Pixel)
  $b.Save("$env:TEMP\claude\$n-crop.png"); $g.Dispose(); $b.Dispose(); $i.Dispose()
}
