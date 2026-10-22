Add-Type -AssemblyName System.Drawing

$outputDir = "Assets\Textures\PuzzleTextures"
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

function Generate-NoSignalScreen {
    param([string]$path)
    $w = 512
    $h = 512
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

    # Dark CRT Screen background with slight vignette
    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $pathGrad = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pathGrad.AddEllipse($rect)
    $pbg = New-Object System.Drawing.Drawing2D.PathGradientBrush($pathGrad)
    $pbg.CenterColor = [System.Drawing.Color]::FromArgb(255, 12, 16, 14)
    $pbg.SurroundColors = @([System.Drawing.Color]::FromArgb(255, 4, 6, 5))
    $g.FillRectangle($pbg, $rect)
    $pbg.Dispose()
    $pathGrad.Dispose()

    # Scanlines
    $scanPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(40, 0, 0, 0), 1)
    for ($y = 0; $y -lt $h; $y += 3) {
        $g.DrawLine($scanPen, 0, $y, $w, $y)
    }
    $scanPen.Dispose()

    # Outer CRT curved frame
    $framePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 25, 30, 28), 8)
    $g.DrawRectangle($framePen, 4, 4, $w - 8, $h - 8)
    $framePen.Dispose()

    # Warning Box in Amber / Orange
    $boxPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220, 220, 140, 20), 2)
    $g.DrawRectangle($boxPen, 48, 140, $w - 96, 210)
    $boxPen.Dispose()

    # Amber Header fill
    $amberBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(180, 220, 140, 20))
    $g.FillRectangle($amberBrush, 48, 140, $w - 96, 36)
    $amberBrush.Dispose()

    # Header Text
    $fontHeader = New-Object System.Drawing.Font("Consolas", 14, [System.Drawing.FontStyle]::Bold)
    $darkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 15, 10, 5))
    $sfCenter = New-Object System.Drawing.StringFormat
    $sfCenter.Alignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("VIDEO DISPLAY SUBSYSTEM", $fontHeader, $darkBrush, [float]($w / 2), 148.0, $sfCenter)
    $fontHeader.Dispose()
    $darkBrush.Dispose()

    # Warning Message Text
    $fontBody = New-Object System.Drawing.Font("Consolas", 13, [System.Drawing.FontStyle]::Bold)
    $amberText = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 245, 165, 35))
    $g.DrawString("NO INPUT SIGNAL DETECTED", $fontBody, $amberText, [float]($w / 2), 195.0, $sfCenter)

    $fontSub = New-Object System.Drawing.Font("Consolas", 11, [System.Drawing.FontStyle]::Regular)
    $g.DrawString("CHECK VGA INTERFACE CABLE", $fontSub, $amberText, [float]($w / 2), 235.0, $sfCenter)
    $g.DrawString("PORT: VGA-01 // HOST: CCS-SRV-105", $fontSub, $amberText, [float]($w / 2), 265.0, $sfCenter)
    $g.DrawString("STATUS: STANDBY / OFF-LINE", $fontSub, $amberText, [float]($w / 2), 300.0, $sfCenter)

    # Blinking prompt cursor
    $g.DrawString("> TERMINAL OFFLINE _", $fontSub, $amberText, 55.0, 380.0)

    $fontBody.Dispose()
    $fontSub.Dispose()
    $amberText.Dispose()
    $sfCenter.Dispose()
    $g.Dispose()

    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created NoSignal screen at $path"
}

