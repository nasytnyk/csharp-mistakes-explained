// Exhibit #0042: the fix

// The same object-typed store - but the setter compares by value with
// object.Equals instead of by reference with !=, so equal boxes count as equal.

object? current = null;
int changes = 0;

void Set(object value)
{
    if (!Equals(current, value)) // static object.Equals: null-safe value comparison
    {
        current = value;
        changes++;
        Console.WriteLine($"  value changed to {value}.");
    }
}

Console.WriteLine("Setting the volume to 5, three times:");
Set(5); // null -> 5: a real change
Set(5); // 5 -> 5: not a change
Set(5); // 5 -> 5: not a change

Console.WriteLine($"Recorded {changes} change(s) for one real change.");

if (changes != 1)
{
    throw new InvalidOperationException(
        $"recorded {changes} changes setting the same value");
}

Console.WriteLine("Only the real change was recorded. As it should be.");
