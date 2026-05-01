using System.Runtime.CompilerServices;
using System.Text;
using Plume.Abstractions;

namespace Plume;

/// <summary>
/// Internal default implementation of <see cref="IChatSession"/>.
/// Maintains an in-memory list of messages and forwards calls to the provider.
/// </summary>
internal sealed class ChatSession : IChatSession
{
    private readonly IProvider _provider;
    private readonly PlumeOptions _options;
    private readonly AskOptions? _sessionDefaults;
    private readonly List<Message> _history = new();

    public ChatSession(
        IProvider provider,
        PlumeOptions options,
        string? system,
        AskOptions? sessionDefaults)
    {
        _provider = provider;
        _options = options;
        _sessionDefaults = sessionDefaults;

        if (!string.IsNullOrWhiteSpace(system))
            _history.Add(new Message(MessageRole.System, system));
    }

    public IReadOnlyList<Message> History => _history;

    public async Task<string> AskAsync(string userMessage, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userMessage);

        _history.Add(new Message(MessageRole.User, userMessage));

        var request = BuildRequest();
        var response = await _provider.SendAsync(request, ct).ConfigureAwait(false);

        _history.Add(new Message(MessageRole.Assistant, response.Content));
        return response.Content;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userMessage);

        if (_provider is not IStreamingProvider streaming)
            throw new NotSupportedException(
                $"Provider '{_provider.Name}' does not support streaming.");

        _history.Add(new Message(MessageRole.User, userMessage));

        var request = BuildRequest();
        var sb = new StringBuilder();

        await foreach (var chunk in streaming.StreamAsync(request, ct).ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(chunk.Content))
                continue;

            sb.Append(chunk.Content);
            yield return chunk.Content;
        }

        _history.Add(new Message(MessageRole.Assistant, sb.ToString()));
    }

    public void AddMessage(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _history.Add(message);
    }

    public void Reset()
    {
        if (_history.Count > 0 && _history[0].Role == MessageRole.System)
        {
            var system = _history[0];
            _history.Clear();
            _history.Add(system);
        }
        else
        {
            _history.Clear();
        }
    }

    private ProviderRequest BuildRequest() => new()
    {
        Model = _sessionDefaults?.Model ?? _options.DefaultModel
            ?? throw new InvalidOperationException("No model configured."),
        Messages = _history.ToList(),
        Temperature = _sessionDefaults?.Temperature ?? _options.DefaultTemperature,
        MaxTokens = _sessionDefaults?.MaxTokens ?? _options.DefaultMaxTokens,
        StopSequences = _sessionDefaults?.StopSequences,
        Extensions = _sessionDefaults?.Extensions
    };
}
