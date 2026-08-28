#!/usr/bin/env bash

set -e

FEATURE_DIR=""
JSON_MODE=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --feature-dir)
            shift
            if [[ $# -eq 0 || "$1" == --* ]]; then
                echo "ERROR: --feature-dir requires a value" >&2
                exit 1
            fi
            FEATURE_DIR="$1"
            ;;
        --json)
            JSON_MODE=true
            ;;
        --help|-h)
            echo "Usage: $0 --feature-dir <path> [--json]"
            exit 0
            ;;
        *)
            echo "ERROR: Unknown argument: $1" >&2
            exit 1
            ;;
    esac
    shift
done

if [[ -z "$FEATURE_DIR" ]]; then
    echo "ERROR: --feature-dir is required" >&2
    exit 1
fi

SCRIPT_DIR="$(CDPATH="" cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if git rev-parse --show-toplevel >/dev/null 2>&1; then
    REPO_ROOT="$(git rev-parse --show-toplevel)"
else
    REPO_ROOT="$(CDPATH="" cd "$SCRIPT_DIR/../.." && pwd)"
fi

ARGS=(--feature-dir "$FEATURE_DIR")
if [[ "$JSON_MODE" == "true" ]]; then
    ARGS+=(--json)
fi

PYTHON_BIN="python"
if command -v python3 >/dev/null 2>&1; then
    PYTHON_BIN="python3"
elif ! command -v python >/dev/null 2>&1; then
    echo "ERROR: python or python3 is required" >&2
    exit 1
fi

"$PYTHON_BIN" "$REPO_ROOT/src/devspark_cli/_knowledge.py" "${ARGS[@]}"
