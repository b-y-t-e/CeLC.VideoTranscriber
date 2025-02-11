using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace CeLC.VideoTranscriber.Library
{
    public class OpenAiHelper
    {
        private static readonly string CacheFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "openai-prompts.cache");

        private static Dictionary<string, string> _cache;

        private static readonly object _cacheLock = new object();

        static OpenAiHelper()
        {
            LoadCache();
        }

        private static void LoadCache()
        {
            if (_cache != null)
                return;

            lock (_cacheLock)
            {
                if (_cache != null)
                    return;

                if (File.Exists(CacheFilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(CacheFilePath);
                        _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                                 ?? new Dictionary<string, string>();
                    }
                    catch
                    {
                        _cache = new Dictionary<string, string>();
                    }
                }
                else
                {
                    _cache = new Dictionary<string, string>();
                }
            }
        }

        private static void SaveCache()
        {
            lock (_cacheLock)
            {
                var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CacheFilePath, json);
            }
        }

        public async Task<string> ExecutePrompt(string apiKey, string model, string prompt, string input)
        {
            var key = $"{model}|||{prompt}|||{input}";

            lock (_cacheLock)
            {
                if (_cache.ContainsKey(key))
                {
                    return _cache[key];
                }
            }

            using var api = new OpenAIClient(apiKey);

            var messages = new List<Message>
            {
                new Message(Role.System, prompt),
                new Message(Role.User, new List<Content> { input })
            };

            // Używamy modelu GPT4oMini lub innego dostępnego w bibliotece.
            // var chatRequest = new ChatRequest(messages, model: Model.GPT4oMini);
            var chatRequest = new ChatRequest(messages, model: model);
            var response = await api.ChatEndpoint.GetCompletionAsync(chatRequest);

            var text = response.FirstChoice.Message.Content?.ToString();

            lock (_cacheLock)
            {
                _cache[key] = text;
                SaveCache();
            }

            return text;
        }
    }
}
