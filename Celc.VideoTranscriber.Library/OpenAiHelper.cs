using OpenAI;
using OpenAI.Chat;

namespace Celc.VideoTranscriber.Library;

public class OpenAiHelper
{
    public async Task<string> ExecutePrompt(string apiKey, string prompt, string input)
    {
        using var api =
            new OpenAIClient(
                apiKey);

        var messages = new List<Message>
        {
            new Message(Role.System, prompt),
            new Message(Role.User, new List<Content>
            {
                input
            })
        };
        var chatRequest = new ChatRequest(messages, model: "gpt-4o"); // Model.GPT4_Turbo);
        var response = await api.ChatEndpoint.GetCompletionAsync(chatRequest);

        var text = response.FirstChoice.Message.Content.ToString();
        return text;
    }
}
