// src/EventBooking.SeedData/Program.cs (complete)
using EventBooking.SeedData;

try
{
    var options = SeedCliOptions.Parse(args, Environment.GetEnvironmentVariable);
    await using var steps = SeedRunSteps.Create(options, Environment.GetEnvironmentVariable);
    return await SeedCommand.RunAsync(options, steps, Console.Out, CancellationToken.None);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Seed failed: {exception.Message}");
    Console.Error.WriteLine(
        "Usage: EventBooking.SeedData <connection-string> [--demo [--reanchor] [--reseed]] [--verbose]");
    return 2;
}