function Generate-BootedScreen {
    param([string]$path)
    $w = 512
    $h = 512
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

    # CRT Green Phosphor Background with subtle radial glow
    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $pathGrad = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pathGrad.AddEllipse($rect)
    $pbg = New-Object System.Drawing.Drawing2D.PathGradientBrush($pathGrad)
    $pbg.CenterColor = [System.Drawing.Color]::FromArgb(255, 6, 26, 12)
    $pbg.SurroundColors = @([System.Drawing.Color]::FromArgb(255, 2, 8, 4))
    $g.FillRectangle($pbg, $rect)
    $pbg.Dispose()
    $pathGrad.Dispose()

    # Scanlines
    $scanPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(45, 0, 0, 0), 1)
    for ($y = 0; $y -lt $h; $y += 3) {
        $g.DrawLine($scanPen, 0, $y, $w, $y)
    }
    $scanPen.Dispose()

    # Frame
    $framePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200, 20, 60, 30), 8)
    $g.DrawRectangle($framePen, 4, 4, $w - 8, $h - 8)
    $framePen.Dispose()

    # Phosphor Green Text
    $fontTitle = New-Object System.Drawing.Font("Consolas", 12, [System.Drawing.FontStyle]::Bold)
    $fontBody = New-Object System.Drawing.Font("Consolas", 10, [System.Drawing.FontStyle]::Bold)
    $fontSmall = New-Object System.Drawing.Font("Consolas", 9, [System.Drawing.FontStyle]::Regular)
    $greenGlow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 60, 255, 110))
    $greenBright = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 160, 255, 190))
    $greenDim = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 45, 180, 80))

    $sfLeft = New-Object System.Drawing.StringFormat
    $sfLeft.Alignment = [System.Drawing.StringAlignment]::Near

    $yPos = 24.0
    $g.DrawString("PAMANTASAN NG CABUYAO // COMPUTER LABORATORY", $fontTitle, $greenBright, 24.0, $yPos, $sfLeft)
    $yPos += 20.0
    $g.DrawString("PNC-TERMINAL OS v3.2.0 [BUILD 1998-04-12]", $fontSmall, $greenDim, 24.0, $yPos, $sfLeft)
    $yPos += 16.0
    $g.DrawString("==================================================", $fontBody, $greenDim, 24.0, $yPos, $sfLeft)
    $yPos += 22.0

    $g.DrawString("[SYSTEM]  MEMORY 640 KB OK ... CHIPSET OK", $fontSmall, $greenGlow, 24.0, $yPos, $sfLeft)
    $yPos += 18.0
    $g.DrawString("[VGA-01]  CCS-SRV-TERMINAL-01 CONNECTED (31.5 kHz)", $fontSmall, $greenGlow, 24.0, $yPos, $sfLeft)
    $yPos += 18.0
    $g.DrawString("[NETWORK] TCP/IP NODE 192.168.1.105 HANDSHAKE OK", $fontSmall, $greenGlow, 24.0, $yPos, $sfLeft)
    $yPos += 22.0

    # Clearance Hold Query Box
    $boxPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 50, 220, 95), 1)
    $g.DrawRectangle($boxPen, 24, [int]$yPos, $w - 48, 120)
    $boxPen.Dispose()

    $yPos += 10.0
    $g.DrawString("DATABASE QUERY: STUDENT CLEARANCE RECORD", $fontBody, $greenBright, 34.0, $yPos, $sfLeft)
    $yPos += 20.0
    $g.DrawString("STUDENT ID : 2024-08912-CB", $fontSmall, $greenGlow, 34.0, $yPos, $sfLeft)
    $yPos += 18.0
    $g.DrawString("HOLD FLAG  : COMLAB-RESTRICTED-105 [FOUND]", $fontSmall, $greenBright, 34.0, $yPos, $sfLeft)
    $yPos += 18.0
    $g.DrawString("RESOLUTION : AUTO-OVERRIDE DISPENSE AUTHORIZED", $fontSmall, $greenGlow, 34.0, $yPos, $sfLeft)
    $yPos += 20.0
    $g.DrawString("STATUS     : [*** HOLD CLEARED & RESOLVED ***]", $fontBody, $greenBright, 34.0, $yPos, $sfLeft)
    $yPos += 36.0

    $g.DrawString("==================================================", $fontBody, $greenDim, 24.0, $yPos, $sfLeft)
    $yPos += 20.0
    $g.DrawString(">>> PRINTER: DISPENSING COMLAB ACCOUNTABILITY SLIP...", $fontBody, $greenBright, 24.0, $yPos, $sfLeft)
    $yPos += 20.0
    $g.DrawString("PLEASE PRESENT SLIP TO CCS DEAN (ROOM 202) _", $fontSmall, $greenGlow, 24.0, $yPos, $sfLeft)

    $fontTitle.Dispose()
    $fontBody.Dispose()
    $fontSmall.Dispose()
    $greenGlow.Dispose()
    $greenBright.Dispose()
    $greenDim.Dispose()
    $sfLeft.Dispose()
    $g.Dispose()

    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created Booted screen at $path"
}

Generate-NoSignalScreen "$outputDir\ComLab_Screen_NoSignal.png"
Generate-BootedScreen "$outputDir\ComLab_Screen_Booted.png"
