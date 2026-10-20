# Rebuilds Assets\Resources\LobbyMusic.wav from the owner's loop (Assets\Basic Uplifting Loop\WAV): the loop is 16 bars of 4/4 at 127 BPM (30.236 s).
# Cuts it into four 4-bar chunks, joins them in a new order with 120 ms equal-power crossfades on the bar lines (the source wraps round, so every chunk
# fades through its natural lead-in and tail), and adds an intro (filter opening), a breakdown (filter closing, then opening with a riser), an impact on
# every chorus and an outro (fade + filter closing). Plain Windows PowerShell 5.1 + the C# in AudioTools.cs (compiled here to a DLL next to this script).
# Usage:  powershell -File BuildLobbyMusic.ps1 [-Out <wav>]
param([string]$Out = (Join-Path $PSScriptRoot '..\..\Assets\Resources\LobbyMusic.wav'))
$here = $PSScriptRoot
$dll = Join-Path $env:TEMP 'BE_AudioTools.dll'
if (-not (Test-Path $dll)) { Add-Type -TypeDefinition (Get-Content (Join-Path $here 'AudioTools.cs') -Raw) -Language CSharp -OutputAssembly $dll -OutputType Library }
Add-Type -Path $dll
$rate = 0
$src = [Wav]::Read((Join-Path $here '..\..\Assets\Basic Uplifting Loop\WAV\Basic Uplifting Loop.wav'), [ref]$rate)
$barLen = $src[0].Length / 16.0
# the chunks (0-based bar of the loop): a1 = bars 1-4 (groove), a2 = bars 5-8 (groove), c = bars 9-12 (the chorus harmony), t = bars 13-16 (the tag)
$map = @{ a1 = 0; a2 = 4; c = 8; t = 12 }
# the order: no run of four chunks repeats the loop's own order after its first appearance; the chorus (c) comes three times, always after a2
$labels = 'a1','a1','a2','t','a2','c','t','a1','a2','t','a2','c','a2','a1','t','a2','a2','c','a2','a1'
$starts = [int[]]($labels | ForEach-Object { $map[$_] })
$o = [Arr]::Assemble($src, $barLen, $starts, 4, 5292, 17640)
[Arr]::Lowpass($o, $barLen, 0, 4, 400, 16000, 0.8, $rate)        # intro: the filter opens
[Arr]::Lowpass($o, $barLen, 36, 2, 16000, 500, 0.8, $rate)       # breakdown: closes, holds, opens into the second chorus
[Arr]::Lowpass($o, $barLen, 38, 2, 500, 500, 0.8, $rate)
[Arr]::Lowpass($o, $barLen, 40, 2, 500, 500, 0.8, $rate)
[Arr]::Lowpass($o, $barLen, 42, 2, 500, 16000, 0.8, $rate)
[Arr]::Lowpass($o, $barLen, 72, 8, 16000, 1400, 0.8, $rate)      # outro: closes slowly
[Arr]::Gain($o, $barLen, 0, 4, 0.45, 1.0, $false)
[Arr]::Gain($o, $barLen, 36, 4, 1.0, 0.78, $false)
[Arr]::Gain($o, $barLen, 40, 4, 0.78, 1.0, $false)
[Arr]::Gain($o, $barLen, 72, 8, 1.0, 0.0, $true)
[Arr]::Riser($o, $barLen, 18, 2, 0.10, 11, $rate); [Arr]::Impact($o, $barLen, 20, 0.35, 21, $rate)
[Arr]::Riser($o, $barLen, 42, 2, 0.22, 12, $rate); [Arr]::Impact($o, $barLen, 44, 0.55, 22, $rate)
[Arr]::Riser($o, $barLen, 66, 2, 0.16, 13, $rate); [Arr]::Impact($o, $barLen, 68, 0.50, 23, $rate)
[Arr]::Normalize($o, 0.93)
[Wav]::Write16($Out, $o, $rate)
'{0:N1} s written to {1}' -f ($o[0].Length / $rate), $Out
