// Exhibit #0085 - the fix: give null its own arm.
//
// null matches the constant pattern `null`, never a type pattern, so classify it
// explicitly before the typed arms. The rest is identical to Bad.cs.

object?[] record = { "Ada Lovelace", 42, null }; // name, age, middle name (absent)

string Format(object? value) => value switch
{
    null => "",           // an absent field renders as an empty cell
    string s => s,
    int n => n.ToString(),
    _ => "<unsupported>",
};

Console.WriteLine("Exported row:");
foreach (var field in record)
    Console.WriteLine($"  [{Format(field)}]");

string rendered = Format(null);
if (rendered != "")
    throw new InvalidOperationException(
        $"a null field rendered as '{rendered}'");

Console.WriteLine("Null fields render blank. As it should be.");
