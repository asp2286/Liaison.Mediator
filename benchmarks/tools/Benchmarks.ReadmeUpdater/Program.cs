using System.Globalization;
using System.Text;
using System.Text.Json;

const string usage = """
Benchmarks.ReadmeUpdater

Rewrites the README benchmarks block from committed summary JSON files.

Usage:
  dotnet run --project benchmarks/tools/Benchmarks.ReadmeUpdater -- \
    --readme "<path>" \
    --ryzen "<path>" \
    [--apple "<path>"] \
    [--rpi "<path>"]

Example:
  dotnet run --project benchmarks/tools/Benchmarks.ReadmeUpdater -- \
    --readme "README.md" \
    --ryzen "benchmarks/results/ryzen-win11/latest.summary.json" \
    --apple "benchmarks/results/apple-m3-macos/latest.summary.json" \
    --rpi "benchmarks/results/rpi5-linux/latest.summary.json"
""";

var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

try
{
    var options = Options.Parse(args);

    var ryzen = SummaryLoader.LoadRequired(options.RyzenPath);
    var apple = SummaryLoader.LoadOptional(options.ApplePath, "--apple");
    var rpi = SummaryLoader.LoadOptional(options.RpiPath, "--rpi");

    var readmeText = File.ReadAllText(options.ReadmePath);
    var newline = Newline.Detect(readmeText);

    var markdown = BenchmarksMarkdown.Generate(ryzen, apple, rpi, newline);

    var updatedReadme = ReadmeRewriter.ReplaceBetweenMarkers(
        readmeText,
        ReadmeRewriter.BeginMarker,
        ReadmeRewriter.EndMarker,
        markdown,
        newline);

    if (!string.Equals(readmeText, updatedReadme, StringComparison.Ordinal))
    {
        File.WriteAllText(options.ReadmePath, updatedReadme, utf8NoBom);
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(usage);
    return 1;
}

sealed record Options(
    string ReadmePath,
    string RyzenPath,
    string? ApplePath,
    string? RpiPath)
{
    public static Options Parse(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
            args.Contains("-h", StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Missing required arguments.");
        }

        string? readme = null;
        string? ryzen = null;
        string? apple = null;
        string? rpi = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected argument '{arg}'.");
            }

            if (i == args.Length - 1)
            {
                throw new InvalidOperationException($"Missing value for '{arg}'.");
            }

            var value = args[++i];
            switch (arg)
            {
                case "--readme":
                    readme = value;
                    break;
                case "--ryzen":
                    ryzen = value;
                    break;
                case "--apple":
                    apple = value;
                    break;
                case "--rpi":
                    rpi = value;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown option '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(readme) || string.IsNullOrWhiteSpace(ryzen))
        {
            throw new InvalidOperationException("Missing required arguments.");
        }

        if (!File.Exists(readme))
        {
            throw new InvalidOperationException($"README not found: '{readme}'.");
        }

        return new Options(ReadmePath: readme, RyzenPath: ryzen, ApplePath: apple, RpiPath: rpi);
    }
}

static class SummaryLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static Summary LoadRequired(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Required summary JSON not found: '{path}'.");
        }

        return Load(path);
    }

    public static Summary? LoadOptional(string? path, string optionName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Warning: {optionName} file not found: '{path}'. Using N/A.");
            return null;
        }

        return Load(path);
    }

    private static Summary Load(string path)
    {
        var json = File.ReadAllText(path);
        var summary = JsonSerializer.Deserialize<Summary>(json, JsonOptions);
        if (summary is null)
        {
            throw new InvalidOperationException($"Unable to parse summary JSON: '{path}'.");
        }

        if (summary.SchemaVersion <= 0)
        {
            throw new InvalidOperationException($"Unsupported schema version in '{path}': {summary.SchemaVersion}.");
        }

        return summary;
    }
}

sealed record Summary(
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    MachineInfo Machine,
    SourceInfo Source,
    IReadOnlyList<ScenarioSummary> Scenarios);

sealed record MachineInfo(string Name, string Os, string Arch, string Runtime);

sealed record SourceInfo(DateTimeOffset RunTimestampUtc, IReadOnlyList<SourceReport> Reports);

sealed record SourceReport(string Format, string FileName);

sealed record ScenarioSummary(
    string Label,
    MeasurementSummary? Liaison,
    MeasurementSummary? MediatR,
    double? Speedup,
    double? AllocReductionPct);

sealed record MeasurementSummary(double MeanNs, long AllocatedBytes);

