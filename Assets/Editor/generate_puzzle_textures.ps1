Add-Type -AssemblyName System.Drawing

$dir = "Assets/Textures/PuzzleTextures"
if (!(Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

# 1. Lock Housing Cutaway (1024 x 768)
function Create-LockHousing {
    param([string]$path)
    $w = 1024
    $h = 768
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    # Gunmetal gradient base
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 38, 42, 50)), ([System.Drawing.Color]::FromArgb(255, 20, 22, 28)), 90.0
    $g.FillRectangle($bgBrush, $rect)
    $bgBrush.Dispose()

    # Machined horizontal brushed steel streaks
    $penStreak = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(16, 255, 255, 255)), 1
    $rand = New-Object System.Random 12345
    for ($i = 0; $i -lt 400; $i++) {
        $y = $rand.Next(0, $h)
        $len = $rand.Next(120, $w)
        $x = $rand.Next(0, $w - $len)
        $g.DrawLine($penStreak, $x, $y, ($x + $len), $y)
    }
    $penStreak.Dispose()

    # Outer bevel border
    $borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 110, 120, 138)), 8
    $g.DrawRectangle($borderPen, 4, 4, ($w - 8), ($h - 8))
    $borderPen.Dispose()

    $innerBorderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 12, 14, 18)), 4
    $g.DrawRectangle($innerBorderPen, 10, 10, ($w - 20), ($h - 20))
    $innerBorderPen.Dispose()

    # 4 Corner Bolt Studs
    $boltBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 0, 44, 44), ([System.Drawing.Color]::FromArgb(255, 200, 210, 225)), ([System.Drawing.Color]::FromArgb(255, 60, 65, 75)), 45.0
    $boltDarkBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 20, 22, 28))
    $boltRimPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 230, 240, 255)), 2

    $bPositions = @(
        @(45, 45),
        @( ($w - 45), 45 ),
        @(45, ($h - 45) ),
        @( ($w - 45), ($h - 45) )
    )

    foreach ($bp in $bPositions) {
        $bx = [int]$bp[0]
        $by = [int]$bp[1]
        $g.FillEllipse($boltBrush, ($bx - 18), ($by - 18), 36, 36)
        $g.DrawEllipse($boltRimPen, ($bx - 18), ($by - 18), 36, 36)
        $g.FillEllipse($boltDarkBrush, ($bx - 8), ($by - 8), 16, 16)
    }
    $boltBrush.Dispose()
    $boltDarkBrush.Dispose()
    $boltRimPen.Dispose()

    # Brass Engraved Brand Plaque
    $plaqueX = [int]($w / 2 - 260)
    $plaqueY = 32
    $plaqueW = 520
    $plaqueH = 56
    $plaqueRect = New-Object System.Drawing.Rectangle $plaqueX, $plaqueY, $plaqueW, $plaqueH
    $plaqueBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $plaqueRect, ([System.Drawing.Color]::FromArgb(255, 220, 185, 85)), ([System.Drawing.Color]::FromArgb(255, 135, 100, 30)), 90.0
    $g.FillRectangle($plaqueBrush, $plaqueRect)
    $plaqueBrush.Dispose()

    $plaqueBorder = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 60, 45, 15)), 3
    $g.DrawRectangle($plaqueBorder, $plaqueRect)
    $plaqueBorder.Dispose()

    $font = [System.Drawing.Font]::new("Arial", [float]15, [System.Drawing.FontStyle]::Bold)
    $textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 40, 28, 10))
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("YALE & TOWNE // 5-PIN TUMBLER SECURITY CORE", $font, $textBrush, [System.Drawing.RectangleF]::new($plaqueX, $plaqueY, $plaqueW, $plaqueH), $sf)
    $font.Dispose()
    $textBrush.Dispose()

    # Cylinder Core Plug Cavity (Bottom)
    $plugX = 70
    $plugY = 470
    $plugW = $w - 140
    $plugH = 240
    $plugRect = New-Object System.Drawing.Rectangle $plugX, $plugY, $plugW, $plugH
    $plugBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $plugRect, ([System.Drawing.Color]::FromArgb(255, 16, 18, 22)), ([System.Drawing.Color]::FromArgb(255, 28, 32, 40)), 90.0
    $g.FillRectangle($plugBrush, $plugRect)
    $plugBrush.Dispose()

    $plugRim = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 75, 85, 100)), 4
    $g.DrawRectangle($plugRim, $plugRect)
    $plugRim.Dispose()

    # 5 Pin Channel Wells (Bible chambers)
    $chanSpacing = 160
    $chanStartX = 190
    $chanY = 120
    $chanW = 76
    $chanH = 340

    $wellBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 0, 80, 360), ([System.Drawing.Color]::FromArgb(255, 12, 14, 18)), ([System.Drawing.Color]::FromArgb(255, 24, 27, 34)), 0.0
    $wellBorder = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 60, 68, 80)), 3

    for ($p = 0; $p -lt 5; $p++) {
        $cx = $chanStartX + ($p * $chanSpacing)
        $g.FillRectangle($wellBrush, $cx, $chanY, $chanW, $chanH)
        $g.DrawRectangle($wellBorder, $cx, $chanY, $chanW, $chanH)
    }
    $wellBrush.Dispose()
    $wellBorder.Dispose()

    # Shear Line Alignment Strip
    $shearPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(200, 240, 190, 50)), 2
    $g.DrawLine($shearPen, 60, 465, ($w - 60), 465)
    $shearPen.Dispose()

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created Lock Housing: $path"
}

