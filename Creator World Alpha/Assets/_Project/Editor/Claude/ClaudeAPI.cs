using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace CreatorWorld.Editor.Claude
{
    /// <summary>
    /// Client for the Anthropic Claude API
    /// </summary>
    public static class ClaudeAPI
    {
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";
        private const string ApiVersion = "2023-06-01";

        [Serializable]
        private class MessageRequest
        {
            public string model;
            public int max_tokens;
            public List<Message> messages;
            public string system;
        }

        [Serializable]
        private class Message
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class MessageResponse
        {
            public string id;
            public string type;
            public string role;
            public List<ContentBlock> content;
            public string model;
            public string stop_reason;
            public Usage usage;
        }

        [Serializable]
        private class ContentBlock
        {
            public string type;
            public string text;
        }

        [Serializable]
        private class Usage
        {
            public int input_tokens;
            public int output_tokens;
        }

        [Serializable]
        private class ErrorResponse
        {
            public Error error;
        }

        [Serializable]
        private class Error
        {
            public string type;
            public string message;
        }

        public class ChatMessage
        {
            public string Role { get; set; }
            public string Content { get; set; }

            public ChatMessage(string role, string content)
            {
                Role = role;
                Content = content;
            }
        }

        public class ApiResponse
        {
            public bool Success { get; set; }
            public string Content { get; set; }
            public string Error { get; set; }
            public int InputTokens { get; set; }
            public int OutputTokens { get; set; }
        }

        /// <summary>
        /// Send a message to Claude and get a response
        /// </summary>
        public static async Task<ApiResponse> SendMessageAsync(
            List<ChatMessage> messages,
            string systemPrompt = null)
        {
            var settings = ClaudeSettings.Instance;

            if (!settings.HasValidApiKey())
            {
                return new ApiResponse
                {
                    Success = false,
                    Error = "Invalid API key. Please configure your API key in Claude Settings."
                };
            }

            var requestMessages = new List<Message>();
            foreach (var msg in messages)
            {
                requestMessages.Add(new Message
                {
                    role = msg.Role,
                    content = msg.Content
                });
            }

            var request = new MessageRequest
            {
                model = settings.Model,
                max_tokens = settings.MaxTokens,
                messages = requestMessages,
                system = systemPrompt ?? GetDefaultSystemPrompt()
            };

            string jsonBody = JsonUtility.ToJson(request);

            using var webRequest = new UnityWebRequest(ApiUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("x-api-key", settings.ApiKey);
            webRequest.SetRequestHeader("anthropic-version", ApiVersion);

            webRequest.timeout = settings.TimeoutSeconds;

            var operation = webRequest.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Delay(100);
            }

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<MessageResponse>(webRequest.downloadHandler.text);

                string content = "";
                if (response.content != null && response.content.Count > 0)
                {
                    foreach (var block in response.content)
                    {
                        if (block.type == "text")
                        {
                            content += block.text;
                        }
                    }
                }

                return new ApiResponse
                {
                    Success = true,
                    Content = content,
                    InputTokens = response.usage?.input_tokens ?? 0,
                    OutputTokens = response.usage?.output_tokens ?? 0
                };
            }
            else
            {
                string errorMessage = "Unknown error";

                try
                {
                    var errorResponse = JsonUtility.FromJson<ErrorResponse>(webRequest.downloadHandler.text);
                    if (errorResponse?.error != null)
                    {
                        errorMessage = $"{errorResponse.error.type}: {errorResponse.error.message}";
                    }
                }
                catch
                {
                    errorMessage = webRequest.error ?? webRequest.downloadHandler.text;
                }

                return new ApiResponse
                {
                    Success = false,
                    Error = errorMessage
                };
            }
        }

        private static string GetDefaultSystemPrompt()
        {
            return @"You are Claude, an AI assistant integrated into the Unity Editor for game development.

You help developers with:
- Writing and explaining C# code for Unity
- Debugging issues and suggesting fixes
- Explaining Unity concepts and best practices
- Reviewing code for performance and correctness
- Suggesting architectural improvements

When providing code:
- Use proper C# conventions and Unity patterns
- Include relevant using statements
- Add XML documentation comments for public APIs
- Consider Unity's main thread requirements

Be concise and practical. Focus on actionable solutions.";
        }
    }
}
