$files = @(
    "Assets/Scripts/NPCJumpscareManager.cs",
    "Assets/Scripts/SecurityGuardStalkerAI.cs",
    "Assets/Scripts/WeepingStudentAI.cs",
    "Assets/Scripts/TheProctorAI.cs"
)

foreach ($f in $files) {
    if (Test-Path $f) {
        $content = [System.IO.File]::ReadAllText($f)
        $normalized = $content -replace "\r\n", "`n"
        [System.IO.File]::WriteAllText($f, $normalized)
        Write-Host "Normalized $f"
    }
}
