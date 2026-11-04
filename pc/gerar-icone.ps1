Add-Type -AssemblyName System.Drawing
$saida = $args[0]
$tamanhos = 256,64,48,32,16
$pngs = @()
foreach ($t in $tamanhos) {
  $bmp = New-Object Drawing.Bitmap $t,$t
  $g = [Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode='AntiAlias'; $g.TextRenderingHint='AntiAliasGridFit'
  $r = New-Object Drawing.RectangleF 0,0,$t,$t
  $raio = $t*0.22; $d=$raio*2
  $p = New-Object Drawing.Drawing2D.GraphicsPath
  $p.AddArc(0,0,$d,$d,180,90); $p.AddArc($t-$d-1,0,$d,$d,270,90); $p.AddArc($t-$d-1,$t-$d-1,$d,$d,0,90); $p.AddArc(0,$t-$d-1,$d,$d,90,90); $p.CloseFigure()
  $br = New-Object Drawing.Drawing2D.LinearGradientBrush $r,([Drawing.Color]::FromArgb(56,189,248)),([Drawing.Color]::FromArgb(29,78,216)),45
  $g.FillPath($br,$p)
  # faixa do calendário
  $g.SetClip($p); $g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(70,255,255,255))),0,0,$t,[int]($t*0.2)); $g.ResetClip()
  $f = New-Object Drawing.Font 'Segoe UI Black',([single]($t*0.52)),([Drawing.FontStyle]::Bold),([Drawing.GraphicsUnit]::Pixel)
  $sf = New-Object Drawing.StringFormat; $sf.Alignment='Center'; $sf.LineAlignment='Center'
  $g.DrawString('M',$f,[Drawing.Brushes]::White,(New-Object Drawing.RectangleF 0,($t*0.1),$t,($t*0.9)),$sf)
  $ms = New-Object IO.MemoryStream; $bmp.Save($ms,[Drawing.Imaging.ImageFormat]::Png); $pngs += ,($ms.ToArray())
}
$out = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$tamanhos.Count)
$off = 6 + 16*$tamanhos.Count
for ($i=0;$i -lt $tamanhos.Count;$i++){ $t=$tamanhos[$i]; $b=if($t -ge 256){0}else{$t}
  $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$off); $off += $pngs[$i].Length }
foreach ($p in $pngs){ $w.Write($p) }
[IO.File]::WriteAllBytes($saida, $out.ToArray())
