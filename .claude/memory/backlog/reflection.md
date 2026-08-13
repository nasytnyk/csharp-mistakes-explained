# 🪞 reflection

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### setvalue-into-the-void (A3)

- **Twist:** PropertyInfo.SetValue on a struct writes into the box reflection
  just created and throws it away - your variable never changes, and no API
  anywhere reports that the write went nowhere.
- **Mechanic:** SetValue takes `object`: passing a struct variable boxes a
  copy; the setter runs against the box; the box is discarded. Classes work
  fine through the same code path, so the mapper "works" until the first
  struct DTO. (The fix that keeps structs: box once explicitly, SetValue
  into that box, unbox at the end.)
- **Who hits it:** hand-rolled mappers, config binders, test data builders -
  reflective property-setting loops written for classes that one day meet a
  struct.
- **Repro:** struct with an auto-property; GetProperty + SetValue; the
  variable still holds the old value. Deterministic, no packages.
- **Damage:** every reflected write silently no-ops: settings objects full
  of defaults, mapped DTOs half-empty - and only for the struct-typed ones,
  which makes the pattern look haunted.
- **Verified:** ran on .NET 10 (2026-07-22): SetValue on the boxed copy,
  variable unchanged.

### getproperty-misses-nonpublic (A5)

- **Twist:** `GetProperty("Channel")` returns null for an internal
  property - and the obvious fix, passing BindingFlags.NonPublic, returns
  null for even more: specifying *any* flags erases the defaults, so
  Public, Instance, and Static must all be rebuilt by hand.
- **Mechanic:** the default binding flags are Public | Instance | Static;
  passing any BindingFlags value *replaces* them, never augments.
  NonPublic alone matches nothing (no Instance or Static in the set);
  NonPublic | Instance finds the internal property but now misses public
  ones - the mapper that "added support for internals" just dropped
  support for everything else. Every miss is a null or an empty array;
  nothing throws.
- **Who hits it:** hand-rolled mappers and serializer-ish code enumerating
  GetProperties(); a refactor makes a setter or property internal and the
  reflective consumer silently stops copying it.
- **Repro:** class with public Id, internal Channel, private Secret,
  public static Source: `GetProperty("Channel")` null; plus NonPublic
  still null; plus NonPublic | Instance found - while
  `GetProperty("Id", NonPublic | Instance)` is null for the *public*
  property. Default GetProperties() lists Id and Source. Deterministic,
  no packages.
- **Damage:** half-populated objects with no error anywhere - and *which*
  half depends on which flag set whoever last touched the call site
  guessed, so two mappers in one codebase disagree about the same class.
- **😈 seed:** the trap punishes the careful: the developer who read the
  docs and passed explicit flags wrote a subtler bug than the one who
  passed nothing - every flag combination is a different silent subset,
  and none of them throws.
- **Verified:** ran on .NET 10 (2026-07-22): all five lookups behaved as
  listed, including the public property invisible under
  NonPublic | Instance.

### activator-needs-parameterless-ctor (A5)

- **Twist:** `Activator.CreateInstance<T>()` compiles for every T and
  throws MissingMethodException the moment T lacks a public parameterless
  constructor - a factory that "works for everything" until the first
  type that doesn't.
- **Mechanic:** generic Activator carries no compile-time constraint; the
  constructor lookup happens at runtime. `where T : new()` is the
  compile-time spelling of the same requirement and would have caught it.
  Value types always pass (their default ctor is free). Crucially, adding
  a constructor *with* parameters to a plain class removes the implicit
  parameterless one - which is how long-working code starts throwing.
- **Who hits it:** generic factories, plugin loaders, test-object
  builders, deserializers - and the POCO that one day gains a
  `Widget(string name)` constructor in an unrelated PR.
- **Repro:** `static T Create<T>() => Activator.CreateInstance<T>()!;` -
  Simple and int create fine, Widget with only a string ctor throws
  MissingMethodException; `CreateInstance(typeof(Widget), "manual")`
  works. BUILDER NOTE: the default file-based AOT profile emits
  trim-analysis warning IL2091 on the generic helper - add
  `#:property PublishAot=false` to keep the build clean. No packages.
- **Damage:** a runtime crash on a path that reviews as fully generic and
  obviously fine - and the crash and its cause land in different commits:
  the ctor was added over here, the Activator call detonated over there.
- **😈 seed:** the stack trace points five layers into framework
  plumbing, and the message names the type but not the call site's
  intent - the investigation starts at the infrastructure that didn't
  change instead of the POCO that did.
- **Verified:** ran on .NET 10 (2026-07-22): Widget threw
  MissingMethodException, Simple and int created, args overload worked;
  IL2091 observed under the default AOT profile.

### changetype-chokes-on-nullable (A4)

- **Twist:** the mapper converts "5" into every int, decimal, and DateTime
  column fine - and throws InvalidCastException on `int?`:
  Convert.ChangeType handles the whole primitive zoo except the nullable
  wrapper around it.
- **Mechanic:** ChangeType converts between IConvertible types;
  Nullable&lt;T&gt; is not one, and there is no built-in unwrap - the cast
  from String to Nullable`1 just fails. The one-line fix everyone
  eventually finds: convert to
  `Nullable.GetUnderlyingType(t) ?? t` - boxing then does the rest, since
  a boxed int assigns into an int? property.
- **Who hits it:** every hand-rolled row/CSV/config-to-object mapper (the
  Dapper-shaped code people write themselves). It works for required
  columns and dies the day an *optional* column arrives with a value in
  it - exactly the field the test data always left empty.
- **Repro:** class { int Qty; int? Discount }; loop GetProperties() with
  `Convert.ChangeType("5", p.PropertyType)`: Qty converts, Discount
  throws; the GetUnderlyingType fix converts both. Deterministic, no
  packages.
- **Damage:** the import crashes only on rows where an optional field is
  filled - "works for months, dies on the first customer who entered a
  discount" - and the stack blames Convert, not the schema change that
  made the column nullable.
- **😈 seed:** the failure is data-shaped, not code-shaped: the bug report
  says "import crashes on THIS file", and diffing the good and bad files
  finds nothing - the difference is one optional cell somebody finally
  filled in.
- **Verified:** ran on .NET 10 (2026-07-22): Int32 converted,
  Nullable&lt;Int32&gt; threw InvalidCastException, the GetUnderlyingType
  fix converted both.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **gettype-string-is-local** - Type.GetType("Full.Name") searches only the
  calling assembly and corlib: your types and System.String resolve, any
  other assembly's type is silently null (verified 2026-07-22:
  System.Text.Json.JsonSerializer null, assembly-qualified name found).
  Config-driven type loading is the natural habitat; needs a damage
  framing before promoting.

- **readonly-yields-to-reflection** - FieldInfo.SetValue happily writes a
  readonly field (verified 2026-07-22: 10 -> 999): "immutable" shared
  state mutated by a mapper. Real, but needs a who-hits-it where the
  write is accidental rather than deliberate before promoting.

- **property-attributes-dont-inherit** - PropertyInfo.GetCustomAttributes(
  inherit: true) IGNORES the flag for properties - an override reports 0
  attributes - while static Attribute.GetCustomAttributes(prop, true)
  honors it and reports 1 (both verified 2026-07-22 on .NET 10). Two
  spellings of the same question, opposite answers: strong A4 material,
  promote with a validation-framework framing.