# 2. Tumbler Driver Pin (Hardened Stainless Steel)
function Create-DriverPin {
    param([string]$path)
    $w = 128
    $h = 256
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $rect = New-Object System.Drawing.Rectangle 10, 8, ($w - 20), ($h - 16)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 90, 95, 105)), ([System.Drawing.Color]::FromArgb(255, 225, 235, 248)), 0.0
    $g.FillRectangle($brush, $rect)
    $brush.Dispose()

    $groovePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(60, 0, 0, 0)), 2
    for ($y = 30; $y -lt $h - 30; $y += 24) {
        $g.DrawLine($groovePen, 10, $y, ($w - 10), $y)
    }
    $groovePen.Dispose()

    $rimPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 45, 50, 60)), 3
    $g.DrawRectangle($rimPen, 10, 8, ($w - 20), ($h - 16))
    $rimPen.Dispose()

    $specPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(180, 255, 255, 255)), 4
    $g.DrawLine($specPen, 38, 12, 38, ($h - 20))
    $specPen.Dispose()

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created Driver Pin: $path"
}

# 3. Tumbler Key Pin (Polished Brass with Conical Tip)
function Create-KeyPin {
    param([string]$path)
    $w = 128
    $h = 256
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $points = @(
        (New-Object System.Drawing.Point 10, 8),
        (New-Object System.Drawing.Point ($w - 10), 8),
        (New-Object System.Drawing.Point ($w - 10), ($h - 36)),
        (New-Object System.Drawing.Point ([int]($w / 2)), ($h - 8)),
        (New-Object System.Drawing.Point 10, ($h - 36))
    )

    $rect = New-Object System.Drawing.Rectangle 10, 8, ($w - 20), ($h - 16)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 175, 120, 30)), ([System.Drawing.Color]::FromArgb(255, 255, 225, 115)), 0.0
    $g.FillPolygon($brush, $points)
    $brush.Dispose()

    $groovePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(50, 80, 45, 10)), 2
    for ($y = 35; $y -lt $h - 50; $y += 24) {
        $g.DrawLine($groovePen, 10, $y, ($w - 10), $y)
    }
    $groovePen.Dispose()

    $rimPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 95, 65, 20)), 3
    $g.DrawPolygon($rimPen, $points)
    $rimPen.Dispose()

    $specPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(190, 255, 255, 200)), 4
    $g.DrawLine($specPen, 36, 12, 36, ($h - 40))
    $specPen.Dispose()

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created Key Pin: $path"
}

# 4. Tumbler Spring (Steel Coil)
function Create-PinSpring {
    param([string]$path)
    $w = 96
    $h = 256
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $wirePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 185, 195, 210)), 6
    $shadePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 60, 65, 75)), 7

    $coilCount = 10
    $step = ($h - 30) / $coilCount
    for ($c = 0; $c -lt $coilCount; $c++) {
        $y0 = 15 + ($c * $step)
        $y1 = $y0 + $step

        $g.DrawLine($shadePen, ($w - 16), $y0, 16, $y1)
        $g.DrawLine($wirePen, 16, $y0, ($w - 16), $y1)
    }
    $wirePen.Dispose()
    $shadePen.Dispose()

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created Pin Spring: $path"
}

