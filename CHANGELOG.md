# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.3] - 2026-03-25

### Added
- `Gov.Tests` — 20 xUnit tests for `FailureClassifier` and `CalculateTokenBudget`
- CI workflow (`ci.yml`) with build + test on push

## [1.0.2] - 2026-02-27

### Changed

- Added SECURITY.md with vulnerability reporting process and data scope
- Added SHIP_GATE.md and SCORECARD.md for product standards compliance
- Updated README with Security & Data Scope section and scorecard
- Fixed empty LICENSE file (now contains MIT license text)

## [1.0.1] - 2026-02-23

### Added

- Build Governor — token-based build throttle system preventing memory exhaustion
- Gov.Protocol — shared message DTOs for named-pipe IPC
- Gov.Common — Windows commit-charge monitoring and OOM failure classification
- Gov.Service — background service with 30-minute idle auto-shutdown
- Gov.Cli — `gov` CLI for status, config, and token management
- Gov.Wrapper.CL / Gov.Wrapper.Link — transparent cl.exe/link.exe shims
- NuGet package publishing via GitHub Actions
- Landing page using @mcptoolshop/site-theme

[Unreleased]: https://github.com/mcp-tool-shop-org/build-governor/compare/v1.0.3...HEAD
[1.0.3]: https://github.com/mcp-tool-shop-org/build-governor/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/mcp-tool-shop-org/build-governor/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/mcp-tool-shop-org/build-governor/releases/tag/v1.0.1
