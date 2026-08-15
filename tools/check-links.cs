// Checks exhibit READMEs: bare #NNNN references that should be links, and links that lead nowhere.
// Run from the repo root: dotnet run tools/check-links.cs

using System.Text.RegularExpressions;

if (!Directory.Exists("src"))
{
    Console.Error.WriteLine("Run from the repo root: dotnet run tools/check-links.cs");
    return 1;
}

// The exhibit map: "0010" -> "events/0010-immortal-subscriber"
var exhibits = new Dictionary<string, string>();
foreach (var hall in Directory.GetDirectories("src"))
{
    foreach (var exhibit in Directory.GetDirectories(hall))
    {
        var slug = Path.GetFileName(exhibit);
        if (Regex.IsMatch(slug, @"^\d{4}-"))
            exhibits[slug[..4]] = $"{Path.GetFileName(hall)}/{slug}";
    }
}

var problems = new List<string>();

foreach (var readme in Directory.GetFiles("src", "README*.md", SearchOption.AllDirectories))
{
    var folder = Path.GetDirectoryName(readme)!;
    var lines = File.ReadAllLines(readme);

    for (int i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        var where = $"{readme}:{i + 1}";

        // A bare #NNNN naming a real exhibit should be a clickable link instead.
        // The exhibit's own H1 title keeps the bare form.
        if (!line.StartsWith("# #"))
        {
            foreach (Match match in Regex.Matches(line, @"#(\d{4})"))
            {
                if (!exhibits.TryGetValue(match.Groups[1].Value, out var path))
                    continue; // order numbers, prices, anything that is not an exhibit

                var slug = path.Split('/')[1];
                problems.Add($"{where}: bare {match.Value} - write [{slug}](../../{path}/)");
            }
        }

        // Every relative link must lead somewhere real.
        foreach (Match match in Regex.Matches(line, @"\]\(([^)]+)\)"))
        {
            var target = match.Groups[1].Value;
            if (target.StartsWith("http"))
                continue;

            var resolved = Path.GetFullPath(Path.Combine(folder, target));
            if (!File.Exists(resolved) && !Directory.Exists(resolved))
                problems.Add($"{where}: link leads nowhere -> {target}");
        }
    }
}

if (problems.Count > 0)
{
    Console.Error.WriteLine($"❌ {problems.Count} problem(s) found:");
    foreach (var problem in problems)
        Console.Error.WriteLine($"  {problem}");
    return 1;
}

Console.WriteLine($"Exhibits: {exhibits.Count}. Every cross-reference is a link, every link resolves.");
return 0;
