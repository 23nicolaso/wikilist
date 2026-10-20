param(
    [Parameter(Mandatory=$true)][string]$Action,
    [int]$Index=0,
    [float]$X=0,
    [float]$Y=0,
    [int]$TimeoutSeconds=25
)
$nearerInbox=Join-Path (Split-Path $PSScriptRoot -Parent) '.nearer-local/editor'
$nearerCommand=Join-Path $nearerInbox 'command.json'
$nearerResult=Join-Path $nearerInbox 'result.json'
if(-not(Test-Path -LiteralPath $nearerInbox)){throw 'Open the Nearer project in Unity first.'}
$nearerPrevious=(Get-Item -LiteralPath $nearerResult -ErrorAction SilentlyContinue).LastWriteTimeUtc
$nearerPending=Join-Path $nearerInbox 'command.pending'
@{action=$Action;index=$Index;x=$X;y=$Y}|ConvertTo-Json -Compress|Set-Content -LiteralPath $nearerPending -NoNewline
Move-Item -LiteralPath $nearerPending -Destination $nearerCommand -Force
$nearerDeadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    Start-Sleep -Milliseconds 150
    $nearerStamp=(Get-Item -LiteralPath $nearerResult -ErrorAction SilentlyContinue).LastWriteTimeUtc
    if((-not(Test-Path -LiteralPath $nearerCommand))-and $nearerStamp -ne $nearerPrevious){
        $nearerResponse=Get-Content -LiteralPath $nearerResult -Raw|ConvertFrom-Json
        if(-not $nearerResponse.ok){throw $nearerResponse.message}
        $nearerResponse.message
        exit 0
    }
}while([DateTime]::UtcNow -lt $nearerDeadline)
throw 'The Unity editor did not finish this action before the timeout.'
