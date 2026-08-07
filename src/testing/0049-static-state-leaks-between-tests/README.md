---
id: "0049"
title: Static state leaking between tests
category: testing
tags: [testing, static-state, test-isolation]
rule: "never let a test leave **static state** behind - reset it in teardown"
---

# #0049 - Static State Leaking Between Tests

## 💥 Symptom

A suite that has been green for months goes red, and the failing test is not the
one anyone touched. It is a test that only reads a value and asserts a baseline - it
writes nothing, it did nothing wrong. Run it by itself and it passes. Run the full
suite and it fails, and only in a particular order. Someone renamed a class or added
an unrelated test, the test-execution order shuffled, and now a static that a
*different* test writes is still set when this one runs.

## 🔍 The Offending Code

```csharp
static void SurchargeApplies()          // test B
{
    PricingConfig.Surcharge = 5m;       // 💥 writes a static, never resets it
    Assert.Equal(5m, PricingConfig.Surcharge);
}

static void BaselineIsClean()           // test A - reads the leftover, and fails
{
    Assert.Equal(0m, PricingConfig.Surcharge);
}
```

## 🧠 What's Actually Going On

Static fields are process-wide, and a test runner executes many tests in **one
process**. A static write in one test outlives that test and becomes an input to
every test that runs afterward - unless something tears it down. Test isolation is a
promise the runner does not make for your static state: it runs your tests in a
shared process, and a mutable static is shared mutable state, so a test that reads it
is a function not of its own code but of whatever ran before it.

That makes the result depend on **order**. Run A then B and both pass: A sees a clean
`0`, B sets `5` and checks `5`. Run B then A and A fails: B left `5` behind, A
expected `0`. The tests never changed; only the sequence did. And the failure lands
on the **innocent** test - A reads the value B wrote, so A goes red while B, the
actual culprit, stays green. The mental model that breaks is "each test starts from a
clean slate." It starts from whatever the last test left in the statics.

## ✅ The Fix

Reset shared static state in teardown - the setup/cleanup the framework runs around
every test - so nothing a test writes survives into the next:

```csharp
// xUnit: reset in the constructor or Dispose; NUnit: [TearDown]; MSTest: [TestCleanup]
public void Dispose() => PricingConfig.Surcharge = 0m;
```

Full version in [Good.cs](Good.cs) - the runner resets the static in a `finally`
after each test, and both orders pass. Options, weakest reliance last:

| Approach | When it's the right call |
|---|---|
| Don't hold test-touched state in a mutable static - inject it, use a fresh instance per test | The real fix - a mutable static is a global; make it not shared and the whole class of bug disappears |
| Reset the static in teardown (`Dispose`/`[TearDown]`/`[TestCleanup]`) | You must keep the static - restore it to a known baseline around every test |
| A fixture that owns and resets the shared state | The state is legitimately shared infrastructure (a cache) - wrap it so reset is automatic |
| Disable parallelism for the affected tests | A stopgap that hides the order dependence; it does not remove it |

## 😈 The Even Worse Sibling

The trigger touches zero logic. Rename a class, add an unrelated test, upgrade the
runner - any of which reshuffles execution order - and the build breaks. `git bisect`
and `git blame` pin it on the rename commit, which is exactly where it started failing
and completely wrong about why, so whoever did the rename loses an afternoon to a leak
they never wrote. Then the "fix" is often to delete or quarantine the failing test A -
which removes the innocent test and leaves the leaking test B in place, so the leak
survives its own cleanup. And on a parallel-by-default runner it is worse still: the
same leak wears a nondeterministic schedule and reads as pure flakiness, gets a
`[Retry]`, gets marked flaky, and is never fixed.

## 🎓 Advanced Nuance

The statics you leak are rarely a field named `Surcharge`. They are a static
`CultureInfo` a test set, an `Environment` variable it exported, a service-locator
singleton it swapped, a cached `JsonSerializerOptions` it mutated, a static `Random`
whose sequence it advanced, an `AsyncLocal` default, a static event it subscribed to
([0010-immortal-subscriber](../../events/0010-immortal-subscriber/) in a suite). Any
process-global a test writes is a leak, and most are invisible at the assertion.

The reason it surprises people is that xUnit constructs a **fresh instance** of the
test class for every test, which resets *instance* fields - training the habit "fields
reset between tests." Statics are not instance fields; they never reset, and that gap
is exactly where this lives. It is the execution-order cousin of
[0048-collection-assert-is-ordered](../../testing/0048-collection-assert-is-ordered/):
there the assertion depends on element order, here the test depends on run order, and
both are a dependency on something the requirement never had.

## 🔎 How to Find It in Your Codebase

- Grep for `static` fields and properties that are **assigned** (not `readonly` /
  `const`) and reachable from tests - config knobs, `Current`/`Instance` singletons,
  `Default` options, caches. Every assignment to one inside a test owes a teardown.
- Flush them out by running the suite in a **different order**: reverse it, randomize
  it (xUnit's `ITestCaseOrderer` / assembly randomization), and run each test in
  isolation. A test that passes alone but fails in the suite, or fails only in some
  orders, is this bug.
- No analyzer reliably catches it - static reachability into tests is too broad. Treat
  it as a review rule: a test that writes shared or static state must reset it, and a
  test that reads a baseline must not assume one it did not set.
- When a rename or an unrelated addition "breaks" a far-away test, suspect order, not
  the diff - the commit that reddened it is almost never the one that caused it.
