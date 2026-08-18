// Exhibit #0085: a type pattern does not match null - the absent value takes the
// default arm.
//
// An export formats each field value (from a data reader, so the static type is
// object?). The `string s` arm handles text, the `int n` arm handles numbers,
// and the default arm flags a type the exporter does not support. A null field -
// an absent optional value - is really an empty string, but no typed arm matches
// it, so it lands in the "unsupported" default.

object?[] record = { "Ada Lovelace", 42, null }; // name, age, middle name (absent)

string Format(object? value) => value switch
{
    string s => s,
    int n => n.ToString(),
    _ => "<unsupported>", // 💥 null matches no type pattern and lands here
};

Console.WriteLine("Exported row:");
foreach (var field in record)
    Console.WriteLine($"  [{Format(field)}]");

// A null field is a missing string; it must render blank, not "<unsupported>".
string rendered = Format(null);
if (rendered != "")
    throw new InvalidOperationException(
        $"a null field rendered as '{rendered}': a type pattern like `string s` does not match null, so the absent " +
        "value fell through every typed arm into the default meant for foreign types - a missing field is exported " +
        "as an unsupported-type marker instead of an empty cell");

Console.WriteLine("Null fields render blank.");
