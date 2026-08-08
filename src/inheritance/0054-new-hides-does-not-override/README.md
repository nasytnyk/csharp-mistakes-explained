---
id: "0054"
title: new hides, it does not override
category: inheritance
tags: [inheritance, method-hiding, polymorphism]
rule: "never `new`-hide a method to change behavior - the call binds to the **static type**, not the object"
---

# #0054 - `new` Hides, It Does Not Override

## 💥 Symptom

Premium accounts are being billed the free-tier fee. The premium class clearly defines
its own, higher `MonthlyFee()` - you can read it right there - and calling it on a
`PremiumAccount` variable returns the right number. But the billing loop holds accounts
by their base type, and through that reference every premium account quietly charges the
base fee. Revenue leaks, silently, while every unit test that calls the method on the
concrete type passes.

## 🔍 The Offending Code

```csharp
class BasicAccount                  { public decimal MonthlyFee() => 5m; }
class PremiumAccount : BasicAccount { public new decimal MonthlyFee() => 20m; } // `new`, not override

BasicAccount account = new PremiumAccount();
account.MonthlyFee(); // 💥 5, not 20 - the base method, chosen by the variable's type
```

## 🧠 What's Actually Going On

`new` and `override` look interchangeable and mean opposite things. `override` joins
**virtual dispatch**: the method that runs is chosen by the object's *runtime* type, so a
`PremiumAccount` behind a `BasicAccount` reference still runs the premium method. `new`
does the reverse - it **hides** the base member, declaring a brand-new, unrelated method
that merely shares a name. Which one runs is decided by the *static* type of the
reference: call through `PremiumAccount`, get the premium fee; call through `BasicAccount`,
get the base fee. Same object, two methods, and the compiler picks by the type you happen
to be holding it with.

The compiler warned you once - `CS0108: 'PremiumAccount.MonthlyFee()' hides inherited
member` - and the fix it suggests, adding the `new` keyword, *silences the warning by
confirming the bug*. The broken belief is "`new` is how you override when the base method
isn't virtual." It is not; it is how you declare a second method the base type will never
call.

## ✅ The Fix

Make the base method `virtual` and the derived one `override`, so dispatch follows the
object, not the reference:

```csharp
class BasicAccount                  { public virtual  decimal MonthlyFee() => 5m; }
class PremiumAccount : BasicAccount { public override decimal MonthlyFee() => 20m; }
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `virtual` + `override` | You want polymorphism - the derived behavior should win through *any* reference. The default for "specialize a base method." |
| `abstract` on the base | The base has no sensible default and every subclass must supply one - the compiler then enforces the override. |
| Keep `new`, deliberately and documented | Genuinely rare: the derived member is a different operation that only makes sense on the derived type and is never called polymorphically. |
| Composition over inheritance | If you are hiding to "replace" behavior, an interface plus delegation expresses "different implementation" without the static-vs-runtime trap. |

## 😈 The Even Worse Sibling

Do it with a `new` **property** and the object literally holds two values for one name.
`PremiumAccount` gets its own `Total`; `BasicAccount`'s hidden `Total` still exists on the
same instance, with its own backing field - a write through one reference lands in a field
the other reference can't see. Then hand that object to a serializer: `System.Text.Json`
emits properties of the *static* type it is given, so `JsonSerializer.Serialize<BasicAccount>(premium)`
writes the base value while `Serialize(premium)` (compile-time `PremiumAccount`) writes the
derived one. **The same object serializes to two different JSON documents**, and which you
get depends on how a method three layers up happened to type its parameter - no exception
anywhere. The billing crash in this exhibit is the *lucky* outcome; the property version
ships two truths and never throws.

## 🎓 Advanced Nuance

- **Base-typed collections are where it always bites.** `List<BasicAccount>`,
  `IEnumerable<Shape>`, a framework callback taking a base type - the instant objects flow
  through a base-typed reference (which is the entire point of inheritance), a `new` member
  reverts to the base. Code that only ever touches the concrete type never sees it, which
  is exactly why tests miss it.
- **Version drift plants it with nobody typing `new`.** A base class from a library update
  gains a member your derived class already had; `CS0108` is only a warning, and from that
  build on one name means two things in every instance. Treat every "hides inherited
  member" warning as a bug or an undocumented deliberate hide - there is no third case.
- **Same runtime-vs-declared surprise as [0034-virtual-call-in-constructor](../../inheritance/0034-virtual-call-in-constructor/).**
  There, a `virtual` call during construction dispatches to an override before the derived
  constructor has run; here, a `new` member refuses to dispatch at all. Both are "which
  method actually runs" defying the plain reading of the code.

## 🔎 How to Find It in Your Codebase

- Stop ignoring `CS0108`, or promote it to an error: every "hides inherited member" warning
  is either a real bug or a hide that needs a comment justifying it.
- Grep for `public new ` / `internal new ` / `protected new ` on methods and properties.
  Each is a member that will not answer to a base-typed reference.
- Flag any `new` member on a type used polymorphically - stored in a `List<Base>`, passed
  as a base parameter, serialized, or resolved from DI behind an interface. That
  combination is the trap.
- In review: whenever a subclass "overrides" a method that is not `virtual` / `abstract` in
  the base, the compiler quietly made it a hide. If the intent was polymorphism, the base
  signature is the thing to change.
