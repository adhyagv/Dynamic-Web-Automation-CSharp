$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\DynamicWebAutomation\DynamicWebAutomation.csproj'
$integrationReports = Join-Path $root 'reports\integration'
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')
New-Item -ItemType Directory -Force -Path $integrationReports | Out-Null

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCommand) {
    $dotnet = $dotnetCommand.Source
} else {
    $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (-not (Test-Path $dotnet)) {
        throw 'The .NET SDK was not found on PATH or under Program Files.'
    }
}

# Fixture expectations live in this runner. Production code receives only the file URL.
$cases = @(
    @{ File = 'v1-original.html'; ExpectedExit = 0; FirstName = 'PASS'; Gender = 'PASS'; State = 'PASS' },
    @{ File = 'v2-changed-identifiers.html'; ExpectedExit = 0; FirstName = 'PASS'; Gender = 'PASS'; State = 'PASS' },
    @{ File = 'v3-changed-order.html'; ExpectedExit = 0; FirstName = 'PASS'; Gender = 'PASS'; State = 'PASS' },
    @{ File = 'v4-similar-and-hidden-fields.html'; ExpectedExit = 0; FirstName = 'PASS'; Gender = 'PASS'; State = 'PASS' },
    @{ File = 'v5-wrapping-labels.html'; ExpectedExit = 0; FirstName = 'PASS'; Gender = 'PASS'; State = 'PASS' },
    @{ File = 'n1-missing-first-name.html'; ExpectedExit = 1; FirstName = 'FAIL'; Gender = 'PASS'; State = 'PASS' },
    @{ File = 'n2-ambiguous-first-name.html'; ExpectedExit = 1; FirstName = 'AMBIGUOUS'; Gender = 'NOT_RUN'; State = 'NOT_RUN' }
)

$rows = [System.Collections.Generic.List[object]]::new()
foreach ($case in $cases) {
    $pagePath = Join-Path $PSScriptRoot $case.File
    $resultPath = Join-Path $integrationReports (
        [IO.Path]::GetFileNameWithoutExtension($case.File) + '-' + $runId + '.json')
    $fileUrl = ([Uri]::new((Resolve-Path $pagePath).Path)).AbsoluteUri

    Write-Host "`n=== $($case.File) ==="
    $output = & $dotnet run --no-build --project $project -- --url $fileUrl --headless --results-json $resultPath 2>&1
    $processExit = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }

    if (-not (Test-Path $resultPath)) {
        $rows.Add([pscustomobject]@{
            Scenario = $case.File
            Expected = "exit $($case.ExpectedExit); FirstName=$($case.FirstName), Gender=$($case.Gender), State=$($case.State)"
            Actual = 'No JSON result file'
            ProcessExit = $processExit
            Comparison = 'UNEXPECTED EXECUTION FAILURE'
        })
        continue
    }

    try {
        $report = Get-Content -Raw $resultPath | ConvertFrom-Json
    } catch {
        $report = $null
    }

    if ($null -eq $report -or $report.executionError) {
        $actual = if ($null -eq $report) { 'Invalid JSON result' } else { $report.executionError }
        $comparison = 'UNEXPECTED EXECUTION FAILURE'
    } elseif ($processExit -ne $case.ExpectedExit -or $report.exitCode -ne $case.ExpectedExit) {
        $actual = "exit $processExit; JSON exit $($report.exitCode)"
        $comparison = 'UNEXPECTED EXECUTION FAILURE'
    } else {
        $fieldMap = @{}
        foreach ($field in $report.fields) {
            $fieldMap[$field.field] = $field.status
        }

        $actualStatuses = "FirstName=$($fieldMap['First Name']), Gender=$($fieldMap['Gender']), State=$($fieldMap['State'])"
        $expectedStatuses = "FirstName=$($case.FirstName), Gender=$($case.Gender), State=$($case.State)"
        $actual = "exit $processExit; $actualStatuses"
        if ($actualStatuses -eq $expectedStatuses) {
            $comparison = if ($case.ExpectedExit -eq 1) {
                'PASS (expected application failure)'
            } else {
                'PASS'
            }
        } else {
            $comparison = "MISMATCH (expected $expectedStatuses)"
        }
    }

    $rows.Add([pscustomobject]@{
        Scenario = $case.File
        Expected = "exit $($case.ExpectedExit); FirstName=$($case.FirstName), Gender=$($case.Gender), State=$($case.State)"
        Actual = $actual
        ProcessExit = $processExit
        Comparison = $comparison
    })
}

Write-Host "`n=== Local integration comparison ==="
$rows | Format-Table -AutoSize | Out-String -Width 240 | Write-Host
$unexpected = @($rows | Where-Object { $_.Comparison -notlike 'PASS*' })
if ($unexpected.Count -gt 0) {
    exit 1
}

exit 0
