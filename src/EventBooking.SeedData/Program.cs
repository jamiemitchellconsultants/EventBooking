// src/EventBooking.SeedData/Program.cs (complete)
using EventBooking.SeedData;

if (SeedUsage.IsHelpRequest(args))
{
    Console.Out.WriteLine(SeedUsage.Text);
    return 0;
}

SeedCliOptions options;
try
{
    options = SeedCliOptions.Parse(args, Environment.GetEnvironmentVariable);
}
catch (SeedException exception)
{
    Console.Error.WriteLine(SeedFailureReport.Format(exception, args));
    Console.Error.WriteLine(SeedUsage.Text);
    return 2;
}

try
{
    await using var steps = SeedRunSteps.Create(options, Environment.GetEnvironmentVariable);
    return await SeedCommand.RunAsync(options, steps, Console.Out, CancellationToken.None);
}
catch (Exception exception)
{
    Console.Error.WriteLine(SeedFailureReport.Format(exception, args));
    return 2;
}
