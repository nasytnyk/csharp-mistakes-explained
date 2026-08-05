// Exhibit #0037: Task.Factory.StartNew with an async lambda

using System.Collections.Concurrent;

// On startup we warm the price cache in the background, then wait for it to
// finish before serving traffic. StartNew got picked "because it takes options".

var prices = new ConcurrentDictionary<string, decimal>();

// Fetch prices from the upstream feed, then fill the cache. The one-second call
// is the story, not the proof: the check below runs microseconds after the await
// returns, so an unfinished warmup is a certainty, not a timing race.
async Task WarmCacheAsync()
{
    await Task.Delay(TimeSpan.FromSeconds(1)); // the price-feed round-trip
    prices["WIDGET"] = 9.99m;
    prices["GADGET"] = 19.99m;
}

// "Run the warmup and await it - once it returns, the cache is ready."
Task warmup = Task.Factory.StartNew(async () => await WarmCacheAsync()); // 💥 returns Task<Task>: awaits the START, not the finish
await warmup;

Console.WriteLine($"Cache warmed. {prices.Count} of 2 prices loaded. Serving traffic.");

if (prices.Count != 2)
{
    throw new InvalidOperationException(
        $"warmup awaited and returned, yet the cache holds {prices.Count} of 2 prices - StartNew awaited the lambda's start, not its work");
}

Console.WriteLine("Every price is in the cache.");
