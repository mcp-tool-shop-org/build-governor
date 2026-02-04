# Test status endpoint
$pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "BuildGovernor", [System.IO.Pipes.PipeDirection]::InOut)
try {
    $pipe.Connect(5000)
    $writer = New-Object System.IO.StreamWriter($pipe)
    $reader = New-Object System.IO.StreamReader($pipe)
    $writer.AutoFlush = $true
    $writer.WriteLine('{"type":"status"}')
    $response = $reader.ReadLine()
    Write-Host "Raw Response:"
    Write-Host $response
    Write-Host ""
    Write-Host "Parsed:"
    $json = $response | ConvertFrom-Json
    $json.data | Format-List
} catch {
    Write-Host "Error: $_"
} finally {
    $pipe.Close()
}
