// No test framework at all. Snapshotter names the snapshots after this file (Program) and, as top
// level statements all run in one member, each verification gives its own name with UseMethodName.
// A mismatch throws, so the process exits with a failure code, which is all a CI step needs.

var order = new
{
    Id = 1001,
    Lines = new[] { "apple", "pear" },
    Total = 12.5m
};

await Verify(order).UseMethodName("Order");

await VerifyJson("""{"status": "ok", "count": 2}""").UseMethodName("Status");

Console.WriteLine("All snapshots match.");
