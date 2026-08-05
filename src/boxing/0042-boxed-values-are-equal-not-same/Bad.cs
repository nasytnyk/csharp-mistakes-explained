// Exhibit #0042: comparing boxed values with ==

// A volume setting kept in an object-typed store (a settings bag, view-state, a
// cache entry). The setter raises "changed" only when the value actually changes
// - or so it looks. The field is object, so != compares boxes by reference.

object? current = null;
int changes = 0;

void Set(object value)
{
    if (current != value) // 💥 object != is reference inequality; two boxes of 5 are never ==
    {
        current = value;
        changes++;
        Console.WriteLine($"  value changed to {value}.");
    }
}

Console.WriteLine("Setting the volume to 5, three times:");
Set(5); // null -> 5: a real change
Set(5); // 5 -> 5: not a change... right?
Set(5); // 5 -> 5: not a change... right?

Console.WriteLine($"Recorded {changes} change(s) for one real change.");

if (changes != 1)
{
    throw new InvalidOperationException(
        $"recorded {changes} changes setting the same value - each boxed 5 is a distinct object, so != fires every time");
}

Console.WriteLine("Only the real change was recorded.");
