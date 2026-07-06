# AIDLC Prompt Library

Reusable Claude Code prompts for this codebase. Each prompt is pre-loaded with eShopOnWeb-specific context so engineers don't have to re-explain the architecture each time.

## Usage

1. Open Claude Code in the repo root
2. Copy the prompt from the relevant file below
3. Replace `[PLACEHOLDER]` values with your specifics
4. Paste into Claude Code

## Prompts

| Prompt | When to use |
|---|---|
| [pre-commit-review.md](pre-commit-review.md) | Quick 60-second check before every commit |
| [code-review.md](code-review.md) | Full review before opening a PR |
| [security-review.md](security-review.md) | Security-focused review for auth/data/API changes |
| [test-generation.md](test-generation.md) | Generate unit tests for a service or domain class |
| [refactor.md](refactor.md) | Safe, pattern-consistent refactoring |

## Workflow Integration

```
Write code  →  pre-commit-review  →  commit
                                          ↓
                               test-generation (if gaps)
                                          ↓
                                    code-review
                                          ↓
                              security-review (if needed)
                                          ↓
                                   Open PR (fill PULL_REQUEST_TEMPLATE.md)
```

## Adding New Prompts

1. Create a `.md` file in this directory
2. Include: a ready-to-paste prompt block, when-to-use section, and links to related prompts
3. Add a row to the table above
