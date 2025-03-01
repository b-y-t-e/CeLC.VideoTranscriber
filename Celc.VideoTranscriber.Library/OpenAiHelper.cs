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

            using var api = new OpenAIClient(apiKey, OpenAIClientSettings.Default, new HttpClient() { Timeout = TimeSpan.FromSeconds(900) });

            var messages = new List<Message>
            {
                new Message(Role.System, prompt),
                new Message(Role.User, new List<Content> { input })
            };

            var chatRequest = new ChatRequest(messages, model: model);
            string text = null;
            int retries = 5;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    var response = await api.ChatEndpoint.GetCompletionAsync(chatRequest);
                    text = response.FirstChoice.Message.Content?.ToString();
                    break; // Exit the loop if successful
                }
                catch (TimeoutException)
                {
                    if (i == retries - 1)
                        throw; // Rethrow the exception if max retries reached
                }
                catch (Exception ex)
                {
                    if (!ex.Message.ToLower().Contains("timeout") || i == retries - 1)
                        throw;
                }
            }

            lock (_cacheLock)
            {
                _cache[key] = text;
                SaveCache();
            }

            return text;
        }
    }
}
