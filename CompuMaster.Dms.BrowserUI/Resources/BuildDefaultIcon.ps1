# Rebuilds the multi-resolution application icon and the WinForms icon resources.
Add-Type -AssemblyName System.Drawing

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sourcePath = Join-Path $PSScriptRoot 'default-dms-icon-source.png'
$source = [System.Drawing.Bitmap]::new($sourcePath)
try {
    $minimumX = $source.Width
    $minimumY = $source.Height
    $maximumX = 0
    $maximumY = 0
    for ($y = 0; $y -lt $source.Height; $y += 2) {
        for ($x = 0; $x -lt $source.Width; $x += 2) {
            if ($source.GetPixel($x, $y).A -gt 16) {
                $minimumX = [Math]::Min($minimumX, $x)
                $minimumY = [Math]::Min($minimumY, $y)
                $maximumX = [Math]::Max($maximumX, $x)
                $maximumY = [Math]::Max($maximumY, $y)
            }
        }
    }
    if ($maximumX -lt $minimumX) { throw 'The source icon contains no visible pixels.' }

    $contentWidth = $maximumX - $minimumX + 1
    $contentHeight = $maximumY - $minimumY + 1
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $images = foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $margin = [Math]::Max(1, [Math]::Round($size * 0.06))
                $scale = [Math]::Min(($size - 2 * $margin) / $contentWidth, ($size - 2 * $margin) / $contentHeight)
                $targetWidth = $contentWidth * $scale
                $targetHeight = $contentHeight * $scale
                $target = [System.Drawing.RectangleF]::new(($size - $targetWidth) / 2, ($size - $targetHeight) / 2, $targetWidth, $targetHeight)
                $sourceRectangle = [System.Drawing.RectangleF]::new($minimumX, $minimumY, $contentWidth, $contentHeight)
                $graphics.DrawImage($source, $target, $sourceRectangle, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally { $graphics.Dispose() }
            $stream = [System.IO.MemoryStream]::new()
            try {
                $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                ,$stream.ToArray()
            }
            finally { $stream.Dispose() }
        }
        finally { $bitmap.Dispose() }
    }

    $iconStream = [System.IO.MemoryStream]::new()
    try {
        $writer = [System.IO.BinaryWriter]::new($iconStream)
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $writer.Write([Byte]($sizes[$index] % 256))
            $writer.Write([Byte]($sizes[$index] % 256))
            $writer.Write([Byte]0)
            $writer.Write([Byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$images[$index].Length)
            $writer.Write([UInt32]$offset)
            $offset += $images[$index].Length
        }
        foreach ($image in $images) { $writer.Write([Byte[]]$image) }
        $iconBytes = $iconStream.ToArray()
    }
    finally { $iconStream.Dispose() }
}
finally { $source.Dispose() }

$iconPaths = @(
    'CompuMaster.Dms.BrowserUI\favicon.ico',
    'CompuMaster.Dms.TestDemo.ScopevisioTeamwork\favicon.ico',
    'CompuMaster.Dms.TestDemo.WebDav\favicon.ico'
)
foreach ($path in $iconPaths) {
    [System.IO.File]::WriteAllBytes((Join-Path $repositoryRoot $path), $iconBytes)
}

$resourcePaths = @(
    'CompuMaster.Dms.BrowserUI\DmsBrowser.resx',
    'CompuMaster.Dms.BrowserUI\DmsItemSharings.resx',
    'CompuMaster.Dms.BrowserUI\DmsLinkShareSetup.resx',
    'CompuMaster.Dms.BrowserUI\DmsStandardShareSetup.resx'
)
foreach ($path in $resourcePaths) {
    $fullPath = Join-Path $repositoryRoot $path
    $contents = [System.IO.File]::ReadAllText($fullPath)
    $pattern = '(?s)<data name="\$this\.Icon"[^>]*>.*?</data>'
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch($contents, $pattern)) { throw "Icon resource was not found in $path." }
    $replacement = '<data name="$this.Icon" type="System.Resources.ResXFileRef, System.Windows.Forms">' + "`r`n" +
                   '    <value>favicon.ico;System.Drawing.Icon, System.Drawing</value>' + "`r`n" +
                   '  </data>'
    $updated = [System.Text.RegularExpressions.Regex]::Replace($contents, $pattern, $replacement)
    $updated = $updated -replace "`r?`n", "`r`n"
    [System.IO.File]::WriteAllText($fullPath, $updated, [System.Text.UTF8Encoding]::new($true))
}
