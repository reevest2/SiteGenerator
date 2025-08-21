using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace SiteGenerator2
{
    public interface IAIService
    {
        Task<AIGeneratedContent> GenerateContentAsync(string prompt);
    }

    public class AIService : IAIService
    {
        private readonly AzureOpenAIClient _azureClient;
        private readonly string _deploymentName;

        public AIService(IConfiguration configuration)
        {
            var endpoint = new Uri(configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured"));
            var apiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");
            _deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName not configured");
            _azureClient = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey));
        }

        public async Task<AIGeneratedContent> GenerateContentAsync(string prompt)
        {
            var chatClient = _azureClient.GetChatClient(_deploymentName);
            
            var systemMessage = ChatMessage.CreateSystemMessage(@"Generate valid HTML, CSS, and JavaScript code.
Return a JSON object with properties html, css, js.
Buttons should call window.regenerateAIContent(prompt).");

            var userMessage = ChatMessage.CreateUserMessage(prompt);

            var chatCompletionOptions = new ChatCompletionOptions()
            {
            };

            try
            {
                var response = await chatClient.CompleteChatAsync([systemMessage, userMessage], chatCompletionOptions);
                
                var content = response.Value.Content[0].Text;
                Console.WriteLine(content);
                return ParseAIResponse(content);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to generate AI content: {ex.Message}", ex);
            }
        }

        private AIGeneratedContent ParseAIResponse(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new AIGeneratedContent { Html = "<p>No content generated</p>" };
            }

            // Try to parse the assistant content as our AIGeneratedContent JSON
            try
            {
                var parsed = JsonSerializer.Deserialize<AIGeneratedContent>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });
                if (parsed != null) return parsed;
            }
            catch
            {
                // fall through to extraction
            }

            // Fallback: try to extract from raw content
            return ExtractContentFromRawResponse(content);
        }

        private AIGeneratedContent ExtractContentFromRawResponse(string content)
        {
            var result = new AIGeneratedContent();

            var cssMatch = System.Text.RegularExpressions.Regex.Match(content, @"<style>(.*?)</style>", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (cssMatch.Success)
            {
                result.Css = cssMatch.Groups[1].Value;
                content = content.Replace(cssMatch.Value, "");
            }

            var jsMatch = System.Text.RegularExpressions.Regex.Match(content, @"<script>(.*?)</script>", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (jsMatch.Success)
            {
                result.Js = jsMatch.Groups[1].Value;
                content = content.Replace(jsMatch.Value, "");
            }

            result.Html = content.Trim();
            return result;
        }
    }

    public class AIGeneratedContent
    {
        public string Html { get; set; } = string.Empty;
        public string Css { get; set; } = string.Empty;
        public string Js { get; set; } = string.Empty;
    }
}