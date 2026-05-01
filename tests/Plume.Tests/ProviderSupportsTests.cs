using Plume.Anthropic;
using Plume.Google;
using Plume.Ollama;
using Plume.OpenAI;
using Xunit;

namespace Plume.Tests;

public class ProviderSupportsTests
{
    [Theory]
    [InlineData("gpt-4o-mini", true)]
    [InlineData("gpt-4o", true)]
    [InlineData("gpt-3.5-turbo", true)]
    [InlineData("o1-preview", true)]
    [InlineData("o3-mini", true)]
    [InlineData("o4", true)]
    [InlineData("claude-sonnet-4", false)]
    [InlineData("gemini-2.0-flash", false)]
    [InlineData("llama3", false)]
    [InlineData("", false)]
    public void OpenAiSupportsCorrectModels(string model, bool expected)
    {
        var http = new HttpClient { BaseAddress = new Uri("https://api.openai.com") };
        var provider = new OpenAiProvider(http, new OpenAiProviderOptions { ApiKey = "x" });
        Assert.Equal(expected, provider.Supports(model));
    }

    [Theory]
    [InlineData("claude-sonnet-4", true)]
    [InlineData("claude-3-5-sonnet-20241022", true)]
    [InlineData("claude-opus-4-7", true)]
    [InlineData("gpt-4o", false)]
    [InlineData("gemini-2.0-flash", false)]
    public void AnthropicSupportsCorrectModels(string model, bool expected)
    {
        var http = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com") };
        var provider = new AnthropicProvider(http, new AnthropicProviderOptions { ApiKey = "x" });
        Assert.Equal(expected, provider.Supports(model));
    }

    [Theory]
    [InlineData("gemini-2.0-flash", true)]
    [InlineData("gemini-1.5-pro", true)]
    [InlineData("Gemini-Pro", true)]
    [InlineData("gpt-4o", false)]
    [InlineData("claude-sonnet-4", false)]
    public void GoogleSupportsCorrectModels(string model, bool expected)
    {
        var http = new HttpClient { BaseAddress = new Uri("https://generativelanguage.googleapis.com") };
        var provider = new GoogleProvider(http, new GoogleProviderOptions { ApiKey = "x" });
        Assert.Equal(expected, provider.Supports(model));
    }

    [Theory]
    [InlineData("llama3", true)]
    [InlineData("any-arbitrary-model", true)]
    [InlineData("", false)]
    public void OllamaSupportsAnyModelByDefault(string model, bool expected)
    {
        var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
        var provider = new OllamaProvider(http, new OllamaProviderOptions());
        Assert.Equal(expected, provider.Supports(model));
    }

    [Fact]
    public void OllamaFiltersByRequiredPrefixWhenSet()
    {
        var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
        var provider = new OllamaProvider(http,
            new OllamaProviderOptions { RequiredModelPrefix = "llama" });

        Assert.True(provider.Supports("llama3"));
        Assert.True(provider.Supports("llama3.2"));
        Assert.False(provider.Supports("mistral"));
    }
}
