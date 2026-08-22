# Contributing to Liaison.Mediator

Thank you for your interest in contributing to **Liaison.Mediator**!

This document describes the expected workflow, commit conventions, versioning rules,
and how to run benchmarks. The goal is to keep the project predictable, reproducible,
and low-surprise for both maintainers and users.

---

## Project principles

- **Explicit over implicit**  
  Prefer explicit configuration and wiring over hidden conventions or magic behavior.

- **Deterministic behavior**  
  Startup, handler registration, execution order, and error handling should be predictable.

- **Minimal surface area**  
  The core stays small and focused. New features should be justified by real use cases.

---

## Commit message conventions

Write clear, descriptive commit messages. Commit messages do **not** drive
versioning: version selection happens exclusively through release tags
(see below).

---

## Versioning and releases

### Stable releases

- Stable versions follow **Semantic Versioning**: `MAJOR.MINOR.PATCH`
- To publish a stable release:
  1. Tag the desired commit (for example `1.2.3`)
  2. Push the tag to the repository

The tagged commit is published exactly as-is.

### Release candidates (RC)

- Release candidates are published the same way as stable versions: push a
  prerelease tag, for example `1.2.3-rc.1`.
- The tag **is** the version — nothing is computed. RC numbering is chosen
  by whoever tags.
- A prerelease tag can point at any commit that contains this tag-driven
  workflow (any commit on `main` after its introduction, or a feature branch
  based on it), so a package can be validated before the branch merges.
- Pushes to `main` do not publish anything.

Example:

```
1.1.0-rc.1   ← prerelease tag (on main or a feature branch); prerelease publish
1.1.0-rc.2   ← another RC if needed
1.1.0        ← stable tag; stable publish
```

---

## Branching model

- `main` — active development branch
- Feature branches:
  - `feature/<name>`
  - `fix/<name>`
- Pull requests should target `main`

Force-push to `main` is discouraged.

---

## Benchmarks

Benchmarks are used for **performance validation and regression detection**.
They are not intended to gate CI builds.

### Running benchmarks locally

Benchmarks are expected to be run **locally** on a known machine.

Typical flow:

1. Run benchmarks:
   ```bash
   dotnet run -c Release --project benchmarks/Liaison.Mediator.Benchmarks
   ```

2. Generate summary:
   ```bash
   dotnet run --project benchmarks/tools/Benchmarks.SummaryGen -- \
     --resultsDir benchmarks/Liaison.Mediator.Benchmarks/BenchmarkDotNet.Artifacts/results \
     --outDir benchmarks/results/<machine-id>
   ```

3. Update README tables (if applicable) using the README updater tool.

### Committing benchmark results

- Benchmark artifacts and summaries **may be committed**
- Benchmark commits never trigger package publishing — only release tags do

---

## Documentation-only changes

Changes limited to:
- `README.md`
- `benchmarks/**`
- `docs/**`

do not affect versioning or trigger publishing.

---

## Pull request guidelines

- Keep PRs focused and reasonably scoped
- Prefer small, reviewable changes
- If behavior changes, update README documentation accordingly
- If public API changes, explain the rationale clearly

---

## Code style and quality

- Nullable reference types are enabled — avoid suppressions unless justified
- Avoid introducing reflection or assembly scanning into default execution paths
- Keep allocations visible and intentional
- Prefer clarity and determinism over cleverness

---

## Design discussions and proposals

For larger changes or design discussions:

- Open an issue first
- Describe the use case and trade-offs
- Avoid proposals that increase implicit behavior by default

---

Thank you for helping improve **Liaison.Mediator**!
