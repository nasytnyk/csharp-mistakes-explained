// Exhibit #0058: the fix

using System.Globalization;

// Pin formatting so the demo reads the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// CreditCard changes the fee - now with a virtual base and a real `override`, so the derived
// Fee() runs no matter which reference type calls it.
var card = new CreditCard(100m);
PaymentMethod asBase = card;               // the SAME object, seen as its base type

Console.WriteLine($"Through CreditCard ref:    {card.Fee()}");   // 3.00 - derived Fee() runs
Console.WriteLine($"Through PaymentMethod ref: {asBase.Fee()}"); // 3.00 - derived Fee() runs too

// Checkout sums the fee of every method in the cart - and a cart is a List<PaymentMethod>,
// so every element travels as the base type.
var cart = new List<PaymentMethod> { new PaymentMethod(50m), card };
decimal totalFee = cart.Sum(m => m.Fee()); // virtual dispatch picks each object's real Fee()

Console.WriteLine($"Total fee charged: {totalFee}");

// Self-audit: the card must contribute its 3% (3.00); the base method's 0 must not stand in.
if (totalFee != 3.00m)
{
    throw new InvalidOperationException(
        $"expected 3.00 in card fees, charged {totalFee}");
}

Console.WriteLine("Every method charged its real fee. As it should be.");

// --- types ---

class PaymentMethod
{
    protected readonly decimal Amount;
    public PaymentMethod(decimal amount) => Amount = amount;
    public virtual decimal Fee() => 0m;               // virtual: overridable
}

class CreditCard : PaymentMethod
{
    public CreditCard(decimal amount) : base(amount) { }
    public override decimal Fee() => Amount * 0.03m;  // override: dispatched by the runtime type
}
