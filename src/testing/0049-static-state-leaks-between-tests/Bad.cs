// Exhibit #0049: static state leaking between tests

// A tiny test runner. Two tests share a static config knob. One test writes it
// and never cleans up, so the other test's result depends on who ran first.

Console.WriteLine("Run order A, B:");
bool ab = RunSuite([("A: baseline is clean", TestBaselineClean), ("B: surcharge applies", TestSurchargeApplies)]);

// Between our two demo runs, reset to the state a fresh process would start from.
// The runner does NOT do this between tests - that is the whole bug.
PricingConfig.Surcharge = 0m;

Console.WriteLine("Run order B, A:");
bool ba = RunSuite([("B: surcharge applies", TestSurchargeApplies), ("A: baseline is clean", TestBaselineClean)]);

Console.WriteLine();
Console.WriteLine($"All green in order A,B: {ab}.  All green in order B,A: {ba}.");

if (ab != ba)
{
    throw new InvalidOperationException(
        "the same two tests pass in one order and fail in the other - B's static write leaked into A, and the failure landed on innocent A");
}

Console.WriteLine("Both orders agree.");

// The tests.
static void TestBaselineClean()
{
    if (PricingConfig.Surcharge != 0m)
        throw new Exception($"expected a clean baseline of 0, found {PricingConfig.Surcharge} left over");
}

static void TestSurchargeApplies()
{
    PricingConfig.Surcharge = 5m; // 💥 writes a static and never resets it
    if (PricingConfig.Surcharge != 5m)
        throw new Exception($"expected 5, got {PricingConfig.Surcharge}");
}

// A minimal runner: run each test, print PASS/FAIL, report whether all passed.
static bool RunSuite((string Name, Action Test)[] tests)
{
    bool allPassed = true;
    foreach (var (name, test) in tests)
    {
        try { test(); Console.WriteLine($"  PASS {name}"); }
        catch (Exception e) { Console.WriteLine($"  FAIL {name}: {e.Message}"); allPassed = false; }
    }
    return allPassed;
}

static class PricingConfig
{
    public static decimal Surcharge; // process-wide shared state
}
