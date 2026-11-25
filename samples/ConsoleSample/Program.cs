// No test framework at all. Snapshots are named after this file (Program). Top level statements all run
// in one member, so each verification gives itself a name with UseMethodName. A mismatch throws, so the
// process exits with a failure code, which is all a CI step needs.

var order = new
{
    Id = 1001,
    Lines = new[] { "apple", "pear" },
    Total = 12.5m,
    PlacedAt = DateTimeOffset.UtcNow
};

await Verify(order).UseMethodName("Order");

await VerifyJson("""{"status": "ok", "requestId": "6b1b7a4e-6f0a-4c36-9a44-1f1d5a5f3c11"}""").UseMethodName("Status");

Console.WriteLine("All snapshots match.");
