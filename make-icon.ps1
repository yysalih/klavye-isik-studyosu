# app.ico üretir (renkli tuşlu küçük klavye). Bir kez çalıştırmak yeterli.
Add-Type -AssemblyName System.Drawing
$sizes = 16, 24, 32, 48, 64, 256
$pngs = @()
foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 32.0
    function RoundRect($x, $y, $w, $h, $r) {
        $p = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = [Math]::Min($r * 2, [Math]::Min($w, $h))
        $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
        $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
        $p.CloseFigure(); return $p
    }
    $body = RoundRect (1 * $s) (6 * $s) (30 * $s) (20 * $s) (5 * $s)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(28, 30, 38))), $body)
    $cols = @([System.Drawing.Color]::FromArgb(255, 40, 120), [System.Drawing.Color]::FromArgb(160, 60, 255),
              [System.Drawing.Color]::FromArgb(0, 150, 255), [System.Drawing.Color]::FromArgb(0, 230, 200))
    for ($row = 0; $row -lt 3; $row++) {
        for ($col = 0; $col -lt 4; $col++) {
            $x = (4 + $col * 6.3) * $s; $y = (9 + $row * 5.3) * $s; $w = 5 * $s; $h = 4 * $s
            if ($row -eq 2 -and $col -eq 2) { continue }
            if ($row -eq 2 -and $col -eq 1) { $w = (5 + 6.3) * $s }
            $k = RoundRect $x $y $w $h (1.2 * $s)
            $g.FillPath((New-Object System.Drawing.SolidBrush $cols[($col + $row) % 4]), $k)
        }
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , @($size, $ms.ToArray())
    $bmp.Dispose()
}
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($p in $pngs) {
    $sz = $p[0]; $data = $p[1]
    $b = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$b); $bw.Write([byte]$b); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($p in $pngs) { $bw.Write([byte[]]$p[1]) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'app.ico'), $out.ToArray())
"app.ico olusturuldu"
