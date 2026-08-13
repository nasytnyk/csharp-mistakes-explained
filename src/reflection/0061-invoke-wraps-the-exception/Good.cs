// Exhibit #0061: the fix

#:property PublishAot=false

using System.Reflection;

// A command dispatcher invokes the handler by reflection and translates a domain
// ValidationException into a clean "rejected" result (think: a 400, not a 500).
var handler = new OrderHandler();
var method = typeof(OrderHandler).GetMethod(nameof(OrderHandler.Place))!;

string result;
try
{
    // DoNotWrapExceptions: the handler's exception propagates with its real type, not wrapped
    method.Invoke(handler, BindingFlags.DoNotWrapExceptions, binder: null, new object[] { -5 }, culture: null);
    result = "placed";
}
catch (ValidationException ex) // now runs - the ValidationException reaches us unwrapped
{
    result = $"rejected: {ex.Message}";
}
catch (Exception ex)
{
    result = $"crashed: {ex.GetType().Name}";
}

Console.WriteLine($"Result: {result}");

// Self-audit: a -5 quantity must come back as a clean rejection, not an unhandled crash.
if (result != "rejected: quantity must be positive")
{
    throw new InvalidOperationException(
        $"expected a rejection, got '{result}'");
}

Console.WriteLine("Validation error handled cleanly. As it should be.");

class OrderHandler
{
    public void Place(int quantity)
    {
        if (quantity <= 0)
            throw new ValidationException("quantity must be positive");
    }
}

class ValidationException(string message) : Exception(message);
