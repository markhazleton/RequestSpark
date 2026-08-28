---
id: fix-score
name: fix-score
audience: expert
exposed: false
category: legacy-command
description: Atomic shim for /devspark.fix-score. Resolves to templates/commands/fix-score.md.
inputs: []
outputs: []
legacy_command: fix-score
---

## Outline

This atomic prompt is a backward-compatibility shim. Its execution is
delegated to the canonical command file at `templates/commands/fix-score.md`.

The workflow runner resolves this id through the standard 3-tier override
chain (personal -> team -> stock) and forwards execution to the legacy
command body.
