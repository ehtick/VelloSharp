using VelloSharp;

Console.WriteLine("Verifying VelloSharp.Core package usage…");

var builder = new PathBuilder()
    .MoveTo(0, 0)
    .LineTo(10, 10)
    .QuadraticTo(15, 15, 20, 5)
    .Close();

var commands = builder.AsSpan();
if (commands.Length == 0 || commands[0].Verb != PathVerb.MoveTo)
{
    throw new InvalidOperationException("PathBuilder did not record expected commands.");
}

Console.WriteLine($"PathBuilder recorded {commands.Length} commands.");
Console.WriteLine($"Assembly location: {typeof(PathBuilder).Assembly.Location}");

Console.WriteLine("VelloSharp.Core integration test completed.");
