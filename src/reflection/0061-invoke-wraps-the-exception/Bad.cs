// Exhibit #0061: MethodInfo.Invoke wraps your exception

#:property PublishAot=false

using System.Reflection;

// A command dispatcher invokes the handler by reflection and translates a domain
// ValidationException into a clean "rejected" result (think: a 400, not a 500).
var handler = new OrderHandler();
var method = typeof(OrderHandler).GetMethod(nameof(OrderHandler.Place))!;

string result;
try
{
    method.Invoke(handler, new object[] { -5 }); // quantity -5: the handler throws ValidationException
    result = "placed";
}
catch (ValidationException ex) // 💥 never runs - Invoke wrapped it in TargetInvocationException
{
    result = $"rejected: {ex.Message}";
}
catch (Exception ex) // the wrapper lands here instead, and the domain error looks like a 500
{
    result = $"crashed: {ex.GetType().Name}";
}

Console.WriteLine($"Result: {result}");

// Self-audit: a -5 quantity must come back as a clean rejection, not an unhandled crash.
if (result != "rejected: quantity must be positive")
{
    throw new InvalidOperationException(
        $"expected a rejection, got '{result}' - MethodInfo.Invoke wraps the handler's ValidationException in " +
        "TargetInvocationException, so catch (ValidationException) is skipped and the domain error escapes as a crash");
}

Console.WriteLine("Validation error handled cleanly.");

class OrderHandler
{
    public void Place(int quantity)
    {
        if (quantity <= 0)
            throw new ValidationException("quantity must be positive");
    }
}

class ValidationException(string message) : Exception(message);
