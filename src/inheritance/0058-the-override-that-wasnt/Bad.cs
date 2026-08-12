// Exhibit #0058: `new` hides, it does not override

using System.Globalization;

// Pin formatting so the demo reads the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// CreditCard changes the fee. It was declared `new` - the IDE offered it to silence the
// warning, and it compiled, so it looked like an override.
var card = new CreditCard(100m);
PaymentMethod asBase = card;               // the SAME object, seen as its base type

Console.WriteLine($"Through CreditCard ref:    {card.Fee()}");   // 3.00 - derived Fee() runs
Console.WriteLine($"Through PaymentMethod ref: {asBase.Fee()}"); // 0    - base Fee() runs

// Checkout sums the fee of every method in the cart - and a cart is a List<PaymentMethod>,
// so every element travels as the base type.
var cart = new List<PaymentMethod> { new PaymentMethod(50m), card };
decimal totalFee = cart.Sum(m => m.Fee()); // 💥 base Fee() runs for every element

Console.WriteLine($"Total fee charged: {totalFee}");

// Self-audit: the card must contribute its 3% (3.00); the base method's 0 must not stand in.
if (totalFee != 3.00m)
{
    throw new InvalidOperationException(
        $"expected 3.00 in card fees, charged {totalFee} - CreditCard.Fee() was declared `new`, not " +
        "`override`, so through PaymentMethod the base Fee() (0) runs and the 3% is silently never charged");
}

Console.WriteLine("Every method charged its real fee.");

// --- types ---

class PaymentMethod
{
    protected readonly decimal Amount;
    public PaymentMethod(decimal amount) => Amount = amount;
    public decimal Fee() => 0m;                  // base: no fee
}

class CreditCard : PaymentMethod
{
    public CreditCard(decimal amount) : base(amount) { }
    public new decimal Fee() => Amount * 0.03m;  // 💥 `new` HIDES Fee - it does not override it
}