# 5. Lockpick Tool (Grip Handle + Shaft + Hook Tip)
function Create-LockpickTool {
    param([string]$path)
    $w = 640
    $h = 96
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $midY = [int]($h / 2)

    # 1. Rubber diamond knurling handle (Left half: 0 to 280)
    $handleRect = New-Object System.Drawing.Rectangle 10, 16, 270, 64
    $hBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $handleRect, ([System.Drawing.Color]::FromArgb(255, 45, 48, 55)), ([System.Drawing.Color]::FromArgb(255, 15, 16, 20)), 90.0
    $g.FillRectangle($hBrush, $handleRect)
    $hBrush.Dispose()

    # Knurling criss-cross pattern
    $knurlPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(25, 255, 255, 255)), 1
    for ($k = -60; $k -lt 300; $k += 8) {
        $g.DrawLine($knurlPen, (10 + $k), 16, (10 + $k + 40), 80)
        $g.DrawLine($knurlPen, (10 + $k + 40), 16, (10 + $k), 80)
    }
    $knurlPen.Dispose()

    $hRimPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 80, 85, 95)), 2
    $g.DrawRectangle($hRimPen, $handleRect)
    $hRimPen.Dispose()

    # 2. Chrome Ferrule Collar
    $ferruleRect = New-Object System.Drawing.Rectangle 276, 24, 20, 48
    $fBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $ferruleRect, ([System.Drawing.Color]::FromArgb(255, 240, 245, 255)), ([System.Drawing.Color]::FromArgb(255, 90, 95, 105)), 90.0
    $g.FillRectangle($fBrush, $ferruleRect)
    $fBrush.Dispose()

    # 3. Spring Steel Blade Shaft (296 to 580)
    $shaftRect = New-Object System.Drawing.Rectangle 296, ($midY - 7), 284, 14
    $sBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $shaftRect, ([System.Drawing.Color]::FromArgb(255, 235, 242, 252)), ([System.Drawing.Color]::FromArgb(255, 120, 128, 140)), 90.0
    $g.FillRectangle($sBrush, $shaftRect)
    $sBrush.Dispose()

    $sRimPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 75, 80, 90)), 2
    $g.DrawRectangle($sRimPen, $shaftRect)
    $sRimPen.Dispose()

    # 4. Precision Hook Tip (580 to 625)
    $hookPoints = @(
        (New-Object System.Drawing.Point 578, ($midY - 7)),
        (New-Object System.Drawing.Point 612, ($midY - 7)),
        (New-Object System.Drawing.Point 626, ($midY - 32)),
        (New-Object System.Drawing.Point 618, ($midY - 34)),
        (New-Object System.Drawing.Point 604, ($midY + 5)),
        (New-Object System.Drawing.Point 578, ($midY + 7))
    )
    $hookBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 220, 228, 238))
    $g.FillPolygon($hookBrush, $hookPoints)
    $hookBrush.Dispose()

    $hookRim = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 60, 65, 75)), 2
    $g.DrawPolygon($hookRim, $hookPoints)
    $hookRim.Dispose()

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created Lockpick Tool: $path"
}

# 6. Tension Wrench
function Create-TensionWrench {
    param([string]$path)
    $w = 512
    $h = 160
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $points = @(
        (New-Object System.Drawing.Point 20, 30),
        (New-Object System.Drawing.Point 440, 30),
        (New-Object System.Drawing.Point 490, 30),
        (New-Object System.Drawing.Point 490, 130),
        (New-Object System.Drawing.Point 468, 130),
        (New-Object System.Drawing.Point 468, 52),
        (New-Object System.Drawing.Point 20, 52)
    )

    $rect = New-Object System.Drawing.Rectangle 20, 30, 470, 100
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 230, 238, 248)), ([System.Drawing.Color]::FromArgb(255, 105, 112, 125)), 90.0
    $g.FillPolygon($brush, $points)
    $brush.Dispose()

    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 55, 60, 70)), 3
    $g.DrawPolygon($pen, $points)
    $pen.Dispose()

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created Tension Wrench: $path"
}

