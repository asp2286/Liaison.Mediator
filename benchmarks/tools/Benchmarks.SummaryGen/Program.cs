using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string usage = """
Benchmarks.SummaryGen

Usage:
  Benchmarks.SummaryGen --resultsDir "<path>" --outDir "<path>" --machine "<name>" --os "<os>" --arch "<arch>" --runtime "<runtime>"

Example:
  dotnet run --project benchmarks/tools/Benchmarks.SummaryGen -- \
    --resultsDir "BenchmarkDotNet.Artifacts/results" \
    --outDir "benchmarks/results/apple-m3-macos" \
    --machine "Apple M3" \
    --os "macOS" \
    --arch "arm64" \
    --runtime "net8.0"
""";

try
{
    var options = Options.Parse(args);
    var results = BenchmarkResults.Load(options.ResultsDir, options.TargetRuntimeVersion);
    var summary = Summary.FromResults(options, results);
    SummaryWriter.Write(summary, options.OutDir);
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
    string ResultsDir,
    string OutDir,
    string Machine,
    string Os,
    string Arch,
    string Runtime,
    string TargetRuntimeVersion)
{
    public static Options Parse(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
            args.Contains("-h", StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Missing required arguments.");
        }

        string? resultsDir = null;
        string? outDir = null;
        string? machine = null;
        string? os = null;
        string? arch = null;
        string? runtime = null;

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
                case "--resultsDir":
                    resultsDir = value;
                    break;
                case "--outDir":
                    outDir = value;
                    break;
                case "--machine":
                    machine = value;
                    break;
                case "--os":
                    os = value;
                    break;
                case "--arch":
                    arch = value;
                    break;
                case "--runtime":
                    runtime = value;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown option '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(resultsDir) ||
            string.IsNullOrWhiteSpace(outDir) ||
            string.IsNullOrWhiteSpace(machine) ||
            string.IsNullOrWhiteSpace(os) ||
            string.IsNullOrWhiteSpace(arch) ||
            string.IsNullOrWhiteSpace(runtime))
        {
            throw new InvalidOperationException("Missing required arguments.");
        }

        var targetRuntimeVersion = RuntimeVersion.Extract(runtime);

        return new Options(
            ResultsDir: resultsDir,
            OutDir: outDir,
            Machine: machine,
            Os: os,
            Arch: arch,
            Runtime: runtime,
            TargetRuntimeVersion: targetRuntimeVersion);
    }
}

static class RuntimeVersion
{
    private static readonly Regex VersionRegex = new(@"(?<v>\d+\.\d+)", RegexOptions.Compiled);

    public static string Extract(string runtime)
    {
        if (string.IsNullOrWhiteSpace(runtime))
        {
            throw new InvalidOperationException("Runtime is required.");
        }

        var match = VersionRegex.Match(runtime);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Unable to infer runtime version from '{runtime}'. Expected something like 'net8.0'.");
        }

        return match.Groups["v"].Value;
    }

    public static string? TryExtract(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = VersionRegex.Match(value);
        return match.Success ? match.Groups["v"].Value : null;
    }
}

enum ResultsFormat
{
    Json,
    Csv,
    Markdown,
}

sealed record BenchmarkRow(
    string BenchmarkType,
    string Method,
    Provider Provider,
    string ScenarioMethod,
    IReadOnlyDictionary<string, string> Parameters,
    string? Job,
    string? RuntimeVersion,
    double MeanNs,
    long AllocatedBytes)
{
    public string ScenarioKey => ScenarioLabel.Build(BenchmarkType, ScenarioMethod, Parameters);
}

enum Provider
{
    Liaison,
    MediatR,
    Other,
}

static class ScenarioLabel
{
    public static string Build(string benchmarkType, string scenarioMethod, IReadOnlyDictionary<string, string> parameters)
    {
        var label = $"{benchmarkType}/{scenarioMethod}";
        if (parameters.Count == 0)
        {
            return label;
        }

        var joined = string.Join(
            ", ",
            parameters
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return $"{label} ({joined})";
    }
}

sealed record SourceReport(string Format, string FileName);

sealed record BenchmarkResults(
    DateTimeOffset RunTimestampUtc,
    IReadOnlyList<SourceReport> Sources,
    IReadOnlyList<BenchmarkRow> Rows)
{
    private static readonly TimeSpan RecentWindow = TimeSpan.FromMinutes(10);
    private const string ReportMarker = "-report";

    public static BenchmarkResults Load(string resultsDir, string targetRuntimeVersion)
    {
        if (!Directory.Exists(resultsDir))
        {
            throw new InvalidOperationException($"Results directory not found: '{resultsDir}'.");
        }

        var candidates = EnumerateReportFiles(resultsDir);
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"No BenchmarkDotNet report files found under '{resultsDir}'.");
        }

