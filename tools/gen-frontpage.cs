// Regenerates the exhibit list on the front-page README from the hall registry
// and each exhibit's front-matter. Run from the repo root:
//   dotnet run tools/gen-frontpage.cs
// The generated block lives between the EXHIBITS:START / EXHIBITS:END markers.

using System.Text;
using System.Text.RegularExpressions;

if (!Directory.Exists("src") || !File.Exists(".claude/memory/halls.md"))
{
    Console.Error.WriteLine("Run from the repo root: dotnet run tools/gen-frontpage.cs");
    return 1;
}

// 1. Hall registry order: (slug, emoji, display name), skipping headers/separators.
var halls = new List<(string Slug, string Emoji, string Name)>();
foreach (var line in File.ReadAllLines(".claude/memory/halls.md"))
{
    if (Regex.IsMatch(line, @"^[\s|:-]+$")) continue; // separator row
    var m = Regex.Match(line, @"^\|\s*([a-z][a-z-]*)\s*\|\s*(\S+)\s*\|\s*([^|]+?)\s*\|");
    if (m.Success && m.Groups[1].Value != "slug")
        halls.Add((m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value));
}

// 2. Exhibits: front-matter of every src/<hall>/<NNNN-slug>/README.md.
//    Canonical readme per exhibit is README.md or README-en.md; translations
//    (README-<lang>.md, e.g. README-ua.md) are skipped so each exhibit lists once.
var exhibits = new List<(string Id, string Category, string Rule, string Folder, string Author)>();
foreach (var path in Directory.GetFiles("src", "README*.md", SearchOption.AllDirectories)
    .Where(p => Path.GetFileName(p) is "README.md" or "README-en.md"))
{
    var text = File.ReadAllText(path);
    var fm = Regex.Match(text, @"\A---\r?\n(.*?)\r?\n---", RegexOptions.Singleline);
    if (!fm.Success) continue;
    var block = fm.Groups[1].Value;

    string Field(string key)
    {
        var m = Regex.Match(block, $@"^{key}:\s*(.+)$", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value.Trim().Trim('"') : "";
    }

    var folder = Path.GetFileName(Path.GetDirectoryName(path)!);
    exhibits.Add((Field("id"), Field("category"), Field("rule"), folder, Field("author")));
}

// 3. Build the generated block.
var body = new StringBuilder();

var opened = halls.Where(h => exhibits.Any(e => e.Category == h.Slug)).ToList();
var latest = exhibits.Max(e => int.Parse(e.Id));
body.AppendLine($"> **{exhibits.Count}** exhibits in **{opened.Count}** halls, latest - **#{latest:D4}**.");
body.AppendLine();

foreach (var hall in opened)
{
    body.AppendLine($"### {hall.Emoji} {hall.Name}");
    body.AppendLine();
    foreach (var e in exhibits.Where(e => e.Category == hall.Slug).OrderBy(e => e.Id))
    {
        // Contributed exhibits carry an author; the curator's own stay uncredited.
        var credit = string.IsNullOrWhiteSpace(e.Author) ? "" : $" (@{e.Author})";
        body.AppendLine($"- [{e.Id}](src/{e.Category}/{e.Folder}/) {e.Rule}{credit}");
    }
    body.AppendLine();
}

var planned = halls.Where(h => exhibits.All(e => e.Category != h.Slug)).ToList();
if (planned.Count > 0)
{
    body.AppendLine("## To Be Continued");
    body.AppendLine();
    body.AppendLine(string.Join(" · ", planned.Select(h => $"{h.Emoji} {h.Name}")));
}

// 4. Splice the block between the markers in README.md.
var readmePath = "README.md";
var readme = File.ReadAllText(readmePath);
var startTag = "<!-- EXHIBITS:START";
var endTag = "<!-- EXHIBITS:END -->";
var si = readme.IndexOf(startTag, StringComparison.Ordinal);
var ei = readme.IndexOf(endTag, StringComparison.Ordinal);
if (si < 0 || ei < 0)
{
    Console.Error.WriteLine("README.md is missing the EXHIBITS:START / EXHIBITS:END markers.");
    return 1;
}
var afterStart = readme.IndexOf("-->", si, StringComparison.Ordinal) + 3;

var rebuilt = readme[..afterStart] + "\n\n" + body.ToString().TrimEnd() + "\n\n" + readme[ei..];
File.WriteAllText(readmePath, rebuilt);

Console.WriteLine($"Front page rebuilt: {exhibits.Count} exhibits, {opened.Count} halls, {planned.Count} planned.");
return 0;
