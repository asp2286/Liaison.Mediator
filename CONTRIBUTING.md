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

Liaison.Mediator uses a lightweight commit convention to drive version bumps.

### Version bump markers

You may include **one of the following markers** in any commit message in your PR:

- `[major]` — breaking change (API removal, incompatible behavior change)
- `[minor]` — backward-compatible feature
- *(no marker)* — patch, refactor, documentation, or internal change

Only **one marker per PR is needed**.  
If multiple commits contain markers, the highest-impact one wins:

```
[major] > [minor] > patch
```

Examples:

```text
[minor] Add explicit notification publisher abstraction
```

```text
[major] Change Publish exception handling semantics
```

```text
Refactor handler invocation cache
```

> The marker does **not** need to be in the last commit.  
> All commits since the last stable tag are considered.

---

## Versioning and releases

### Stable releases

- Stable versions follow **Semantic Versioning**: `MAJOR.MINOR.PATCH`
- To publish a stable release:
  1. Tag the desired commit (for example `1.2.3`)
  2. Push the tag to the repository

The tagged commit is published exactly as-is.

### Release candidates (RC)

- Every push to `main` produces a prerelease:
  ```
  X.Y.Z-rc.N
  ```
- `X.Y.Z` is computed from commit markers since the last stable tag
- `N` is the number of commits since the last stable tag
- RC numbering resets automatically after each new stable release

Example:

```
1.0.0        ← stable tag
1.0.1-rc.1
1.0.1-rc.2
...
1.0.1        ← next stable tag
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
- Changes under `benchmarks/**` and `README.md` **do not trigger package publishing**
- Benchmark commits should **not** include version bump markers

---

## Documentation-only changes

Changes limited to:
- `README.md`
- `benchmarks/**`
- `docs/**`

do not affect versioning and should not include `[major]` or `[minor]` markers.

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