static class BenchmarksMarkdown
{
    public static string Generate(Summary ryzen, Summary? apple, Summary? rpi, string newline)
    {
        var ryzenByLabel = ryzen.Scenarios.ToDictionary(s => s.Label, StringComparer.Ordinal);
        var appleByLabel = apple?.Scenarios.ToDictionary(s => s.Label, StringComparer.Ordinal);
        var rpiByLabel = rpi?.Scenarios.ToDictionary(s => s.Label, StringComparer.Ordinal);

        var orderedLabels = ScenarioOrdering.Order(ryzen.Scenarios.Select(s => s.Label));

        var sb = new StringBuilder();

        sb.AppendLine("## Benchmarks");
        sb.AppendLine();
        sb.AppendLine($"Primary baseline: {ryzen.Machine.Os} / {ryzen.Machine.Name}.");
        sb.AppendLine();
        sb.AppendLine("| Scenario | Runtime | MediatR Mean | Liaison Mean | Speedup | MediatR Alloc (B/op) | Liaison Alloc (B/op) | Alloc Reduction |");
        sb.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |");

        foreach (var label in orderedLabels)
        {
            ryzenByLabel.TryGetValue(label, out var scenario);
            var runtime = ryzen.Machine.Runtime;

            var mediatRMean = scenario?.MediatR is null ? "N/A" : Formatting.FormatTime(scenario.MediatR.MeanNs);
            var liaisonMean = scenario?.Liaison is null ? "N/A" : Formatting.FormatTime(scenario.Liaison.MeanNs);
            var speedup = Formatting.FormatSpeedup(scenario?.Speedup);

            var mediatRAlloc = scenario?.MediatR is null ? "N/A" : Formatting.FormatBytes(scenario.MediatR.AllocatedBytes);
            var liaisonAlloc = scenario?.Liaison is null ? "N/A" : Formatting.FormatBytes(scenario.Liaison.AllocatedBytes);
            var allocReduction = Formatting.FormatAllocReduction(scenario?.AllocReductionPct);

            sb.AppendLine($"| {Formatting.EscapeTable(label)} | {runtime} | {mediatRMean} | {liaisonMean} | {speedup} | {mediatRAlloc} | {liaisonAlloc} | {allocReduction} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Cross-platform sanity check");
        sb.AppendLine();
        sb.AppendLine("| Scenario | Ryzen speedup | Ryzen alloc reduction | Apple M3 speedup | Apple M3 alloc reduction | RPi5 speedup | RPi5 alloc reduction |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");

        foreach (var label in orderedLabels)
        {
            ryzenByLabel.TryGetValue(label, out var ryzenScenario);
            var ryzenSpeedup = ryzenScenario is null ? "N/A" : Formatting.FormatSpeedup(ryzenScenario.Speedup);
            var ryzenAlloc = ryzenScenario is null ? "N/A" : Formatting.FormatAllocReduction(ryzenScenario.AllocReductionPct);

            ScenarioSummary? appleScenario = null;
            appleByLabel?.TryGetValue(label, out appleScenario);
            var appleSpeedup = appleScenario is null ? "N/A" : Formatting.FormatSpeedup(appleScenario.Speedup);
            var appleAlloc = appleScenario is null ? "N/A" : Formatting.FormatAllocReduction(appleScenario.AllocReductionPct);

            ScenarioSummary? rpiScenario = null;
            rpiByLabel?.TryGetValue(label, out rpiScenario);
            var rpiSpeedup = rpiScenario is null ? "N/A" : Formatting.FormatSpeedup(rpiScenario.Speedup);
            var rpiAlloc = rpiScenario is null ? "N/A" : Formatting.FormatAllocReduction(rpiScenario.AllocReductionPct);

            sb.AppendLine($"| {Formatting.EscapeTable(label)} | {ryzenSpeedup} | {ryzenAlloc} | {appleSpeedup} | {appleAlloc} | {rpiSpeedup} | {rpiAlloc} |");
        }

        return sb.ToString().ReplaceLineEndings(newline);
    }
}

static class ScenarioOrdering
{
    public static IReadOnlyList<string> Order(IEnumerable<string> labels)
        => labels
            .Select(label => (label, key: ScenarioSortKey.Parse(label)))
            .OrderBy(tuple => tuple.key, ScenarioSortKeyComparer.Instance)
            .Select(tuple => tuple.label)
            .ToList();

    private sealed record ScenarioSortKey(
        string BenchmarkType,
        string ScenarioMethod,
        IReadOnlyList<(string Name, string Value)> Parameters)
    {
        public static ScenarioSortKey Parse(string label)
        {
            var slashIndex = label.IndexOf('/', StringComparison.Ordinal);
            if (slashIndex <= 0 || slashIndex == label.Length - 1)
            {
                return new ScenarioSortKey(label, string.Empty, Array.Empty<(string, string)>());
            }

            var benchmarkType = label[..slashIndex];
            var remainder = label[(slashIndex + 1)..];

            var paramsIndex = remainder.IndexOf(" (", StringComparison.Ordinal);
            if (paramsIndex < 0)
            {
                return new ScenarioSortKey(benchmarkType, remainder, Array.Empty<(string, string)>());
            }

            var scenarioMethod = remainder[..paramsIndex];
            var paramText = remainder[(paramsIndex + 2)..];
            if (paramText.EndsWith(")", StringComparison.Ordinal))
            {
                paramText = paramText[..^1];
            }

            var parameters = new List<(string Name, string Value)>();
            foreach (var part in paramText.Split(", ", StringSplitOptions.RemoveEmptyEntries))
            {
                var equalsIndex = part.IndexOf('=', StringComparison.Ordinal);
                if (equalsIndex <= 0 || equalsIndex == part.Length - 1)
                {
                    parameters.Add((part, string.Empty));
                    continue;
                }

                parameters.Add((part[..equalsIndex], part[(equalsIndex + 1)..]));
            }

            return new ScenarioSortKey(benchmarkType, scenarioMethod, parameters);
        }
    }

