---
id: "0066"
title: the server that trusted the request
category: security
tags: [security, validation, model-binding]
rule: "never read price or status from the **request body** - the server owns them, keyed by id"
---

# #0066 - The Server That Trusted the Request

## 💥 Symptom

An order goes through for a penny. Or a record shows up already marked `Paid`, `Approved`, or
`Admin`, with no one on staff having touched it. The endpoint validated the input - the fields
were the right types, nothing was null - and still the client decided its own price and its own
status. Nothing was "hacked"; the request simply carried fields the server took at face value.

## 🔍 The Offending Code

```csharp
var order = new Order
{
    ProductId = req.ProductId,
    Quantity  = req.Quantity,
    UnitPrice = req.UnitPrice, // 💥 from the body, not the catalog
    Status    = req.Status,    // 💥 the client set the paid status
};
Charge(order.Quantity * order.UnitPrice);
```

## 🧠 What's Actually Going On

The request body is **input**, not state. Every field in it is whatever the client chose to send
- and a client is not only the app you shipped; it is curl, a script, a modified page, anyone who
can form an HTTP request. When the server copies price, total, status, role, ownership, or
discount straight from the body onto the thing it saves or charges, it hands those decisions to
the caller. Model binding makes it effortless and invisible: the DTO has a `UnitPrice` property,
the JSON has a `unitPrice` field, they bind, and the "wait, the client set the price?" moment never
happens in review because the line reads like ordinary mapping.

The broken belief is "I validated the request, so the data is safe." Validation checks *shape* -
types, ranges, required fields - not *authority*. `unitPrice: 0.01` is a perfectly valid decimal;
`status: "Paid"` is a perfectly valid string. What is missing is the question of who is allowed to
decide that field, and for price and status the answer is the server, from its own data, keyed by
an id the client cannot forge past.

## ✅ The Fix

Accept only the fields the client legitimately owns - which product, how many - and derive
everything money- or authority-related from the server's own source of truth:

```csharp
var order = new Order
{
    ProductId = req.ProductId,
    Quantity  = req.Quantity,
    UnitPrice = catalog[req.ProductId], // price from the catalog, keyed by ProductId
    Status    = "Pending",              // the server owns the lifecycle
};
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| A narrow input DTO (only client-owned fields) | The default - the request type carries `ProductId` and `Quantity`, and there is nothing to bind price or status *from*. |
| Look up server-owned values by id | Price from the catalog, tax/shipping from your rules, ownership from the authenticated user - never from the body. |
| Bind broadly, then overwrite trusted fields | If you must bind the whole object, immediately set `Status`, `UnitPrice`, `UserId` from the server before saving - weaker, because "remember to overwrite" is a step you can forget. |
| `[JsonIgnore]` on sensitive properties | A quick guard on a shared model so they never bind at all - though a purpose-built input type states the intent better. |

## 😈 The Even Worse Sibling

The penny order is the loud version - a reconciliation report catches "charged != catalog price"
eventually. The quiet version is a field nobody audits: the request carries `userId`, `tenantId`,
or `accountId`, the server binds it, and now the caller reads and writes *other people's* data
through an endpoint that "obviously" scopes to the current user - an object-level authorization
(IDOR) hole opened by the same reflexive mapping. The most invisible face is a boolean that flips
a workflow: `isPaid`, `emailVerified`, `isApproved` bound from the body, so a self-service action
skips the step that was supposed to set it - no exception, no wrong number, just a record that
reached a trusted state it never earned. Money you can reconcile; ownership and status you often
cannot, because nothing ever looks wrong. It is the same wrong-price outcome as
[0012-zero-priced-order](../../serialization/0012-zero-priced-order/), arrived at from the other
direction: there a default filled the price, here the client did.

## 🎓 Advanced Nuance

- **Validation and authorization are different questions.** A validator answers "is this a
  well-formed decimal / non-empty string / in range?" It never answers "is this caller allowed to
  set this field to this value?" Passing validation says nothing about authority - and the fields
  that need an authority check are exactly the ones you should not bind at all.
- **The authenticated identity is server state, not a request field.** Take `userId` / `tenantId`
  from the auth token or session, never from the body or a query string - a request-supplied
  identity is a claim the client makes about itself, and the client is the adversary.
- **Even "hidden" form fields are client-controlled.** A value the UI renders as a read-only or
  hidden input still arrives in the POST as plain text the caller can change; there is no field
  the browser will send that the user cannot edit. If it is on the wire, treat it as
  attacker-chosen.

## 🔎 How to Find It in Your Codebase

- Grep for actions that bind a request/DTO and assign `Price`, `Total`, `Amount`, `Status`,
  `Role`, `IsAdmin`, `UserId`, `TenantId`, or `Discount` straight from it - money and authority
  fields sourced from the body are the shape.
- Look for a domain entity used directly as the `[FromBody]` parameter (binding the whole entity) -
  a dedicated input DTO with only client-owned fields removes the risk by construction.
- Symptom-side: charges that do not match catalog prices; records in a trusted state (`Paid`,
  `Approved`) with no server action that set them; users able to read or modify rows outside their
  own scope.
- Take money, status, ownership, and roles from the server's data or the auth context, keyed by an
  id; accept from the body only what the client legitimately decides, and validate that shape on
  top - shape checks are necessary but never sufficient.
