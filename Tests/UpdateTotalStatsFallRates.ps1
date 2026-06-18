param(
    [string]$Path = ".\total_metrics.tsv"
)

$source = [System.IO.Path]::GetFullPath($Path)
$temp = $source + ".tmp"

$reader = [System.IO.StreamReader]::new($source, [System.Text.Encoding]::UTF8)
$writer = $null

try {
    $writer = [System.IO.StreamWriter]::new(
        $temp,
        $false,
        [System.Text.UTF8Encoding]::new($false)
    )

    $header = $reader.ReadLine()
    $writer.WriteLine($header)

    $previousJumps = 0L
    $totalFalls = 0L
    $carry = 0.0

    while (($line = $reader.ReadLine()) -ne $null) {
        if ($line.Length -eq 0) {
            continue
        }

        $columns = $line.Split("`t")
        if ($columns.Length -lt 4) {
            continue
        }

        $sampledAt = [datetimeoffset]::Parse($columns[0])
        $totalFrames = [int64]$columns[1]
        $totalJumps = [int64]$columns[2]
        $jumpDelta = [Math]::Max(0L, $totalJumps - $previousJumps)

        # Deterministic monthly variation from 1.0% to 7.0%.
        $hash = (($sampledAt.Year * 17 + $sampledAt.Month * 29) % 61)
        $fallRate = (10 + $hash) / 1000.0
        $fallExact = $jumpDelta * $fallRate + $carry
        $fallDelta = [int64][Math]::Floor($fallExact)
        $carry = $fallExact - $fallDelta
        $totalFalls += $fallDelta

        $writer.Write($columns[0])
        $writer.Write("`t")
        $writer.Write($totalFrames)
        $writer.Write("`t")
        $writer.Write($totalJumps)
        $writer.Write("`t")
        $writer.WriteLine($totalFalls)

        $previousJumps = $totalJumps
    }
}
finally {
    $reader.Dispose()

    if ($writer -ne $null) {
        $writer.Dispose()
    }
}

Move-Item -LiteralPath $temp -Destination $source -Force
