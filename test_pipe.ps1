# Test pipe sender - simulates the C# host sending a state_update message
# Run the overlay first with: .\build\overlay\bin\Release\OBSReplayOverlay.exe --pipe TestPipe

$pipeName = "TestPipe"

# Create named pipe server
$pipe = New-Object System.IO.Pipes.NamedPipeServerStream($pipeName, [System.IO.Pipes.PipeDirection]::InOut, 1, [System.IO.Pipes.PipeTransmissionMode]::Byte, [System.IO.Pipes.PipeOptions]::Asynchronous)

Write-Host "Waiting for overlay to connect..."
$pipe.WaitForConnection()
Write-Host "Connected!"

# Read the 'ready' message from overlay
$lenBuf = New-Object byte[] 4
$pipe.Read($lenBuf, 0, 4) | Out-Null
$len = [BitConverter]::ToInt32($lenBuf, 0)
$bodyBuf = New-Object byte[] $len
$pipe.Read($bodyBuf, 0, $len) | Out-Null
$readyMsg = [System.Text.Encoding]::UTF8.GetString($bodyBuf)
Write-Host "Received: $readyMsg"

# Send a config_update
$configPayload = '{"toggleHotkey":"F10","saveHotkey":"F9","recIndicatorPosition":"top-right","showRecIndicator":true,"showNotifications":true,"notificationDuration":3.0,"notificationMessage":"REPLAY SAVED"}'
$configMsg = '{"type":"config_update","payload":"' + ($configPayload -replace '"', '\"') + '"}'
Write-Host "Sending config_update..."
$configBytes = [System.Text.Encoding]::UTF8.GetBytes($configMsg)
$configLen = [BitConverter]::GetBytes($configBytes.Length)
$pipe.Write($configLen, 0, 4)
$pipe.Write($configBytes, 0, $configBytes.Length)
$pipe.Flush()

Start-Sleep -Seconds 1

# Send a state_update with all the new fields
$statePayload = @{
    connected = $true
    currentScene = "Scene 1"
    isStreaming = $false
    isRecording = $false
    isRecordingPaused = $false
    isBufferActive = $false
    hasActiveCapture = $null
    scenes = @("Scene 1", "Scene 2")
    sources = @(
        @{id=1; name="Game Capture"; isVisible=$true; isLocked=$false; sourceKind="game_capture"}
        @{id=2; name="Webcam"; isVisible=$true; isLocked=$false; sourceKind="dshow_input"}
    )
    audio = @(
        @{name="Desktop Audio"; volumeMul=1.0; isMuted=$false}
        @{name="Mic/Aux"; volumeMul=0.8; isMuted=$false}
    )
    currentTransition = "Fade"
    transitionDuration = 300
    transitions = @("Fade", "Cut", "Swipe")
    studioModeEnabled = $false
    previewScene = $null
    currentProfile = "Default"
    currentSceneCollection = "Scenes"
    profiles = @("Default")
    sceneCollections = @("Scenes")
} | ConvertTo-Json -Compress

# Double-encode: payload is a JSON string inside the message
$statePayloadEscaped = $statePayload -replace '\\', '\\\\' -replace '"', '\"'
$stateMsg = '{"type":"state_update","payload":"' + $statePayloadEscaped + '"}'

Write-Host "Sending state_update (length=$($stateMsg.Length))..."
$stateBytes = [System.Text.Encoding]::UTF8.GetBytes($stateMsg)
$stateLen = [BitConverter]::GetBytes($stateBytes.Length)
$pipe.Write($stateLen, 0, 4)
$pipe.Write($stateBytes, 0, $stateBytes.Length)
$pipe.Flush()
Write-Host "Sent!"

Start-Sleep -Seconds 1

# Send show_overlay
$showMsg = '{"type":"show_overlay","payload":"{}"}'
Write-Host "Sending show_overlay..."
$showBytes = [System.Text.Encoding]::UTF8.GetBytes($showMsg)
$showLen = [BitConverter]::GetBytes($showBytes.Length)
$pipe.Write($showLen, 0, 4)
$pipe.Write($showBytes, 0, $showBytes.Length)
$pipe.Flush()

Write-Host "Done! Waiting 10 seconds..."
Start-Sleep -Seconds 10

$pipe.Dispose()
Write-Host "Pipe closed."
