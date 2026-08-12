---
id: "0058"
title: the override that wasn't
category: inheritance
tags: [inheritance, polymorphism, new-keyword]
rule: "`new` **hides**, `override` replaces - a base-typed reference runs the hidden base member, not yours"
---

# #0058 - The Override That Wasn't

## 💥 Symptom

A derived class "overrides" a method, the unit test that news up the derived type and calls it
directly passes, and yet in production the base behavior runs. A credit-card fee is never
charged, a discount never applies, a subclass's validation never fires - but only when the
object is handled as its base type: in a `List<Base>`, as a base-typed parameter, through a
framework callback. Call the same object through a derived reference and it behaves correctly,
so the bug looks like it isn't there.

## 🔍 The Offending Code

```csharp
class PaymentMethod
{
    public decimal Fee() => 0m;                  // base: no fee
}

class CreditCard : PaymentMethod
{
    public new decimal Fee() => Amount * 0.03m;  // 💥 `new` HIDES Fee - it does not override it
}

decimal total = cart.Sum(m => m.Fee());          // cart is List<PaymentMethod> -> base Fee() every time
```

## 🧠 What's Actually Going On

`new` and `override` look interchangeable - both compile, both let a derived class declare a
method the base already has - but they do opposite things. `override` *replaces* the base
member: one method, dispatched at runtime by the object's actual type, so every caller gets the
derived behavior. `new` *hides* it: two independent methods that happen to share a name,
selected by the variable's **compile-time** type. `card.Fee()` runs the derived one because
`card` is typed `CreditCard`; `((PaymentMethod)card).Fee()` runs the base one - the *same
object*, two answers, chosen by the reference, not the object.

That is exactly backwards from what polymorphism promises, and the failure lands precisely
where objects travel as their base type - which is everywhere a framework touches them:
`List<PaymentMethod>`, an `IEnumerable<Shape>`, a base-typed method parameter, a virtual
callback. The broken belief is "if it compiled as a redefinition, it's an override." It
compiled because `new` is a legal, deliberate feature - and the only hint you ignored was
warning **CS0108**, which the IDE's quick-fix silences by inserting the very `new` that seals
the bug.

## ✅ The Fix

To get one method dispatched by the object's real type, make the base member `virtual` (or
`abstract`) and the derived one `override`:

```csharp
class PaymentMethod
{
    public virtual decimal Fee() => 0m;               // virtual: overridable
}

class CreditCard : PaymentMethod
{
    public override decimal Fee() => Amount * 0.03m;  // override: dispatched by the runtime type
}
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Situation | What to do |
|---|---|
| You want subclasses to change behavior | `virtual` on the base, `override` on the derived - the only combination that survives base-typed callers. |
| The base method isn't yours to make `virtual` | You cannot truly override it. Compose (wrap the base type) or use an interface - do not reach for `new` to fake it. |
| CS0108 warning appeared after a library update | The base class just grew a member yours already had. Rename yours, or make it a real `override` if the base member is now `virtual` - do not blindly accept the `new` quick-fix. |
| You genuinely want a separate, statically-bound member | `new` is legal and intentional - but then never treat it as an override, and never let the type travel polymorphically expecting your version. |

## 😈 The Even Worse Sibling

Do it to a **property** and you don't just lose dispatch - you split the object's state in two.
A `new` auto-property gets its **own backing field**, so the base and derived `Name` are two
storage slots living in one object:

```csharp
class Base    { public string Name { get; set; } }
class Derived : Base { public new string Name { get; set; } }

var d = new Derived();
((Base)d).Name = "from base ref";
d.Name          = "from derived ref";
// ((Base)d).Name == "from base ref"   AND   d.Name == "from derived ref"  - both alive, same object
```

A write through one reference is invisible through the other, and nothing throws. Worse still,
`System.Text.Json` serializes whichever `Name` the **static** type declares:
`JsonSerializer.Serialize(d)` emits the derived value, `JsonSerializer.Serialize<Base>(d)` emits
the base one - *different JSON from the same object*, so what you persisted depends on the
declared type at the call site, not on the data. One property name, two values, silently chosen
by whoever holds the reference.

## 🎓 Advanced Nuance

- **Version drift plants it with nobody typing `new`.** A base class from a NuGet update grows
  a member your derived class already had; CS0108 is only a *warning*, the build still
  succeeds, and from that compile on, one name means two things in every instance of your type.
- **`new` binds at compile time, `virtual`/`override` at runtime.** Method hiding is resolved
  from the static type of the expression; virtual dispatch from the runtime type of the object.
  Every "why does it work in this call but not that one" trace comes back to which type the
  *variable* had, never which type the *object* had.
- **`base.Method()` and `sealed` are the honest tools.** If you truly want to extend rather than
  replace, `override` and call `base.Method()`; if you want to forbid further overriding,
  `sealed override`. `new` says neither "extend" nor "replace" - it says "a different method
  that happens to share this name," which is almost never what a subclass author means.
- Same family as [0034-virtual-call-in-constructor](../0034-virtual-call-in-constructor/): both
  are cases where *which code runs* is decided by a subtlety of dispatch, not by the line you're
  reading.

## 🔎 How to Find It in Your Codebase

- Grep for the `new` modifier on methods and properties: `public new `, `protected new `,
  `internal new `. Every one is a member deliberately hidden - confirm each is intentional, not
  a silenced CS0108.
- Turn CS0108 into an error (`<WarningsAsErrors>CS0108</WarningsAsErrors>` or treat-warnings-as-
  errors): the compiler already knows where every accidental hide is; make it stop the build.
- Symptom-side: behavior that is correct through the concrete type but wrong through a
  `List<Base>`, an interface, or a framework callback; a subclass whose "override" never fires
  in production but passes a direct unit test.
- For properties, watch for a value written in one place and read as stale somewhere else on the
  *same object* - two backing fields behind one name is the shape, and serialization that
  changes with the declared type confirms it.