# 7. Safe Dial Wheel (512 x 512)
function Create-SafeDialWheel {
    param([string]$path)
    $size = 512
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $center = $size / 2

    # Outer chrome bezel ring
    $rimBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 10, 10, ($size - 20), ($size - 20)), ([System.Drawing.Color]::FromArgb(255, 240, 245, 252)), ([System.Drawing.Color]::FromArgb(255, 75, 80, 90)), 45.0
    $g.FillEllipse($rimBrush, 16, 16, ($size - 32), ($size - 32))
    $rimBrush.Dispose()

    # Darker dial face circle
    $faceBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 40, 40, ($size - 80), ($size - 80)), ([System.Drawing.Color]::FromArgb(255, 30, 34, 40)), ([System.Drawing.Color]::FromArgb(255, 16, 18, 22)), 90.0
    $g.FillEllipse($faceBrush, 48, 48, ($size - 96), ($size - 96))
    $faceBrush.Dispose()

    $faceBorder = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 160, 170, 185)), 4
    $g.DrawEllipse($faceBorder, 48, 48, ($size - 96), ($size - 96))
    $faceBorder.Dispose()

    # Radial tick marks and numbers 0 to 9
    $tickPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 220, 230, 245)), 3
    $font = [System.Drawing.Font]::new("Arial", [float]20, [System.Drawing.FontStyle]::Bold)
    $textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 245, 250, 255))
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center

    for ($n = 0; $n -lt 10; $n++) {
        $angle = ($n * 36.0 - 90.0) * [Math]::PI / 180.0
        $cos = [Math]::Cos($angle)
        $sin = [Math]::Sin($angle)

        $x1 = $center + ($cos * 192)
        $y1 = $center + ($sin * 192)
        $x2 = $center + ($cos * 170)
        $y2 = $center + ($sin * 170)
        $g.DrawLine($tickPen, [float]$x1, [float]$y1, [float]$x2, [float]$y2)

        $tx = $center + ($cos * 142)
        $ty = $center + ($sin * 142)
        $g.DrawString("$n", $font, $textBrush, [float]$tx, [float]$ty, $sf)
    }
    $tickPen.Dispose()
    $font.Dispose()
    $textBrush.Dispose()

    # Inner chrome grab hub
    $hubBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 160, 160, 192, 192), ([System.Drawing.Color]::FromArgb(255, 220, 230, 245)), ([System.Drawing.Color]::FromArgb(255, 60, 65, 75)), 135.0
    $g.FillEllipse($hubBrush, 172, 172, 168, 168)
    $hubBrush.Dispose()

    $hubDark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 25, 28, 35))
    $g.FillEllipse($hubDark, 208, 208, 96, 96)
    $hubDark.Dispose()

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created Safe Dial Wheel: $path"
}

# 8. Safe Door Heavy Plate (800 x 600)
function Create-SafeDoorPlate {
    param([string]$path)
    $w = 800
    $h = 600
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 45, 50, 58)), ([System.Drawing.Color]::FromArgb(255, 22, 24, 30)), 90.0
    $g.FillRectangle($brush, $rect)
    $brush.Dispose()

    $rivetBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 140, 150, 165))
    for ($x = 24; $x -lt $w - 20; $x += 48) {
        $g.FillEllipse($rivetBrush, $x, 18, 14, 14)
        $g.FillEllipse($rivetBrush, $x, ($h - 32), 14, 14)
    }
    for ($y = 24; $y -lt $h - 20; $y += 48) {
        $g.FillEllipse($rivetBrush, 18, $y, 14, 14)
        $g.FillEllipse($rivetBrush, ($w - 32), $y, 14, 14)
    }
    $rivetBrush.Dispose()

    $innerRect = New-Object System.Drawing.Rectangle 50, 50, ($w - 100), ($h - 100)
    $innerPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 75, 82, 95)), 4
    $g.DrawRectangle($innerPen, $innerRect)
    $innerPen.Dispose()

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created Safe Door Plate: $path"
}

# Run All Generators
Create-LockHousing "$dir/Lock_Housing_Cutaway.png"
Create-DriverPin "$dir/Lock_Pin_Driver.png"
Create-KeyPin "$dir/Lock_Pin_Key.png"
Create-PinSpring "$dir/Lock_Pin_Spring.png"
Create-LockpickTool "$dir/Lock_Pick_Tool.png"
Create-TensionWrench "$dir/Lock_Tension_Wrench.png"
Create-SafeDialWheel "$dir/Safe_Dial_Wheel.png"
Create-SafeDoorPlate "$dir/Safe_Door_Plate.png"
Write-Host "All puzzle textures generated successfully!"
