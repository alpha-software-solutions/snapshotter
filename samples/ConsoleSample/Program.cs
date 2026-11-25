// No test framework at all. Snapshots are named after this file's class (Program) and the calling member; top
// level statements all run in one member, so each verification gives itself a name with UseMethodName. A mismatch
// throws a VerifyException, so the process exits with a failure code, which is all a CI step needs.

var order = new
{
    Id = Guid.NewGuid(),
    Lines = new[] { "apple", "pear" },
    Total = 12.5m,
    PlacedAt = DateTimeOffset.UtcNow
};

await Verify(order).UseMethodName("Order");

await VerifyJson("""{"status": "ok", "requestId": "6b1b7a4e-6f0a-4c36-9a44-1f1d5a5f3c11"}""").UseMethodName("Status");

Console.WriteLine("All snapshots match.");
