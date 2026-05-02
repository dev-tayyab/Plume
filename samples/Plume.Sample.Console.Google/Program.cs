using Plume;
using Plume.Google;

var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
    ?? throw new InvalidOperationException("Set GOOGLE_API_KEY environment variable.");

using var http = new HttpClient();
var provider = new GoogleProvider(http, new GoogleProviderOptions { ApiKey = apiKey });

var ai = PlumeClient.CreateBuilder()
    .Use(provider)
    .WithDefaultModel("gemini-2.5-flash")
    .Build();

var chat = ai.NewChat(system: "You are a helpful assistant. Keep replies concise unless asked to elaborate.");

using var cts = new ConsoleCancellationSource();

Console.WriteLine("Plume live chat — Gemini 2.5 Flash");
Console.WriteLine("Type your message and hit Enter. Commands: /reset, /history, /exit");
Console.WriteLine();

while (true)
{
    Console.Write("you > ");
    var input = Console.ReadLine();

    if (input is null) break;
    input = input.Trim();
    if (input.Length == 0) continue;

    switch (input)
    {
        case "/exit":
        case "/quit":
            return;
        case "/reset":
            chat.Reset();
            Console.WriteLine("(history cleared)");
            continue;
        case "/history":
            foreach (var m in chat.History)
                Console.WriteLine($"  [{m.Role}] {m.Content}");
            continue;
    }

    Console.Write("ai  > ");
    try
    {
        await foreach (var chunk in chat.StreamAsync(input, cts.Token))
            Console.Write(chunk);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("\n(interrupted)");
        cts.Reset();
    }
    catch (PlumeException ex)
    {
        Console.WriteLine($"\n(error: {ex.Message})");
    }

    Console.WriteLine();
    Console.WriteLine();
}

internal sealed class ConsoleCancellationSource : IDisposable
{
    private CancellationTokenSource _cts = new();

    public ConsoleCancellationSource()
    {
        Console.CancelKeyPress += OnCancel;
    }

    public CancellationToken Token => _cts.Token;

    public void Reset()
    {
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    private void OnCancel(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _cts.Cancel();
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancel;
        _cts.Dispose();
    }
}