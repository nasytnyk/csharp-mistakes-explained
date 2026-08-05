# Halls registry

Canonical list of halls (exhibit categories). The `add-exhibit` and
`propose-exhibits` skills and the front page all draw from here.

- **opened** = has at least one exhibit.
- **planned** = reserved for the encyclopedia, not yet opened. A candidate there
  opens the hall.

Front-page display names may expand (⚡ "Async & Threading"). Emojis are unique
per hall. New halls: add a row here first.

## Opened

| slug | emoji | front-page name |
|------|:--:|---|
| collections | 🗂 | Collections |
| numbers | 🔢 | Numbers |
| async | ⚡ | Async & Threading |
| linq | 🔗 | LINQ & Lambdas |
| events | 🔔 | Events |
| value-types | 📦 | Value Types |
| exceptions | 💥 | Exceptions |
| orm | 🗄 | ORM |
| serialization | 📄 | Serialization |
| di-lifetimes | 💉 | DI Lifetimes |
| datetime | 📅 | Datetime |
| disposal | 🗑️ | Disposal |
| equality | ⚖️ | Equality |
| records | 📇 | Records |
| pattern-matching | 🧩 | Pattern Matching |
| logging | 🪵 | Logging |
| inheritance | 🪆 | Inheritance |
| memory | 💾 | Memory |

## Planned - language mechanics

| slug | emoji | front-page name |
|------|:--:|---|
| nullability | 🕳️ | Nullability |
| generics | 🧬 | Generics |
| enums | 🏷️ | Enums |

## Planned - runtime & resources

| slug | emoji | front-page name |
|------|:--:|---|
| boxing | 🥊 | Boxing |
| reflection | 🪞 | Reflection |

## Planned - ecosystem

| slug | emoji | front-page name |
|------|:--:|---|
| http | 🌐 | HTTP |
| configuration | ⚙️ | Configuration |
| testing | 🧪 | Testing |
| io | 📁 | IO & Files |

## Planned - strings & security

| slug | emoji | front-page name |
|------|:--:|---|
| strings | 🧵 | Strings |
| security | 🔒 | Security |

> `strings-memory` is retired in favor of splitting into `strings`
> (culture, encoding, grapheme) and `memory` (allocation, Span, GC) - two
> different worlds. Neither was opened yet, so this is a clean rename.
