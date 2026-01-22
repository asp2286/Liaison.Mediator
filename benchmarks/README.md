# Benchmarks

The benchmark suite lives in `benchmarks/Liaison.Mediator.Benchmarks` and uses BenchmarkDotNet. Commit only the generated summaries under `benchmarks/results/**` (not `BenchmarkDotNet.Artifacts`).

## Run benchmarks

From the repository root:

```bash
dotnet run -c Release --project benchmarks/Liaison.Mediator.Benchmarks --framework net8.0
```

BenchmarkDotNet writes results to `BenchmarkDotNet.Artifacts/results` by default.

## Generate and commit summaries

Run the summary generator (example: Windows/Ryzen baseline):

```bash
dotnet run --project benchmarks/tools/Benchmarks.SummaryGen -- \
  --resultsDir "BenchmarkDotNet.Artifacts/results" \
  --outDir "benchmarks/results/ryzen-win11" \
  --machine "Ryzen 9 7940HS" \
  --os "Windows 11 Pro" \
  --arch "x64" \
  --runtime "net8.0"
```

This writes:

- `benchmarks/results/<machine>/latest.summary.json`
- `benchmarks/results/<machine>/latest.summary.md`

Commit both files and (optionally) update the root `README.md` tables if you keep them in sync.

## Per-machine examples

### Win11 Pro / Ryzen 9 7940HS (baseline)

```bash
dotnet run --project benchmarks/tools/Benchmarks.SummaryGen -- \
  --resultsDir "BenchmarkDotNet.Artifacts/results" \
  --outDir "benchmarks/results/ryzen-win11" \
  --machine "Ryzen 9 7940HS" \
  --os "Windows 11 Pro" \
  --arch "x64" \
  --runtime "net8.0"
```

### macOS / Apple M3

```bash
dotnet run --project benchmarks/tools/Benchmarks.SummaryGen -- \
  --resultsDir "BenchmarkDotNet.Artifacts/results" \
  --outDir "benchmarks/results/apple-m3-macos" \
  --machine "Apple M3" \
  --os "macOS" \
  --arch "arm64" \
  --runtime "net8.0"
```

### Linux / Raspberry Pi 5

```bash
dotnet run --project benchmarks/tools/Benchmarks.SummaryGen -- \
  --resultsDir "BenchmarkDotNet.Artifacts/results" \
  --outDir "benchmarks/results/rpi5-linux" \
  --machine "Raspberry Pi 5" \
  --os "Linux" \
  --arch "arm64" \
  --runtime "net8.0"
```

## Suggested machine folders

- `benchmarks/results/ryzen-win11` (Win11 Pro / Ryzen 9 7940HS)
- `benchmarks/results/apple-m3-macos` (macOS / Apple M3)
- `benchmarks/results/rpi5-linux` (Linux / Raspberry Pi 5)
