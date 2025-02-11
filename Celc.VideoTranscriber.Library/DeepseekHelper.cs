using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeepSeek.Core;
using DeepSeek.Core.Models;

public class DeepseekHelper
{
    private static readonly string CacheFilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deepseek-prompts.cache");

    private static Dictionary<string, string> _cache;
    private static readonly object _cacheLock = new object();

    static DeepseekHelper()
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

    public async Task<string> ExecutePrompt(string apiKey, string prompt, string input)
    {
        var key = $"{prompt}|||{input}";

        lock (_cacheLock)
            if (_cache.ContainsKey(key))
                return _cache[key];


        var maxRetries = 20;
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                var client = new DeepSeekClient(apiKey);

                var request = new ChatRequest
                {
                    Messages = new List<Message>
                    {
                        Message.NewSystemMessage(prompt),
                        Message.NewUserMessage(input)
                    },
                    Model = Constant.Model.ChatModel
                };

                var chatResponse = await client.ChatAsync(request, new CancellationToken());
                if (chatResponse is null)
                {
                    throw new Exception(client.ErrorMsg);
                }

                var result = chatResponse.Choices.First().Message?.Content;

                lock (_cacheLock)
                {
                    _cache[key] = result;
                    SaveCache();
                }

                return result;
            }
            catch (Exception ex)
            {
                if (ex.Message == "empty response")
                {
                    i--;
                }
                else
                {
                    if (i == (maxRetries - 1))
                        throw;
                    await Task.Delay(500 + 250 * i);
                }
            }
        }

        return null;
    }
}
