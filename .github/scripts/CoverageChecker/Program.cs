using System.Globalization;
using System.Xml;
using System.Xml.Linq;

const double minimumCoverage = 97;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: CoverageChecker <coverage.cobertura.xml>");
    return 2;
}

try
{
    var coveragePath = Path.GetFullPath(args[0]);
    var repositoryRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
    var document = XDocument.Load(coveragePath);
    var report = document.Root ?? throw new InvalidDataException("Cobertura report has no root element.");
    var valid = ReadLineCount(report, "lines-valid");
    var covered = ReadLineCount(report, "lines-covered");

    if (valid <= 0 || covered < 0 || covered > valid)
    {
        throw new InvalidDataException("Coverage report must contain measured application lines.");
    }

    var percentage = 100d * covered / valid;
    Console.WriteLine($"Application line coverage: {percentage:F2}% ({covered}/{valid} lines)");
    if (percentage < minimumCoverage)
    {
        throw new InvalidDataException($"Application line coverage is below the CI minimum of {minimumCoverage:0}%.");
    }

    NormalizeSources(report, repositoryRoot);
    document.Save(coveragePath, SaveOptions.DisableFormatting);
    return 0;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or XmlException or ArgumentException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

static int ReadLineCount(XElement report, string attributeName)
{
    var value = (string?)report.Attribute(attributeName);
    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
    {
        throw new InvalidDataException($"Coverage report is missing a valid '{attributeName}' total.");
    }

    return count;
}

static void NormalizeSources(XElement report, string repositoryRoot)
{
    var sources = report.Element("sources");
    var sourceRoots = sources?
        .Elements("source")
        .Select(source => source.Value.Trim())
        .Where(source => source.Length > 0)
        .Select(source => Path.GetFullPath(Path.Combine(repositoryRoot, source)))
        .ToArray() ?? [];

    foreach (var entry in report.Descendants("class"))
    {
        var filename = (string?)entry.Attribute("filename");
        if (string.IsNullOrWhiteSpace(filename))
        {
            throw new InvalidDataException("Coverage report contains a class with no filename.");
        }

        var candidates = Path.IsPathRooted(filename)
            ? new[] { Path.GetFullPath(filename) }
            : new[] { Path.GetFullPath(Path.Combine(repositoryRoot, filename)) }
                .Concat(sourceRoots.Select(sourceRoot => Path.GetFullPath(Path.Combine(sourceRoot, filename))));

        var resolved = candidates.FirstOrDefault(candidate =>
            File.Exists(candidate) && IsInsideRepository(repositoryRoot, candidate));

        if (resolved is null)
        {
            throw new InvalidDataException($"Coverage source does not exist inside the repository: {filename}");
        }

        entry.SetAttributeValue("filename", Path.GetRelativePath(repositoryRoot, resolved).Replace(Path.DirectorySeparatorChar, '/'));
    }

    if (sources is null)
    {
        sources = new XElement("sources");
        report.Add(sources);
    }
    else
    {
        sources.RemoveNodes();
    }

    sources.Add(new XElement("source", "."));
}

static bool IsInsideRepository(string repositoryRoot, string candidate)
{
    var relative = Path.GetRelativePath(repositoryRoot, candidate);
    return !Path.IsPathRooted(relative)
        && relative != ".."
        && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
