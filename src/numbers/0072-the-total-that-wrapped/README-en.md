---
id: "0072"
title: the total that wrapped
category: numbers
tags: [numbers, overflow, checked]
rule: "never assume int math throws on overflow - it **wraps**; use `checked`"
---

# #0072 - The Total That Wrapped

## 💥 Symptom

An invoice for a large order comes out far too small - a `$49,999.50` order billed as `$7,049.83`.
No error, no warning; the arithmetic quietly produced the wrong number and the system charged it.
The quantity and the price are both right, the multiplication is trivial, and yet the total is a
fraction of what it should be (or, on other inputs, negative). Money went missing between two
correct numbers and a `*`.

## 🔍 The Offending Code

```csharp
int totalCents = quantity * unitPriceCents; // 💥 50_000 * 99_999 = 4_999_950_000, overflows int, wraps to 704_982_704
```

## 🧠 What's Actually Going On

C# integer arithmetic runs in an **unchecked** context by default, and on overflow it silently
wraps instead of failing. `quantity * unitPriceCents` is computed in `int`; when the true product
(4,999,950,000) exceeds `int.MaxValue` (2,147,483,647), the result wraps modulo 2^32 down to
704,982,704 - a smaller, positive, entirely plausible-looking number. Both operands fit in an
`int`; their product does not, and nothing warns you, because wrapping is the language's defined
behavior here, not an error. The customer is billed `$7,049.83` for a `$49,999.50` order, and every
log line looks normal.

The broken belief is "if the math overflowed, I'd get an exception." You would in a `checked`
context; you do not by default. The default trades safety for speed - `int.MaxValue + 1` is
`int.MinValue` - so a total that outgrows `int` becomes a quietly wrong total, not a crash.

## ✅ The Fix

Make the arithmetic throw instead of wrap - compute money math in a `checked` context, so an
overflow surfaces as an `OverflowException` you can catch and refuse, instead of a wrong amount you
bill:

```csharp
try
{
    int totalCents = checked(quantity * unitPriceCents); // throws OverflowException on overflow
    Charge(totalCents);
}
catch (OverflowException)
{
    Reject("order total exceeds the supported range"); // never bill a wrapped number
}
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `checked(...)` around the arithmetic | Money and counts where a wrong number must never ship silently - overflow becomes a catchable `OverflowException` at the exact operation. |
| `<CheckForOverflowUnderflow>` for the project | You want *every* overflow to throw, not just the spots you remembered to wrap - the safest default for a billing / finance service. |
| Compute in `long` (or `decimal`) | The value legitimately needs the range - `(long)quantity * unitPriceCents` holds it; `decimal` for money you also round and display. Widening carries the number; `checked` guards the ones you keep in `int`. |
| Validate inputs against a limit | Reject an order whose `quantity * price` cannot fit before you compute it - fail with a business message, not an arithmetic one. |

## 😈 The Even Worse Sibling

The undercharge is loud in hindsight; the wrap that lands *negative* is worse in the moment.
`int.MaxValue + 1` is `int.MinValue`, so a total that overflows a little past the limit can come
out as a large negative number - a refund where you meant a charge, a balance that flips sign, a
quantity that reads as "owed to the customer." And overflow is data-dependent by nature: the
multiply is correct for every order in dev and test (small quantities, small prices) and wraps the
first time a real wholesale order crosses ~2.1 billion cents, which is exactly the order big enough
to matter. `checked` would have thrown on that first big order; unchecked, it shipped the wrong
number, and only reconciliation - days later - finds a total that no single line explains.

## 🎓 Advanced Nuance

- **`checked` / `unchecked` are lexical, not deep.** `checked(expr)` covers the operations *in that
  expression*, not the calls it makes; a multiply inside a method you call runs in that method's own
  (default `unchecked`) context. Wrap the arithmetic itself, or set the project-wide flag, not a
  distant caller.
- **Constant overflow is a compile error; runtime overflow is not.** `int x = int.MaxValue + 1;`
  (all constants) fails to compile; the identical overflow with variables wraps silently at run
  time. The compiler catches what it can see, and production supplies the rest.
- **Only `checked` integer ops throw; floating point never does.** `int` / `long` overflow throws
  under `checked`; `double` overflow produces `Infinity` and never throws regardless. If a total can
  be huge and fractional, `decimal` (which *does* throw on overflow) is the money type, not `double`.

## 🔎 How to Find It in Your Codebase

- Grep for `int` (and `short`) arithmetic on money, counts, or sizes that can grow - `* price`,
  `* quantity`, `+= amount`, `Sum()` over `int` - and ask whether the *result* can exceed ~2.1
  billion.
- Turn on `<CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>` in a finance / billing
  project (at least in Debug / CI) so silent wraps become failing tests, then fix or `unchecked` the
  spots that are intentional.
- Symptom-side: totals far smaller than expected, or negative, for large orders; reconciliation gaps
  that no single line item accounts for; a value correct for small inputs and wrong for big ones.
- Carry genuinely large values in `long` / `decimal`, and wrap the `int` arithmetic you keep in
  `checked` so an overflow throws at the source instead of billing a wrapped number.
