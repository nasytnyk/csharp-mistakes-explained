// Exhibit #0049: the fix

// The same runner and tests - but now the runner resets the shared static after
// every test (teardown), so no test inherits what another left behind.

Console.WriteLine("Run order A, B:");
bool ab = RunSuite([("A: baseline is clean", TestBaselineClean), ("B: surcharge applies", TestSurchargeApplies)]);

PricingConfig.Surcharge = 0m; // (redundant now - teardown already left it clean)

Console.WriteLine("Run order B, A:");
bool ba = RunSuite([("B: surcharge applies", TestSurchargeApplies), ("A: baseline is clean", TestBaselineClean)]);

Console.WriteLine();
Console.WriteLine($"All green in order A,B: {ab}.  All green in order B,A: {ba}.");

if (ab != ba)
{
    throw new InvalidOperationException("test outcomes still depend on order");
}

Console.WriteLine("Both orders agree - order can't change the result. As it should be.");

// The tests (unchanged).
static void TestBaselineClean()
{
    if (PricingConfig.Surcharge != 0m)
        throw new Exception($"expected a clean baseline of 0, found {PricingConfig.Surcharge} left over");
}

static void TestSurchargeApplies()
{
    PricingConfig.Surcharge = 5m;
    if (PricingConfig.Surcharge != 5m)
        throw new Exception($"expected 5, got {PricingConfig.Surcharge}");
}

// A minimal runner that tears down shared state after each test.
static bool RunSuite((string Name, Action Test)[] tests)
{
    bool allPassed = true;
    foreach (var (name, test) in tests)
    {
        try { test(); Console.WriteLine($"  PASS {name}"); }
        catch (Exception e) { Console.WriteLine($"  FAIL {name}: {e.Message}"); allPassed = false; }
        finally { PricingConfig.Surcharge = 0m; } // teardown: restore the shared baseline
    }
    return allPassed;
}

static class PricingConfig
{
    public static decimal Surcharge; // process-wide shared state
}
