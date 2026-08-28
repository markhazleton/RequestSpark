#!/usr/bin/env bash
# Gathers compact score-remediation context as JSON for /devspark.fix-score.

set -euo pipefail

SCRIPT_DIR="$(CDPATH="" cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/common.sh"

JSON_MODE=false
OUTPUT_PATH=""
ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --json)
      JSON_MODE=true
      shift
      ;;
    --output)
      OUTPUT_PATH="${2:-}"
      shift 2
      ;;
    --output=*)
      OUTPUT_PATH="${1#*=}"
      shift
      ;;
    *)
      ARGS+=("$1")
      shift
      ;;
  esac
done

PYTHON_CMD=""
if command -v python3 >/dev/null 2>&1; then
  PYTHON_CMD="python3"
elif command -v python >/dev/null 2>&1; then
  PYTHON_CMD="python"
else
  echo "ERROR: python3/python is required to generate JSON output." >&2
  exit 1
fi

REPO_ROOT=$(get_repo_root)
CURRENT_BRANCH=$(get_current_branch)

JSON_OUTPUT=$(
  "$PYTHON_CMD" - "$REPO_ROOT" "$CURRENT_BRANCH" "${ARGS[@]}" <<'PY'
import json
import os
import re
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path


repo_root = Path(sys.argv[1])
current_branch = sys.argv[2]
raw_args = sys.argv[3:]


def git_lines(*args: str) -> list[str]:
    result = subprocess.run(["git", *args], cwd=repo_root, text=True, capture_output=True, check=False)
    if result.returncode != 0:
        return []
    return [line for line in result.stdout.splitlines() if line.strip()]


def git_text(*args: str) -> str:
    result = subprocess.run(["git", *args], cwd=repo_root, text=True, capture_output=True, check=False)
    return result.stdout.strip() if result.returncode == 0 else ""


def rel(path: Path) -> str:
    return path.relative_to(repo_root).as_posix()


def exists_any(names: list[str]) -> list[str]:
    return [name for name in names if (repo_root / name).exists()]


def parse_scope(args: list[str]) -> dict:
    scope = {"repo": None, "user": None, "category": None, "audit": None, "raw": args}
    for arg in args:
        for key in ("repo", "user", "category", "audit"):
            prefix = f"{key}:"
            if arg.startswith(prefix):
                scope[key] = arg[len(prefix):]
    return scope


def read_text(path: Path, limit: int = 200_000) -> str:
    try:
        data = path.read_bytes()[:limit]
        return data.decode("utf-8", errors="replace")
    except OSError:
        return ""


def readme_metrics() -> dict:
    candidates = sorted(repo_root.glob("README*"))
    readme = next((p for p in candidates if p.is_file()), None)
    if not readme:
        return {
            "present": False,
            "path": None,
            "estimated_quality_score": 0,
            "score_inputs": {
                "characters": 0,
                "headings": 0,
                "code_blocks": 0,
                "links": 0,
                "images_or_badges": 0,
                "has_install_or_usage_section": False,
            },
            "opportunities": ["Add README with headings, usage/install section, code examples, links, and badges/images."],
        }
    text = read_text(readme)
    headings = len(re.findall(r"(?m)^#{1,6}\s+\S", text))
    code_blocks = len(re.findall(r"(?m)^```", text)) // 2
    links = len(re.findall(r"\[[^\]]+\]\([^)]+\)|https?://\S+", text))
    images = len(re.findall(r"!\[[^\]]*\]\([^)]+\)|img\.shields\.io|badge", text, flags=re.I))
    has_install = bool(re.search(r"\b(install|getting started|usage|quick start|setup)\b", text, flags=re.I))
    score = min(20, len(text) // 200) + min(20, headings * 4) + min(15, code_blocks * 5)
    score += min(15, links * 3) + min(15, images * 5) + (15 if has_install else 0)
    opportunities = []
    if len(text) < 4000:
        opportunities.append("Expand README substance; length contributes up to 20 points.")
    if headings < 5:
        opportunities.append("Add clear README sections; headings contribute up to 20 points.")
    if code_blocks < 3:
        opportunities.append("Add runnable examples; code blocks contribute up to 15 points.")
    if links < 5:
        opportunities.append("Add relevant docs/project links; links contribute up to 15 points.")
    if images < 3:
        opportunities.append("Add badges or useful images; images/badges contribute up to 15 points.")
    if not has_install:
        opportunities.append("Add install, setup, quick start, or usage section for 15 points.")
    return {
        "present": True,
        "path": rel(readme),
        "estimated_quality_score": min(100, score),
        "score_inputs": {
            "characters": len(text),
            "headings": headings,
            "code_blocks": code_blocks,
            "links": links,
            "images_or_badges": images,
            "has_install_or_usage_section": has_install,
        },
        "opportunities": opportunities[:6],
    }


def dependency_signals() -> dict:
    manifests = exists_any([
        "package.json", "pyproject.toml", "requirements.txt", "Pipfile", "poetry.lock",
        "go.mod", "Cargo.toml", "pom.xml", "build.gradle", "build.gradle.kts", "Directory.Packages.props",
    ])
    lockfiles = exists_any([
        "package-lock.json", "npm-shrinkwrap.json", "yarn.lock", "pnpm-lock.yaml", "poetry.lock",
        "Pipfile.lock", "requirements.lock", "go.sum", "Cargo.lock", "packages.lock.json",
    ])
    direct_count = None
    package_json = repo_root / "package.json"
    if package_json.exists():
        try:
            pkg = json.loads(read_text(package_json))
            direct_count = sum(len(pkg.get(key, {}) or {}) for key in ("dependencies", "devDependencies", "peerDependencies", "optionalDependencies"))
        except json.JSONDecodeError:
            direct_count = None
    opportunities = []
    if manifests and not lockfiles:
        opportunities.append("Add or restore lockfiles so dependency state is reproducible before currency checks.")
    if manifests:
        opportunities.append("Run project-native outdated/audit commands and update fixable direct dependencies.")
    return {
        "manifests": manifests,
        "lockfiles": lockfiles,
        "direct_dependency_count_package_json": direct_count,
        "currency_score_note": "Dependency currency requires registry/latest-version data; this context only identifies local manifests and lockfile readiness.",
        "opportunities": opportunities,
    }


def repository_health() -> dict:
    workflow_files = []
    workflows_dir = repo_root / ".github" / "workflows"
    if workflows_dir.exists():
        workflow_files = [rel(p) for p in sorted(workflows_dir.glob("*")) if p.is_file()][:10]
    license_files = exists_any(["LICENSE", "LICENSE.md", "COPYING", "NOTICE"])
    return {
        "readme_present": bool(readme_metrics_cache["present"]),
        "license_present": bool(license_files),
        "license_files": license_files,
        "ci_present": bool(workflow_files),
        "workflow_files_sample": workflow_files,
        "opportunities": [
            item for item, missing in (
                ("Add a license file; front-end maintenance score penalizes missing license.", not license_files),
                ("Add CI workflow; front-end maintenance score penalizes missing CI/CD.", not workflow_files),
            ) if missing
        ],
    }


def activity_signals() -> dict:
    last_commit_iso = git_text("log", "-1", "--format=%cI")
    days_since_last_commit = None
    if last_commit_iso:
        try:
            last_dt = datetime.fromisoformat(last_commit_iso.replace("Z", "+00:00"))
            days_since_last_commit = (datetime.now(timezone.utc) - last_dt).days
        except ValueError:
            pass
    return {
        "current_branch": current_branch,
        "last_commit_iso": last_commit_iso or None,
        "days_since_last_commit": days_since_last_commit,
        "commit_counts": {
            "last_90_days": len(git_lines("log", "--since=90 days ago", "--format=%H")),
            "last_180_days": len(git_lines("log", "--since=180 days ago", "--format=%H")),
            "last_365_days": len(git_lines("log", "--since=365 days ago", "--format=%H")),
            "total": int(git_text("rev-list", "--count", "HEAD") or "0"),
        },
        "active_weeks_last_52": len(set(line[:10] for line in git_lines("log", "--since=52 weeks ago", "--date=format:%G-%V", "--format=%cd"))),
        "anti_gaming_note": "Do not create empty or meaningless commits to manipulate activity, consistency, or recency scores.",
    }


def gh_json(args: list[str]):
    result = subprocess.run(["gh", *args], cwd=repo_root, text=True, capture_output=True, check=False)
    if result.returncode != 0:
        return None
    try:
        return json.loads(result.stdout)
    except json.JSONDecodeError:
        return None


def attention_signals() -> dict:
    gh_available = bool(shutil.which("gh")) and subprocess.run(["gh", "--version"], text=True, capture_output=True, check=False).returncode == 0
    open_prs = None
    open_issues = None
    security_alerts = None
    if gh_available:
        prs = gh_json(["pr", "list", "--state", "open", "--limit", "100", "--json", "number,isDraft,reviewRequests,createdAt"])
        issues = gh_json(["issue", "list", "--state", "open", "--limit", "100", "--json", "number,createdAt"])
        if isinstance(prs, list):
            open_prs = {
                "count": len(prs),
                "draft_count": sum(1 for pr in prs if pr.get("isDraft")),
                "review_requested_count": sum(1 for pr in prs if pr.get("reviewRequests")),
            }
        if isinstance(issues, list):
            open_issues = {"count": len(issues)}
        alerts = gh_json(["api", "repos/{owner}/{repo}/dependabot/alerts", "--paginate"])
        if isinstance(alerts, list):
            by_severity = {}
            for alert in alerts:
                severity = ((alert.get("security_vulnerability") or {}).get("severity") or "unknown").lower()
                by_severity[severity] = by_severity.get(severity, 0) + 1
            security_alerts = by_severity
    return {
        "github_cli_available": gh_available,
        "open_prs": open_prs,
        "open_issues": open_issues,
        "dependabot_alerts_by_severity": security_alerts,
        "security_data_note": "Null alert data means unavailable, not healthy. GitHubSpark adds availability penalties for partial/unavailable security data.",
    }


def audit_artifacts() -> list[dict]:
    roots = [repo_root / ".documentation", repo_root / ".devspark"]
    patterns = ["*audit*.md", "*audit*.json", "*score*.json", "*score*.md", "*diagnostic*.json", "*diagnostic*.md"]
    found = []
    for root in roots:
        if not root.exists():
            continue
        for pattern in patterns:
            for path in root.rglob(pattern):
                if path.is_file():
                    try:
                        found.append({"path": rel(path), "bytes": path.stat().st_size})
                    except OSError:
                        pass
    return sorted(found, key=lambda item: item["path"])[:20]


def recommended_reads(scope: dict) -> list[dict]:
    category = (scope.get("category") or "").lower()
    reads = []
    if not category or "readme" in category:
        if readme_metrics_cache["path"]:
            reads.append({"path": readme_metrics_cache["path"], "why": "README quality score inputs and low-cost improvements."})
    if not category or "depend" in category:
        for path in dependency_signals_cache["manifests"][:5]:
            reads.append({"path": path, "why": "Dependency currency and audit command discovery."})
    if not category or "attention" in category or "maintenance" in category:
        for path in repository_health_cache["workflow_files_sample"][:3]:
            reads.append({"path": path, "why": "CI/CD presence and maintenance signal."})
    for artifact in audit_artifacts_cache[:3]:
        reads.append({"path": artifact["path"], "why": "Existing score/audit signal; validate before trusting."})
    return reads[:10]


scope = parse_scope(raw_args)
readme_metrics_cache = readme_metrics()
dependency_signals_cache = dependency_signals()
repository_health_cache = repository_health()
activity_signals_cache = activity_signals()
attention_signals_cache = attention_signals()
audit_artifacts_cache = audit_artifacts()

result = {
    "schema_version": 1,
    "generated_at": datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    "repo_root": str(repo_root),
    "scope": scope,
    "score_categories": {
        "profile-spark": {"direction": "increase", "perfect": 100, "local_fixability": "limited", "primary_levers": ["consistency", "volume", "collaboration"]},
        "repository-composite": {"direction": "increase", "perfect": 100, "local_fixability": "partial", "primary_levers": ["activity", "health", "popularity"]},
        "repository-attention": {"direction": "decrease", "perfect": 0, "local_fixability": "high", "primary_levers": ["open_pr_pressure", "security_alerts", "staleness", "dependency_attention"]},
        "dependency-currency": {"direction": "increase", "perfect": 100, "local_fixability": "high", "primary_levers": ["outdated_dependencies", "version_coverage", "latest_version_coverage"]},
        "readme-quality": {"direction": "increase", "perfect": 100, "local_fixability": "high", "primary_levers": ["length", "headings", "code_blocks", "links", "images_or_badges", "install_or_usage_section"]},
        "frontend-maintenance": {"direction": "decrease", "perfect": 0, "local_fixability": "high", "primary_levers": ["staleness", "missing_readme", "missing_license", "missing_ci", "open_issues", "open_prs", "security_alerts"]},
    },
    "signals": {
        "readme_quality": readme_metrics_cache,
        "repository_health": repository_health_cache,
        "activity": activity_signals_cache,
        "dependencies": dependency_signals_cache,
        "attention": attention_signals_cache,
        "audit_artifacts_sample": audit_artifacts_cache,
    },
    "recommended_context_reads": recommended_reads(scope),
    "token_budget_guidance": [
        "Use this JSON as the first-pass blocker map; read only recommended files until a concrete fix requires more evidence.",
        "For score proof, capture baseline and rerun the same scorer/audit after fixes; do not infer improvement from edits alone.",
    ],
}

print(json.dumps(result, indent=2))
PY
)

if [[ -n "$OUTPUT_PATH" ]]; then
  mkdir -p "$(dirname "$OUTPUT_PATH")"
  printf '%s\n' "$JSON_OUTPUT" > "$OUTPUT_PATH"
fi

if [[ "$JSON_MODE" == true || -z "$OUTPUT_PATH" ]]; then
  printf '%s\n' "$JSON_OUTPUT"
else
  printf 'Fix-score context written to %s\n' "$OUTPUT_PATH"
fi