        var mostRecentFile = candidates
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .First();

        var runTimestampUtc = mostRecentFile.LastWriteTimeUtc;
        var cutoffUtc = mostRecentFile.LastWriteTimeUtc - RecentWindow;

        var recentCandidates = candidates
            .Where(fi => fi.LastWriteTimeUtc >= cutoffUtc)
            .ToList();

        var selectedFiles = recentCandidates
            .GroupBy(fi => fi.BaseName, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(fi => FormatPriority(fi.Format))
                .ThenByDescending(fi => fi.LastWriteTimeUtc)
                .ThenBy(fi => fi.FileName, StringComparer.Ordinal)
                .First())
            .OrderBy(fi => fi.FileName, StringComparer.Ordinal)
            .ToList();

        var rows = new List<BenchmarkRow>();
        foreach (var file in selectedFiles)
        {
            rows.AddRange(file.Format switch
            {
                ResultsFormat.Json => JsonReportParser.Parse(file.FullPath),
                ResultsFormat.Csv => CsvReportParser.Parse(file.FullPath),
                ResultsFormat.Markdown => MarkdownReportParser.Parse(file.FullPath),
                _ => throw new InvalidOperationException($"Unsupported format '{file.Format}'."),
            });
        }

        if (rows.Any(row => !string.IsNullOrWhiteSpace(row.RuntimeVersion)))
        {
            rows = rows
                .Where(row => string.Equals(row.RuntimeVersion, targetRuntimeVersion, StringComparison.Ordinal))
                .ToList();
        }

        var sources = selectedFiles
            .Select(file => new SourceReport(file.Format.ToString(), file.FileName))
            .ToList();

        return new BenchmarkResults(runTimestampUtc, sources, rows);
    }

    private static int FormatPriority(ResultsFormat format)
        => format switch
        {
            ResultsFormat.Json => 0,
            ResultsFormat.Csv => 1,
            ResultsFormat.Markdown => 2,
            _ => 10,
        };

    private static List<ReportFile> EnumerateReportFiles(string resultsDir)
    {
        var list = new List<ReportFile>();

        foreach (var path in Directory.EnumerateFiles(resultsDir, "*-report*.json", SearchOption.TopDirectoryOnly))
        {
            list.Add(ReportFile.FromPath(path, ResultsFormat.Json));
        }

        foreach (var path in Directory.EnumerateFiles(resultsDir, "*-report*.csv", SearchOption.TopDirectoryOnly))
        {
            list.Add(ReportFile.FromPath(path, ResultsFormat.Csv));
        }

        foreach (var path in Directory.EnumerateFiles(resultsDir, "*-report*.md", SearchOption.TopDirectoryOnly))
        {
            list.Add(ReportFile.FromPath(path, ResultsFormat.Markdown));
        }

        return list;
    }

    private static string BaseNameFromFileName(string fileNameWithoutExtension)
    {
        var idx = fileNameWithoutExtension.IndexOf(ReportMarker, StringComparison.OrdinalIgnoreCase);
        return idx <= 0 ? fileNameWithoutExtension : fileNameWithoutExtension[..idx];
    }

    private sealed record ReportFile(
        string FullPath,
        string FileName,
        string BaseName,
        ResultsFormat Format,
        DateTimeOffset LastWriteTimeUtc)
    {
        public static ReportFile FromPath(string fullPath, ResultsFormat format)
        {
            var fileName = Path.GetFileName(fullPath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
            var baseName = BaseNameFromFileName(fileNameWithoutExtension);
            var lastWriteTimeUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(fullPath), TimeSpan.Zero);
            return new ReportFile(fullPath, fileName, baseName, format, lastWriteTimeUtc);
        }
    }
}