    private sealed class ScenarioSortKeyComparer : IComparer<ScenarioSortKey>
    {
        public static ScenarioSortKeyComparer Instance { get; } = new();

        public int Compare(ScenarioSortKey? x, ScenarioSortKey? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var cmp = MethodGroup(x.ScenarioMethod).CompareTo(MethodGroup(y.ScenarioMethod));
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = string.Compare(x.BenchmarkType, y.BenchmarkType, StringComparison.Ordinal);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = string.Compare(x.ScenarioMethod, y.ScenarioMethod, StringComparison.Ordinal);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = CompareParameters(x.Parameters, y.Parameters);
            if (cmp != 0)
            {
                return cmp;
            }

            return 0;
        }

        private static int MethodGroup(string value)
        {
            if (value.Equals("Send", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (value.Equals("Publish", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 2;
        }

        private static int CompareParameters(IReadOnlyList<(string Name, string Value)> left, IReadOnlyList<(string Name, string Value)> right)
        {
            var cmp = left.Count.CompareTo(right.Count);
            if (cmp != 0)
            {
                return cmp;
            }

            for (var i = 0; i < left.Count; i++)
            {
                cmp = string.Compare(left[i].Name, right[i].Name, StringComparison.Ordinal);
                if (cmp != 0)
                {
                    return cmp;
                }

                cmp = CompareParameterValue(left[i].Value, right[i].Value);
                if (cmp != 0)
                {
                    return cmp;
                }
            }

            return 0;
        }

        private static int CompareParameterValue(string left, string right)
        {
            if (int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out var leftInt) &&
                int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rightInt))
            {
                return leftInt.CompareTo(rightInt);
            }

            if (double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var leftDouble) &&
                double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var rightDouble))
            {
                return leftDouble.CompareTo(rightDouble);
            }

            return string.Compare(left, right, StringComparison.Ordinal);
        }
    }
}

static class Formatting
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string EscapeTable(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal);

    public static string FormatTime(double nanoseconds)
    {
        if (nanoseconds < 1000.0)
        {
            return $"{nanoseconds.ToString("0.##", Invariant)} ns";
        }

        var microseconds = nanoseconds / 1000.0;
        if (microseconds < 1000.0)
        {
            return $"{microseconds.ToString("0.##", Invariant)} us";
        }

        var milliseconds = microseconds / 1000.0;
        return $"{milliseconds.ToString("0.##", Invariant)} ms";
    }

    public static string FormatSpeedup(double? speedup)
    {
        if (speedup is null || speedup.Value <= 0)
        {
            return "N/A";
        }

        return $"x{speedup.Value.ToString("0.00", Invariant)}";
    }

    public static string FormatAllocReduction(double? allocReductionPct)
    {
        if (allocReductionPct is null)
        {
            return "N/A";
        }

        var rounded = Math.Round(allocReductionPct.Value, digits: 0, MidpointRounding.AwayFromZero);
        return $"{rounded.ToString("0", Invariant)}%";
    }

    public static string FormatBytes(long bytes)
        => bytes.ToString(Invariant);
}

static class ReadmeRewriter
{
    public const string BeginMarker = "<!-- BENCHMARKS:BEGIN -->";
    public const string EndMarker = "<!-- BENCHMARKS:END -->";

    public static string ReplaceBetweenMarkers(string input, string beginMarker, string endMarker, string replacement, string newline)
    {
        var beginIndex = input.IndexOf(beginMarker, StringComparison.Ordinal);
        var endIndex = input.IndexOf(endMarker, StringComparison.Ordinal);
        if (beginIndex < 0 || endIndex < 0 || endIndex <= beginIndex)
        {
            throw new InvalidOperationException(
                $"README markers not found or invalid. Expected '{beginMarker}' before '{endMarker}'.");
        }

        var beginContentIndex = beginIndex + beginMarker.Length;
        var before = input[..beginContentIndex];
        var after = input[endIndex..];

        var normalizedReplacement = replacement.TrimEnd('\r', '\n');
        return before + newline + normalizedReplacement + newline + after;
    }
}

static class Newline
{
    public static string Detect(string text)
        => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
}
