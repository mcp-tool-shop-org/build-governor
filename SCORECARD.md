# Scorecard

> Score a repo before remediation. Fill this out first, then use SHIP_GATE.md to fix.

**Repo:** build-governor
**Date:** 2026-02-27
**Type tags:** [desktop] [cli]

## Pre-Remediation Assessment

| Category | Score | Notes |
|----------|-------|-------|
| A. Security | 4/10 | No SECURITY.md, no threat model in README |
| B. Error Handling | 8/10 | OOM classification, structured diagnostics |
| C. Operator Docs | 6/10 | Excellent README but empty LICENSE, no CHANGELOG |
| D. Shipping Hygiene | 7/10 | CI + NuGet publish present, no verify script |
| E. Identity (soft) | 10/10 | Logo, translations, landing page, metadata |
| **Overall** | **35/50** | |

## Key Gaps

1. No SECURITY.md — no vulnerability reporting process
2. Empty LICENSE file
3. No CHANGELOG.md
4. No Security & Data Scope in README

## Remediation Priority

| Priority | Item | Estimated effort |
|----------|------|-----------------|
| 1 | Create SECURITY.md + threat model in README | 5 min |
| 2 | Fix LICENSE, create CHANGELOG.md | 3 min |
| 3 | Add SHIP_GATE.md + SCORECARD.md | 5 min |

## Post-Remediation

| Category | Before | After |
|----------|--------|-------|
| A. Security | 4/10 | 10/10 |
| B. Error Handling | 8/10 | 10/10 |
| C. Operator Docs | 6/10 | 10/10 |
| D. Shipping Hygiene | 7/10 | 10/10 |
| E. Identity (soft) | 10/10 | 10/10 |
| **Overall** | **35/50** | **50/50** |
