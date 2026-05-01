using System.Runtime.CompilerServices;
using Plume.Abstractions;

namespace Plume;

/// <summary>
/// Default implementation of <see cref="IPlumeClient"/>. Translates the public
/// API into provider-agnostic <see cref="ProviderRequest"/>s.
/// </summary>
internal sealed class DefaultPlumeClient(IProvider provider, PlumeOptions options) : IPlumeClient
{
    private readonly IProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    private readonly PlumeOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async Task<string> AskAsync(
        string prompt,
        AskOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(prompt);

        var request = BuildRequest(prompt, options);
        var response = await _provider.SendAsync(request, ct).ConfigureAwait(false);
        return response.Content;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        AskOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(prompt);

        if (_provider is not IStreamingProvider streaming)
        {
            throw new NotSupportedException(
                $"Provider '{_provider.Name}' does not support streaming.");
        }

        var request = BuildRequest(prompt, options);
        await foreach (var chunk in streaming.StreamAsync(request, ct).ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
                yield return chunk.Content;
        }
    }

    public IChatSession NewChat(string? system = null, AskOptions? options = null)
        => new ChatSession(_provider, _options, system, options);

    public async Task<PlumeResponse> SendAsync(
        PlumeRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var providerRequest = new ProviderRequest
        {
            Model = request.Model ?? _options.DefaultModel
                ?? throw new InvalidOperationException(
                    "No model specified on the request and no DefaultModel configured."),
            Messages = request.Messages,
            Temperature = request.Temperature ?? _options.DefaultTemperature,
            MaxTokens = request.MaxTokens ?? _options.DefaultMaxTokens,
            StopSequences = request.StopSequences,
            Extensions = request.Extensions
        };

        var resp = await _provider.SendAsync(providerRequest, ct).ConfigureAwait(false);

        return new PlumeResponse
        {
            Content = resp.Content,
            Model = resp.Model,
            Provider = _provider.Name,
            FinishReason = resp.FinishReason,
            Usage = resp.Usage,
            Metadata = resp.Metadata
        };
    }

    private ProviderRequest BuildRequest(string prompt, AskOptions? options)
    {
        var messages = new List<Message>(2);

        var system = options?.System ?? _options.DefaultSystemPrompt;
        if (!string.IsNullOrWhiteSpace(system))
            messages.Add(new Message(MessageRole.System, system));

        messages.Add(new Message(MessageRole.User, prompt));

        return new ProviderRequest
        {
            Model = options?.Model ?? _options.DefaultModel
                ?? throw new InvalidOperationException(
                    "No model specified. Set AskOptions.Model or PlumeOptions.DefaultModel."),
            Messages = messages,
            Temperature = options?.Temperature ?? _options.DefaultTemperature,
            MaxTokens = options?.MaxTokens ?? _options.DefaultMaxTokens,
            StopSequences = options?.StopSequences,
            Extensions = options?.Extensions
        };
    }
}
