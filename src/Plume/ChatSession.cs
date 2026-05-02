using System.Runtime.CompilerServices;
using System.Text;
using Plume.Abstractions;
using Plume.Tools;

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
    private Dictionary<string, BoundTool>? _tools;

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

    public int MaxToolIterations { get; set; } = 10;

    public IChatSession UseTools(params BoundTool[] tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        _tools ??= new();
        foreach (var t in tools)
        {
            if (t is null) throw new ArgumentException("Tool array contains a null entry.", nameof(tools));
            _tools[t.Tool.Name] = t;
        }
        return this;
    }

    public async Task<string> AskAsync(string userMessage, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userMessage);

        _history.Add(new Message(MessageRole.User, userMessage));

        for (int iteration = 0; iteration < MaxToolIterations + 1; iteration++)
        {
            var request = BuildRequest();
            var response = await _provider.SendAsync(request, ct).ConfigureAwait(false);

            // No tool calls or tools not registered → final answer
            if (_tools is null || _tools.Count == 0 || response.ToolCalls is not { Count: > 0 } calls)
            {
                _history.Add(new Message(MessageRole.Assistant, response.Content));
                return response.Content;
            }

            // Append the assistant turn carrying the tool_calls so the provider can
            // round-trip it on the next request.
            _history.Add(new Message(MessageRole.Assistant, response.Content)
            {
                ToolCalls = calls
            });

            // Execute each call and append the result message.
            foreach (var call in calls)
            {
                ct.ThrowIfCancellationRequested();
                string result;
                if (_tools.TryGetValue(call.Name, out var bound))
                {
                    try
                    {
                        result = await bound.Handler(call.ArgumentsJson, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        result = $"{{\"error\":\"{Escape(ex.Message)}\"}}";
                    }
                }
                else
                {
                    result = $"{{\"error\":\"Tool '{Escape(call.Name)}' is not registered with this session.\"}}";
                }

                _history.Add(new Message(MessageRole.Tool, result) { ToolCallId = call.Id });
            }
        }

        throw new InvalidOperationException(
            $"Tool-call loop exceeded MaxToolIterations ({MaxToolIterations}). " +
            "The model is likely stuck in a loop — increase MaxToolIterations or review tool definitions.");
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userMessage);

        if (_provider is not IStreamingProvider streaming)
            throw new NotSupportedException(
                $"Provider '{_provider.Name}' does not support streaming.");

        if (_tools is { Count: > 0 })
            throw new NotSupportedException(
                "Streaming with tools is not supported in this release. " +
                "Use AskAsync for tool-enabled sessions.");

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

    private ProviderRequest BuildRequest()
    {
        IReadOnlyList<Tool>? toolDefs = null;
        if (_tools is { Count: > 0 })
        {
            var defs = new List<Tool>(_tools.Count);
            foreach (var bt in _tools.Values) defs.Add(bt.Tool);
            toolDefs = defs;
        }

        return new ProviderRequest
        {
            Model = _sessionDefaults?.Model ?? _options.DefaultModel
                ?? throw new InvalidOperationException("No model configured."),
            Messages = _history.ToList(),
            Temperature = _sessionDefaults?.Temperature ?? _options.DefaultTemperature,
            MaxTokens = _sessionDefaults?.MaxTokens ?? _options.DefaultMaxTokens,
            StopSequences = _sessionDefaults?.StopSequences,
            ResponseSchema = _sessionDefaults?.ResponseSchema,
            Tools = toolDefs ?? _sessionDefaults?.Tools,
            ToolChoice = _sessionDefaults?.ToolChoice,
            Extensions = _sessionDefaults?.Extensions
        };
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
