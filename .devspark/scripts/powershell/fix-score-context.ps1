#!/usr/bin/env pwsh
#requires -Version 7.0
<#
.SYNOPSIS
    Gathers compact score-remediation context as JSON for /devspark.fix-score.

.DESCRIPTION
    Produces a token-efficient repository snapshot focused on GitHubSpark score
    levers: README quality, dependency readiness, repository health, activity,
    attention/security signals, and existing score/audit artifacts.
#>

param(
    [Parameter(Position = 0, ValueFromRemainingArguments)]
    [string[]]$Arguments,

    [switch]$Json,

    [string]$Output
)

. (Join-Path $PSScriptRoot 'common.ps1')

function Invoke-GitLines {
    param([string[]]$GitArgs)
    try {
        $output = git @GitArgs 2>$null
        if ($LASTEXITCODE -ne 0 -or -not $output) { return @() }
        return @($output | Where-Object { $_ -and $_.Trim() })
    }
    catch {
        return @()
    }
}

function Invoke-GitText {
    param([string[]]$GitArgs)
    $lines = Invoke-GitLines -GitArgs $GitArgs
    if ($lines.Count -eq 0) { return "" }
    return ($lines -join "`n").Trim()
}

function Get-RelativePath {
    param([string]$Path, [string]$RepoRoot)
    return [System.IO.Path]::GetRelativePath($RepoRoot, $Path).Replace('\', '/')
}

function Get-ExistingPaths {
    param([string]$RepoRoot, [string[]]$Names)
    $found = @()
    foreach ($name in $Names) {
        if (Test-Path -LiteralPath (Join-Path $RepoRoot $name)) {
            $found += $name
        }
    }
    return @($found)
}

function Get-Scope {
    param([string[]]$RawArguments)
    $scope = [ordered]@{
        repo = $null
        user = $null
        category = $null
        audit = $null
        raw = @($RawArguments)
    }
    foreach ($arg in $RawArguments) {
        foreach ($key in @('repo', 'user', 'category', 'audit')) {
            $prefix = "${key}:"
            if ($arg.StartsWith($prefix)) {
                $scope[$key] = $arg.Substring($prefix.Length)
            }
        }
    }
    return $scope
}

function Get-ReadmeMetrics {
    param([string]$RepoRoot)
    $readme = Get-ChildItem -Path $RepoRoot -Filter 'README*' -File -ErrorAction SilentlyContinue |
        Sort-Object Name |
        Select-Object -First 1

    if (-not $readme) {
        return [ordered]@{
            present = $false
            path = $null
            estimated_quality_score = 0
            score_inputs = [ordered]@{
                characters = 0
                headings = 0
                code_blocks = 0
                links = 0
                images_or_badges = 0
                has_install_or_usage_section = $false
            }
            opportunities = @('Add README with headings, usage/install section, code examples, links, and badges/images.')
        }
    }

    $text = Get-Content -LiteralPath $readme.FullName -Raw -Encoding utf8 -ErrorAction SilentlyContinue
    if ($null -eq $text) { $text = "" }

    $headings = ([regex]::Matches($text, '(?m)^#{1,6}\s+\S')).Count
    $codeBlocks = [math]::Floor(([regex]::Matches($text, '(?m)^```')).Count / 2)
    $links = ([regex]::Matches($text, '\[[^\]]+\]\([^)]+\)|https?://\S+')).Count
    $images = ([regex]::Matches($text, '!\[[^\]]*\]\([^)]+\)|img\.shields\.io|badge', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $hasInstall = [regex]::IsMatch($text, '\b(install|getting started|usage|quick start|setup)\b', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    $score = [math]::Min(20, [math]::Floor($text.Length / 200)) +
        [math]::Min(20, $headings * 4) +
        [math]::Min(15, $codeBlocks * 5) +
        [math]::Min(15, $links * 3) +
        [math]::Min(15, $images * 5)
    if ($hasInstall) { $score += 15 }

    $opportunities = @()
    if ($text.Length -lt 4000) { $opportunities += 'Expand README substance; length contributes up to 20 points.' }
    if ($headings -lt 5) { $opportunities += 'Add clear README sections; headings contribute up to 20 points.' }
    if ($codeBlocks -lt 3) { $opportunities += 'Add runnable examples; code blocks contribute up to 15 points.' }
    if ($links -lt 5) { $opportunities += 'Add relevant docs/project links; links contribute up to 15 points.' }
    if ($images -lt 3) { $opportunities += 'Add badges or useful images; images/badges contribute up to 15 points.' }
    if (-not $hasInstall) { $opportunities += 'Add install, setup, quick start, or usage section for 15 points.' }

    return [ordered]@{
        present = $true
        path = Get-RelativePath -Path $readme.FullName -RepoRoot $RepoRoot
        estimated_quality_score = [math]::Min(100, $score)
        score_inputs = [ordered]@{
            characters = $text.Length
            headings = $headings
            code_blocks = $codeBlocks
            links = $links
            images_or_badges = $images
            has_install_or_usage_section = $hasInstall
        }
        opportunities = @($opportunities | Select-Object -First 6)
    }
}

function Get-DependencySignals {
    param([string]$RepoRoot)
    $manifests = Get-ExistingPaths -RepoRoot $RepoRoot -Names @(
        'package.json', 'pyproject.toml', 'requirements.txt', 'Pipfile', 'poetry.lock',
        'go.mod', 'Cargo.toml', 'pom.xml', 'build.gradle', 'build.gradle.kts', 'Directory.Packages.props'
    )
    $lockfiles = Get-ExistingPaths -RepoRoot $RepoRoot -Names @(
        'package-lock.json', 'npm-shrinkwrap.json', 'yarn.lock', 'pnpm-lock.yaml', 'poetry.lock',
        'Pipfile.lock', 'requirements.lock', 'go.sum', 'Cargo.lock', 'packages.lock.json'
    )

    $directCount = $null
    $packageJson = Join-Path $RepoRoot 'package.json'
    if (Test-Path -LiteralPath $packageJson) {
        try {
            $pkg = Get-Content -LiteralPath $packageJson -Raw -Encoding utf8 | ConvertFrom-Json
            $count = 0
            foreach ($key in @('dependencies', 'devDependencies', 'peerDependencies', 'optionalDependencies')) {
                if ($pkg.PSObject.Properties.Name -contains $key -and $pkg.$key) {
                    $count += @($pkg.$key.PSObject.Properties).Count
                }
            }
            $directCount = $count
        }
        catch {
            $directCount = $null
        }
    }

    $opportunities = @()
    if ($manifests.Count -gt 0 -and $lockfiles.Count -eq 0) {
        $opportunities += 'Add or restore lockfiles so dependency state is reproducible before currency checks.'
    }
    if ($manifests.Count -gt 0) {
        $opportunities += 'Run project-native outdated/audit commands and update fixable direct dependencies.'
    }

    return [ordered]@{
        manifests = @($manifests)
        lockfiles = @($lockfiles)
        direct_dependency_count_package_json = $directCount
        currency_score_note = 'Dependency currency requires registry/latest-version data; this context only identifies local manifests and lockfile readiness.'
        opportunities = @($opportunities)
    }
}

function Get-RepositoryHealth {
    param([string]$RepoRoot, [hashtable]$ReadmeMetrics)
    $workflowFiles = @()
    $workflowsDir = Join-Path $RepoRoot '.github/workflows'
    if (Test-Path -LiteralPath $workflowsDir) {
        $workflowFiles = @(Get-ChildItem -Path $workflowsDir -File -ErrorAction SilentlyContinue |
            Sort-Object Name |
            Select-Object -First 10 |
            ForEach-Object { Get-RelativePath -Path $_.FullName -RepoRoot $RepoRoot })
    }
    $licenseFiles = Get-ExistingPaths -RepoRoot $RepoRoot -Names @('LICENSE', 'LICENSE.md', 'COPYING', 'NOTICE')
    $opportunities = @()
    if ($licenseFiles.Count -eq 0) { $opportunities += 'Add a license file; front-end maintenance score penalizes missing license.' }
    if ($workflowFiles.Count -eq 0) { $opportunities += 'Add CI workflow; front-end maintenance score penalizes missing CI/CD.' }

    return [ordered]@{
        readme_present = [bool]$ReadmeMetrics.present
        license_present = ($licenseFiles.Count -gt 0)
        license_files = @($licenseFiles)
        ci_present = ($workflowFiles.Count -gt 0)
        workflow_files_sample = @($workflowFiles)
        opportunities = @($opportunities)
    }
}

function Get-ActivitySignals {
    param([string]$CurrentBranch)
    $lastCommitIso = Invoke-GitText -GitArgs @('log', '-1', '--format=%cI')
    $daysSinceLastCommit = $null
    if ($lastCommitIso) {
        try {
            $last = [DateTimeOffset]::Parse($lastCommitIso)
            $daysSinceLastCommit = [int]([DateTimeOffset]::UtcNow - $last).TotalDays
        }
        catch {
            $daysSinceLastCommit = $null
        }
    }

    $activeWeeks = Invoke-GitLines -GitArgs @('log', '--since=52 weeks ago', '--date=format:%G-%V', '--format=%cd') |
        Sort-Object -Unique

    return [ordered]@{
        current_branch = $CurrentBranch
        last_commit_iso = $(if ($lastCommitIso) { $lastCommitIso } else { $null })
        days_since_last_commit = $daysSinceLastCommit
        commit_counts = [ordered]@{
            last_90_days = (Invoke-GitLines -GitArgs @('log', '--since=90 days ago', '--format=%H')).Count
            last_180_days = (Invoke-GitLines -GitArgs @('log', '--since=180 days ago', '--format=%H')).Count
            last_365_days = (Invoke-GitLines -GitArgs @('log', '--since=365 days ago', '--format=%H')).Count
            total = [int]((Invoke-GitText -GitArgs @('rev-list', '--count', 'HEAD')) -replace '\D', '')
        }
        active_weeks_last_52 = @($activeWeeks).Count
        anti_gaming_note = 'Do not create empty or meaningless commits to manipulate activity, consistency, or recency scores.'
    }
}

function Invoke-GhJson {
    param([string[]]$GhArgs)
    try {
        $raw = gh @GhArgs 2>$null
        if ($LASTEXITCODE -ne 0 -or -not $raw) { return $null }
        return ($raw | ConvertFrom-Json)
    }
    catch {
        return $null
    }
}

function Get-AttentionSignals {
    $ghAvailable = $false
    try {
        gh --version 2>$null | Out-Null
        $ghAvailable = ($LASTEXITCODE -eq 0)
    }
    catch {
        $ghAvailable = $false
    }

    $openPrs = $null
    $openIssues = $null
    $securityAlerts = $null
    if ($ghAvailable) {
        $prs = Invoke-GhJson -GhArgs @('pr', 'list', '--state', 'open', '--limit', '100', '--json', 'number,isDraft,reviewRequests,createdAt')
        $issues = Invoke-GhJson -GhArgs @('issue', 'list', '--state', 'open', '--limit', '100', '--json', 'number,createdAt')
        if ($prs) {
            $prArray = @($prs)
            $openPrs = [ordered]@{
                count = $prArray.Count
                draft_count = @($prArray | Where-Object { $_.isDraft }).Count
                review_requested_count = @($prArray | Where-Object { $_.reviewRequests -and @($_.reviewRequests).Count -gt 0 }).Count
            }
        }
        if ($issues) {
            $openIssues = [ordered]@{ count = @($issues).Count }
        }
        $alerts = Invoke-GhJson -GhArgs @('api', 'repos/{owner}/{repo}/dependabot/alerts', '--paginate')
        if ($alerts) {
            $bySeverity = @{}
            foreach ($alert in @($alerts)) {
                $severity = 'unknown'
                if ($alert.security_vulnerability -and $alert.security_vulnerability.severity) {
                    $severity = $alert.security_vulnerability.severity.ToLower()
                }
                $bySeverity[$severity] = 1 + $(if ($bySeverity.ContainsKey($severity)) { $bySeverity[$severity] } else { 0 })
            }
            $securityAlerts = $bySeverity
        }
    }

    return [ordered]@{
        github_cli_available = $ghAvailable
        open_prs = $openPrs
        open_issues = $openIssues
        dependabot_alerts_by_severity = $securityAlerts
        security_data_note = 'Null alert data means unavailable, not healthy. GitHubSpark adds availability penalties for partial/unavailable security data.'
    }
}

function Get-AuditArtifacts {
    param([string]$RepoRoot)
    $roots = @('.documentation', '.devspark') | ForEach-Object { Join-Path $RepoRoot $_ }
    $patterns = @('*audit*.md', '*audit*.json', '*score*.json', '*score*.md', '*diagnostic*.json', '*diagnostic*.md')
    $found = @()
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        foreach ($pattern in $patterns) {
            $found += Get-ChildItem -Path $root -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue |
                ForEach-Object {
                    [ordered]@{
                        path = Get-RelativePath -Path $_.FullName -RepoRoot $RepoRoot
                        bytes = $_.Length
                    }
                }
        }
    }
    return @($found | Sort-Object path -Unique | Select-Object -First 20)
}

function Get-RecommendedReads {
    param(
        [hashtable]$Scope,
        [hashtable]$ReadmeMetrics,
        [hashtable]$DependencySignals,
        [hashtable]$RepositoryHealth,
        [object[]]$AuditArtifacts
    )
    $category = if ($Scope.category) { $Scope.category.ToLower() } else { '' }
    $reads = @()
    if ((-not $category -or $category -like '*readme*') -and $ReadmeMetrics.path) {
        $reads += [ordered]@{ path = $ReadmeMetrics.path; why = 'README quality score inputs and low-cost improvements.' }
    }
    if (-not $category -or $category -like '*depend*') {
        foreach ($path in @($DependencySignals.manifests | Select-Object -First 5)) {
            $reads += [ordered]@{ path = $path; why = 'Dependency currency and audit command discovery.' }
        }
    }
    if (-not $category -or $category -like '*attention*' -or $category -like '*maintenance*') {
        foreach ($path in @($RepositoryHealth.workflow_files_sample | Select-Object -First 3)) {
            $reads += [ordered]@{ path = $path; why = 'CI/CD presence and maintenance signal.' }
        }
    }
    foreach ($artifact in @($AuditArtifacts | Select-Object -First 3)) {
        $reads += [ordered]@{ path = $artifact.path; why = 'Existing score/audit signal; validate before trusting.' }
    }
    return @($reads | Select-Object -First 10)
}

if ($Arguments) {
    for ($i = 0; $i -lt $Arguments.Count; $i++) {
        if ($Arguments[$i] -eq '--output' -and $i + 1 -lt $Arguments.Count) {
            $Output = $Arguments[$i + 1]
            $Arguments = @($Arguments | Where-Object { $_ -ne '--output' -and $_ -ne $Output })
            break
        }
        if ($Arguments[$i] -match '^--output=(.+)$') {
            $Output = $matches[1]
            $Arguments = @($Arguments | Where-Object { $_ -ne $Arguments[$i] })
            break
        }
    }
}

$repoRoot = Get-RepoRoot
$currentBranch = Get-CurrentBranch
$scope = Get-Scope -RawArguments $Arguments
$readmeMetrics = Get-ReadmeMetrics -RepoRoot $repoRoot
$dependencySignals = Get-DependencySignals -RepoRoot $repoRoot
$repositoryHealth = Get-RepositoryHealth -RepoRoot $repoRoot -ReadmeMetrics $readmeMetrics
$activitySignals = Get-ActivitySignals -CurrentBranch $currentBranch
$attentionSignals = Get-AttentionSignals
$auditArtifacts = Get-AuditArtifacts -RepoRoot $repoRoot

$result = [ordered]@{
    schema_version = 1
    generated_at = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    repo_root = $repoRoot
    scope = $scope
    score_categories = [ordered]@{
        'profile-spark' = [ordered]@{ direction = 'increase'; perfect = 100; local_fixability = 'limited'; primary_levers = @('consistency', 'volume', 'collaboration') }
        'repository-composite' = [ordered]@{ direction = 'increase'; perfect = 100; local_fixability = 'partial'; primary_levers = @('activity', 'health', 'popularity') }
        'repository-attention' = [ordered]@{ direction = 'decrease'; perfect = 0; local_fixability = 'high'; primary_levers = @('open_pr_pressure', 'security_alerts', 'staleness', 'dependency_attention') }
        'dependency-currency' = [ordered]@{ direction = 'increase'; perfect = 100; local_fixability = 'high'; primary_levers = @('outdated_dependencies', 'version_coverage', 'latest_version_coverage') }
        'readme-quality' = [ordered]@{ direction = 'increase'; perfect = 100; local_fixability = 'high'; primary_levers = @('length', 'headings', 'code_blocks', 'links', 'images_or_badges', 'install_or_usage_section') }
        'frontend-maintenance' = [ordered]@{ direction = 'decrease'; perfect = 0; local_fixability = 'high'; primary_levers = @('staleness', 'missing_readme', 'missing_license', 'missing_ci', 'open_issues', 'open_prs', 'security_alerts') }
    }
    signals = [ordered]@{
        readme_quality = $readmeMetrics
        repository_health = $repositoryHealth
        activity = $activitySignals
        dependencies = $dependencySignals
        attention = $attentionSignals
        audit_artifacts_sample = @($auditArtifacts)
    }
    recommended_context_reads = Get-RecommendedReads -Scope $scope -ReadmeMetrics $readmeMetrics -DependencySignals $dependencySignals -RepositoryHealth $repositoryHealth -AuditArtifacts $auditArtifacts
    token_budget_guidance = @(
        'Use this JSON as the first-pass blocker map; read only recommended files until a concrete fix requires more evidence.',
        'For score proof, capture baseline and rerun the same scorer/audit after fixes; do not infer improvement from edits alone.'
    )
}

$jsonOutput = $result | ConvertTo-Json -Depth 10

if ($Output) {
    $parent = Split-Path -Parent $Output
    if ($parent) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    Set-Content -LiteralPath $Output -Value $jsonOutput -Encoding utf8
}

if ($Json -or -not $Output) {
    $jsonOutput
}
else {
    Write-Output "Fix-score context written to $Output"
}
