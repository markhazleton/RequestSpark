# AGENTS.md
## DevSpark

- DevSpark framework files live in `.devspark/`.
- Project artifacts and team overrides live in `.documentation/`.
- Resolve DevSpark commands through the first existing file:
  1. `.documentation/{git-user}/commands/devspark.{name}.md`
  2. `.documentation/commands/devspark.{name}.md`
  3. `.devspark/defaults/commands/devspark.{name}.md`
- Preserve user work in `.documentation/`; upgrades refresh `.devspark/` only.
