param(
	[Parameter(Mandatory = $true)][string]$Name,
	[int]$Id = 0,
	[int]$Seconds = 60
)

# Measures a process's average CPU usage over a window by sampling
# TotalProcessorTime, which is far more reliable than reading the
# instantaneous percentage shown in Task Manager.

$procs = @(Get-Process -Name $Name -ErrorAction SilentlyContinue)

if ($Id -ne 0) {
	$procs = @($procs | Where-Object { $_.Id -eq $Id })
}

if ($procs.Count -eq 0) {
	Write-Host "No process named '$Name' found (Id filter: $Id)."
	exit 1
}

if ($procs.Count -gt 1) {
	Write-Host "Multiple processes named '$Name' are running:"
	$procs | ForEach-Object { Write-Host ("  PID {0}" -f $_.Id) }
	Write-Host "Stop the extra ones, or pass the right one with -Id <pid>."
	exit 1
}

$process = $procs[0]
$process.Refresh()

$cpuStart = $process.TotalProcessorTime.TotalSeconds
$wallStart = [DateTime]::UtcNow

Write-Host "Sampling '$($process.ProcessName)' (PID $($process.Id)) for $Seconds seconds; keep the app running and hands off."

$remaining = $Seconds

while ($remaining -gt 0) {
	$step = [Math]::Min(10, $remaining)

	Start-Sleep -Seconds $step
	$remaining -= $step

	Write-Host ("  ... {0} s remaining" -f $remaining)
}

$process.Refresh()

$cpuDelta = $process.TotalProcessorTime.TotalSeconds - $cpuStart
$wallDelta = ([DateTime]::UtcNow - $wallStart).TotalSeconds
$percent = ($cpuDelta / $wallDelta) * 100.0

Write-Host ""
Write-Host ("Process:            {0} (PID {1})" -f $process.ProcessName, $process.Id)
Write-Host ("Sampled wall time:  {0:N1} s" -f $wallDelta)
Write-Host ("CPU time consumed:  {0:N2} s" -f $cpuDelta)
Write-Host ("Average CPU usage:  {0:N2} % of one core" -f $percent)