static class ProviderParser
{
    public static (Provider Provider, string ScenarioMethod) Parse(string method)
    {
        if (method.StartsWith("Liaison_", StringComparison.OrdinalIgnoreCase))
        {
            return (Provider.Liaison, method["Liaison_".Length..]);
        }

        if (method.StartsWith("MediatR_", StringComparison.OrdinalIgnoreCase))
        {
            return (Provider.MediatR, method["MediatR_".Length..]);
        }

        return (Provider.Other, method);
    }
}

static class JsonReportParser
{
    public static IReadOnlyList<BenchmarkRow> Parse(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("Benchmarks", out var benchmarksElement) ||
            benchmarksElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"JSON file '{filePath}' does not look like a BenchmarkDotNet JSON report.");
        }

        var rows = new List<BenchmarkRow>();
        foreach (var benchmark in benchmarksElement.EnumerateArray())
        {
            var benchmarkType = benchmark.GetProperty("Type").GetString() ?? "Unknown";
            var method = benchmark.GetProperty("Method").GetString() ?? "Unknown";
            var displayInfo = benchmark.GetProperty("DisplayInfo").GetString();
            var jobName = ParseJobName(displayInfo);
            var runtimeVersion = ParseRuntimeVersion(displayInfo);

            var (provider, scenarioMethod) = ProviderParser.Parse(method);
            var parameters = ParseParameters(benchmark);

            var meanNs = benchmark.GetProperty("Statistics").GetProperty("Mean").GetDouble();
            var allocatedBytes = benchmark.GetProperty("Memory").GetProperty("BytesAllocatedPerOperation").GetInt64();

            rows.Add(new BenchmarkRow(
                BenchmarkType: benchmarkType,
                Method: method,
                Provider: provider,
                ScenarioMethod: scenarioMethod,
                Parameters: parameters,
                Job: jobName,
                RuntimeVersion: runtimeVersion,
                MeanNs: meanNs,
                AllocatedBytes: allocatedBytes));
        }

        return SelectPreferredJobs(rows);
    }

    private static IReadOnlyDictionary<string, string> ParseParameters(JsonElement benchmark)
    {
        if (!benchmark.TryGetProperty("Parameters", out var parametersElement))
        {
            return new Dictionary<string, string>();
        }

        var raw = parametersElement.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new Dictionary<string, string>();
        }

        // BenchmarkDotNet exports parameters as a formatted string (e.g. "HandlerCount=10").
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=', StringComparison.Ordinal);
            if (idx <= 0 || idx == part.Length - 1)
            {
                continue;
            }

            dictionary[part[..idx].Trim()] = part[(idx + 1)..].Trim();
        }

        return dictionary;
    }

    private static string? ParseJobName(string? displayInfo)
    {
        if (string.IsNullOrWhiteSpace(displayInfo))
        {
            return null;
        }

        // Example: "Send_DI.Liaison_Send: .NET 8.0(Runtime=.NET 8.0)"
        var colon = displayInfo.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0 || colon == displayInfo.Length - 1)
        {
            return null;
        }

        var afterColon = displayInfo[(colon + 1)..].TrimStart();
        var paren = afterColon.IndexOf('(', StringComparison.Ordinal);
        return paren < 0 ? afterColon : afterColon[..paren];
    }

    private static string? ParseRuntimeVersion(string? displayInfo)
    {
        if (string.IsNullOrWhiteSpace(displayInfo))
        {
            return null;
        }

        // Example: "Send_DI.Liaison_Send: .NET 8.0(Runtime=.NET 8.0)"
        return RuntimeVersion.TryExtract(displayInfo);
    }

    private static List<BenchmarkRow> SelectPreferredJobs(List<BenchmarkRow> rows)
    {
        var best = new Dictionary<(string ScenarioKey, Provider Provider, string RuntimeVersion), BenchmarkRow>();
        foreach (var row in rows)
        {
            var key = (row.ScenarioKey, row.Provider, row.RuntimeVersion ?? string.Empty);
            if (!best.TryGetValue(key, out var existing))
            {
                best[key] = row;
                continue;
            }

            if (JobPriority(row.Job) < JobPriority(existing.Job))
            {
                best[key] = row;
            }
        }

        return best.Values.OrderBy(r => r.ScenarioKey, StringComparer.Ordinal).ToList();
    }

    private static int JobPriority(string? job)
    {
        if (string.IsNullOrWhiteSpace(job))
        {
            return 3;
        }

        if (job.Equals("Dry", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        if (job.StartsWith(".NET", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return 2;
    }
}

static class CsvReportParser
{
    public static IReadOnlyList<BenchmarkRow> Parse(string filePath)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            return Array.Empty<BenchmarkRow>();
        }

        var headers = Csv.SplitLine(headerLine);
        var headerIndex = headers
            .Select((name, index) => (name, index))
            .ToDictionary(pair => pair.name, pair => pair.index, StringComparer.OrdinalIgnoreCase);

        if (!headerIndex.TryGetValue("Method", out var methodIndex) ||
            !headerIndex.TryGetValue("Mean", out var meanIndex) ||
            !headerIndex.TryGetValue("Allocated", out var allocatedIndex))
        {
            throw new InvalidOperationException($"CSV file '{filePath}' does not contain required columns.");
        }

        var jobIndex = headerIndex.TryGetValue("Job", out var jobIndexValue) ? jobIndexValue : -1;
        var runtimeIndex = headerIndex.TryGetValue("Runtime", out var runtimeIndexValue) ? runtimeIndexValue : -1;

        var parameters = ResolveParameterColumns(headers, headerIndex);
        var benchmarkType = BenchmarkTypeFromFileName(filePath);

        var rows = new List<BenchmarkRow>();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = Csv.SplitLine(line);
            if (fields.Length != headers.Length)
            {
                continue;
            }

            var method = fields[methodIndex];
            var (provider, scenarioMethod) = ProviderParser.Parse(method);

            var runtimeVersion = runtimeIndex >= 0 ? RuntimeVersion.TryExtract(fields[runtimeIndex]) : null;
            var job = jobIndex >= 0 ? fields[jobIndex] : null;

            var meanNs = MeasurementParser.ParseTimeToNanoseconds(fields[meanIndex]);
            var allocatedBytes = MeasurementParser.ParseBytes(fields[allocatedIndex]);

            var paramDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, index) in parameters)
            {
                var value = fields[index];
                if (!string.IsNullOrWhiteSpace(value) && !value.Equals("Default", StringComparison.OrdinalIgnoreCase))
                {
                    paramDictionary[name] = value;
                }
            }

            rows.Add(new BenchmarkRow(
                BenchmarkType: benchmarkType,
                Method: method,
                Provider: provider,
                ScenarioMethod: scenarioMethod,
                Parameters: paramDictionary,
                Job: job,
                RuntimeVersion: runtimeVersion,
                MeanNs: meanNs,
                AllocatedBytes: allocatedBytes));
        }

        return SelectPreferredJobs(rows);
    }

    private static IReadOnlyList<(string name, int index)> ResolveParameterColumns(string[] headers, IReadOnlyDictionary<string, int> headerIndex)
    {
        if (!headerIndex.TryGetValue("WarmupCount", out var warmupCountIndex) ||
            !headerIndex.TryGetValue("Mean", out var meanIndex))
        {
            return Array.Empty<(string, int)>();
        }

        if (meanIndex <= warmupCountIndex + 1)
        {
            return Array.Empty<(string, int)>();
        }

        var list = new List<(string, int)>();
        for (var i = warmupCountIndex + 1; i < meanIndex; i++)
        {
            list.Add((headers[i], i));
        }

        return list;
    }

    private static List<BenchmarkRow> SelectPreferredJobs(List<BenchmarkRow> rows)
    {
        var best = new Dictionary<(string ScenarioKey, Provider Provider, string RuntimeVersion), BenchmarkRow>();
        foreach (var row in rows)
        {
            var key = (row.ScenarioKey, row.Provider, row.RuntimeVersion ?? string.Empty);
            if (!best.TryGetValue(key, out var existing))
            {
                best[key] = row;
                continue;
            }

            if (JobPriority(row.Job, row.RuntimeVersion) < JobPriority(existing.Job, existing.RuntimeVersion))
            {
                best[key] = row;
            }
        }

        return best.Values.OrderBy(r => r.ScenarioKey, StringComparer.Ordinal).ToList();
    }

    private static int JobPriority(string? job, string? runtimeVersion)
    {
        if (string.IsNullOrWhiteSpace(job))
        {
            return 3;
        }

        if (job.Equals("Dry", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        if (!string.IsNullOrWhiteSpace(runtimeVersion) &&
            job.Contains(runtimeVersion, StringComparison.OrdinalIgnoreCase) &&
            job.StartsWith(".NET", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (job.StartsWith(".NET", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static string BenchmarkTypeFromFileName(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);

        var reportIndex = name.IndexOf("-report", StringComparison.OrdinalIgnoreCase);
        if (reportIndex > 0)
        {
            name = name[..reportIndex];
        }

        var lastDot = name.LastIndexOf('.');
        return lastDot >= 0 ? name[(lastDot + 1)..] : name;
    }
}

static class MarkdownReportParser
{
    public static IReadOnlyList<BenchmarkRow> Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var tableStart = Array.FindIndex(lines, line => line.TrimStart().StartsWith("|", StringComparison.Ordinal));
        if (tableStart < 0 || tableStart >= lines.Length - 2)
        {
            return Array.Empty<BenchmarkRow>();
        }

        var headerLine = lines[tableStart];
        var separatorLine = lines[tableStart + 1];
        if (!separatorLine.Contains("---", StringComparison.Ordinal))
        {
            return Array.Empty<BenchmarkRow>();
        }

        var headers = Markdown.SplitRow(headerLine);
        var headerIndex = headers
            .Select((name, index) => (name, index))
            .ToDictionary(pair => pair.name, pair => pair.index, StringComparer.OrdinalIgnoreCase);

        if (!headerIndex.TryGetValue("Method", out var methodIndex) ||
            !headerIndex.TryGetValue("Mean", out var meanIndex) ||
            !headerIndex.TryGetValue("Allocated", out var allocatedIndex))
        {
            return Array.Empty<BenchmarkRow>();
        }

        var jobIndex = headerIndex.TryGetValue("Job", out var jobIndexValue) ? jobIndexValue : -1;
        var runtimeIndex = headerIndex.TryGetValue("Runtime", out var runtimeIndexValue) ? runtimeIndexValue : -1;
        var benchmarkType = BenchmarkTypeFromFileName(filePath);

        var parameters = ResolveParameterColumns(headers, headerIndex);
        var rows = new List<BenchmarkRow>();

        for (var i = tableStart + 2; i < lines.Length; i++)
        {
            var rowLine = lines[i].Trim();
            if (!rowLine.StartsWith("|", StringComparison.Ordinal))
            {
                break;
            }

            var fields = Markdown.SplitRow(rowLine);
            if (fields.Count != headers.Count)
            {
                continue;
            }

            var method = fields[methodIndex];
            var (provider, scenarioMethod) = ProviderParser.Parse(method);
            var runtimeVersion = runtimeIndex >= 0 ? RuntimeVersion.TryExtract(fields[runtimeIndex]) : null;
            var job = jobIndex >= 0 ? fields[jobIndex] : null;
            var meanNs = MeasurementParser.ParseTimeToNanoseconds(fields[meanIndex]);
            var allocatedBytes = MeasurementParser.ParseBytes(fields[allocatedIndex]);

            var paramDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, index) in parameters)
            {
                var value = fields[index];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    paramDictionary[name] = value;
                }
            }

            rows.Add(new BenchmarkRow(
                BenchmarkType: benchmarkType,
                Method: method,
                Provider: provider,
                ScenarioMethod: scenarioMethod,
                Parameters: paramDictionary,
                Job: job,
                RuntimeVersion: runtimeVersion,
                MeanNs: meanNs,
                AllocatedBytes: allocatedBytes));
        }

        return SelectPreferredJobs(rows);
    }

    private static IReadOnlyList<(string name, int index)> ResolveParameterColumns(IReadOnlyList<string> headers, IReadOnlyDictionary<string, int> headerIndex)
    {
        if (!headerIndex.TryGetValue("Runtime", out var runtimeIndex) ||
            !headerIndex.TryGetValue("Mean", out var meanIndex))
        {
            return Array.Empty<(string, int)>();
        }

        if (meanIndex <= runtimeIndex + 1)
        {
            return Array.Empty<(string, int)>();
        }

        var list = new List<(string, int)>();
        for (var i = runtimeIndex + 1; i < meanIndex; i++)
        {
            if (headers[i].Equals("Job", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            list.Add((headers[i], i));
        }

        return list;
    }

    private static string BenchmarkTypeFromFileName(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);

        var reportIndex = name.IndexOf("-report", StringComparison.OrdinalIgnoreCase);
        if (reportIndex > 0)
        {
            name = name[..reportIndex];
        }

        var lastDot = name.LastIndexOf('.');
        return lastDot >= 0 ? name[(lastDot + 1)..] : name;
    }

    private static List<BenchmarkRow> SelectPreferredJobs(List<BenchmarkRow> rows)
    {
        var best = new Dictionary<(string ScenarioKey, Provider Provider, string RuntimeVersion), BenchmarkRow>();
        foreach (var row in rows)
        {
            var key = (row.ScenarioKey, row.Provider, row.RuntimeVersion ?? string.Empty);
            if (!best.TryGetValue(key, out var existing))
            {
                best[key] = row;
                continue;
            }

            if (JobPriority(row.Job, row.RuntimeVersion) < JobPriority(existing.Job, existing.RuntimeVersion))
            {
                best[key] = row;
            }
        }

        return best.Values.OrderBy(r => r.ScenarioKey, StringComparer.Ordinal).ToList();
    }

    private static int JobPriority(string? job, string? runtimeVersion)
    {
        if (string.IsNullOrWhiteSpace(job))
        {
            return 3;
        }

        if (job.Equals("Dry", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        if (!string.IsNullOrWhiteSpace(runtimeVersion) &&
            job.Contains(runtimeVersion, StringComparison.OrdinalIgnoreCase) &&
            job.StartsWith(".NET", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (job.StartsWith(".NET", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }
}

static class Csv
{
    public static string[] SplitLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            else
            {
                if (ch == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else if (ch == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    current.Append(ch);
                }
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}

static class Markdown
{
    public static List<string> SplitRow(string row)
    {
        // | col1 | col2 |
        var trimmed = row.Trim();
        if (trimmed.StartsWith("|", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith("|", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1];
        }

        return trimmed
            .Split('|')
            .Select(cell => cell.Trim())
            .ToList();
    }
}

static class MeasurementParser
{
    public static double ParseTimeToNanoseconds(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return double.NaN;
        }

        value = value.Trim();
        value = value.Replace(",", "", StringComparison.Ordinal);

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return double.NaN;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return double.NaN;
        }

        if (parts.Length == 1)
        {
            return number;
        }

        var unit = parts[1];
        return unit switch
        {
            "ns" => number,
            "us" => number * 1_000.0,
            "µs" => number * 1_000.0,
            "ms" => number * 1_000_000.0,
            "s" => number * 1_000_000_000.0,
            _ => number,
        };
    }

    public static long ParseBytes(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-" || value.Equals("NA", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        value = value.Trim();
        value = value.Replace(",", "", StringComparison.Ordinal);

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return 0;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return 0;
        }

        if (parts.Length == 1)
        {
            return (long)Math.Round(number);
        }

        return parts[1] switch
        {
            "B" => (long)Math.Round(number),
            "KB" => (long)Math.Round(number * 1024.0),
            "MB" => (long)Math.Round(number * 1024.0 * 1024.0),
            "GB" => (long)Math.Round(number * 1024.0 * 1024.0 * 1024.0),
            _ => (long)Math.Round(number),
        };
    }
}

sealed record Summary(
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    MachineInfo Machine,
    SourceInfo Source,
    IReadOnlyList<ScenarioSummary> Scenarios)
{
    public static Summary FromResults(Options options, BenchmarkResults results)
    {
        var scenarios = ScenarioSummary.Build(results.Rows);

        return new Summary(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Machine: new MachineInfo(options.Machine, options.Os, options.Arch, options.Runtime),
            Source: new SourceInfo(results.RunTimestampUtc, results.Sources),
            Scenarios: scenarios);
    }
}

sealed record MachineInfo(string Name, string Os, string Arch, string Runtime);

sealed record SourceInfo(DateTimeOffset RunTimestampUtc, IReadOnlyList<SourceReport> Reports);

sealed record MeasurementSummary(double MeanNs, long AllocatedBytes);

sealed record ScenarioSummary(
    string Label,
    MeasurementSummary? Liaison,
    MeasurementSummary? MediatR,
    double? Speedup,
    double? AllocReductionPct)
{
    public static IReadOnlyList<ScenarioSummary> Build(IEnumerable<BenchmarkRow> rows)
    {
        var byScenario = new Dictionary<string, Dictionary<Provider, BenchmarkRow>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!byScenario.TryGetValue(row.ScenarioKey, out var providers))
            {
                providers = new Dictionary<Provider, BenchmarkRow>();
                byScenario[row.ScenarioKey] = providers;
            }

            providers[row.Provider] = row;
        }

        var scenarios = new List<ScenarioSummary>();
        foreach (var (label, providers) in byScenario.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
        {
            providers.TryGetValue(Provider.Liaison, out var liaison);
            providers.TryGetValue(Provider.MediatR, out var mediatR);

            MeasurementSummary? liaisonMeasurement = liaison is null
                ? null
                : new MeasurementSummary(liaison.MeanNs, liaison.AllocatedBytes);

            MeasurementSummary? mediatRMeasurement = mediatR is null
                ? null
                : new MeasurementSummary(mediatR.MeanNs, mediatR.AllocatedBytes);

            double? speedup = null;
            double? allocReductionPct = null;

            if (liaisonMeasurement is not null && mediatRMeasurement is not null &&
                liaisonMeasurement.MeanNs > 0 &&
                mediatRMeasurement.MeanNs > 0)
            {
                speedup = mediatRMeasurement.MeanNs / liaisonMeasurement.MeanNs;
            }

            if (liaisonMeasurement is not null && mediatRMeasurement is not null &&
                mediatRMeasurement.AllocatedBytes > 0)
            {
                allocReductionPct = 100.0 * (1.0 - ((double)liaisonMeasurement.AllocatedBytes / mediatRMeasurement.AllocatedBytes));
            }

            scenarios.Add(new ScenarioSummary(label, liaisonMeasurement, mediatRMeasurement, speedup, allocReductionPct));
        }

        return scenarios;
    }
}

static class SummaryWriter
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static void Write(Summary summary, string outDir)
    {
        Directory.CreateDirectory(outDir);

        var jsonPath = Path.Combine(outDir, "latest.summary.json");
        var mdPath = Path.Combine(outDir, "latest.summary.md");

        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        File.WriteAllText(jsonPath, json + Environment.NewLine, Utf8NoBom);
        File.WriteAllText(mdPath, ToMarkdown(summary), Utf8NoBom);
    }

    private static string ToMarkdown(Summary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Benchmarks summary ({summary.Machine.Name})");
        sb.AppendLine();
        sb.AppendLine($"- OS: {summary.Machine.Os}");
        sb.AppendLine($"- Arch: {summary.Machine.Arch}");
        sb.AppendLine($"- Runtime: {summary.Machine.Runtime}");
        var sourceFormats = string.Join(
            ", ",
            summary.Source.Reports
                .Select(report => report.Format)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        sb.AppendLine($"- Source formats: {sourceFormats}");
        sb.AppendLine($"- Source files: {summary.Source.Reports.Count}");
        sb.AppendLine($"- Run timestamp (UTC): {summary.Source.RunTimestampUtc:O}");
        sb.AppendLine();

        sb.AppendLine("| Scenario | Liaison (ns) | MediatR (ns) | Speedup | Liaison (B/op) | MediatR (B/op) | Alloc reduction |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");

        foreach (var scenario in summary.Scenarios)
        {
            var liaisonMean = scenario.Liaison?.MeanNs.ToString("0.##", CultureInfo.InvariantCulture) ?? "N/A";
            var mediatRMean = scenario.MediatR?.MeanNs.ToString("0.##", CultureInfo.InvariantCulture) ?? "N/A";
            var speedup = scenario.Speedup is null ? "N/A" : $"{scenario.Speedup.Value:0.##}x";
            var liaisonAlloc = scenario.Liaison?.AllocatedBytes.ToString(CultureInfo.InvariantCulture) ?? "N/A";
            var mediatRAlloc = scenario.MediatR?.AllocatedBytes.ToString(CultureInfo.InvariantCulture) ?? "N/A";
            var allocReduction = scenario.AllocReductionPct is null ? "N/A" : $"{scenario.AllocReductionPct.Value:0.#}%";

            sb.AppendLine($"| {Escape(scenario.Label)} | {liaisonMean} | {mediatRMean} | {speedup} | {liaisonAlloc} | {mediatRAlloc} | {allocReduction} |");
        }

        return sb.ToString();
    }

    private static string Escape(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal);
}
