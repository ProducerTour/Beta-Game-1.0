using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace CreatorWorld.Editor.Claude
{
    /// <summary>
    /// Unity Editor window for interacting with Claude AI
    /// </summary>
    public class ClaudeAssistantWindow : EditorWindow
    {
        private string _inputText = "";
        private Vector2 _chatScrollPosition;
        private Vector2 _inputScrollPosition;
        private bool _isLoading;
        private List<ChatEntry> _chatHistory = new List<ChatEntry>();

        private GUIStyle _userMessageStyle;
        private GUIStyle _assistantMessageStyle;
        private GUIStyle _codeBlockStyle;
        private GUIStyle _inputStyle;
        private bool _stylesInitialized;

        private class ChatEntry
        {
            public string Role;
            public string Content;
            public DateTime Timestamp;
        }

        [MenuItem("Tools/Claude Assistant/Open Chat %#c")]
        public static void ShowWindow()
        {
            var window = GetWindow<ClaudeAssistantWindow>();
            window.titleContent = new GUIContent("Claude Assistant", EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image);
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _userMessageStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(40, 4, 4, 4),
                wordWrap = true,
                richText = true,
                fontSize = 12
            };

            _assistantMessageStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(4, 40, 4, 4),
                wordWrap = true,
                richText = true,
                fontSize = 12
            };

            _codeBlockStyle = new GUIStyle(EditorStyles.textArea)
            {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(4, 4, 4, 4),
                font = Font.CreateDynamicFontFromOSFont("Menlo", 11),
                wordWrap = false,
                richText = false
            };

            _inputStyle = new GUIStyle(EditorStyles.textArea)
            {
                padding = new RectOffset(8, 8, 8, 8),
                wordWrap = true,
                fontSize = 12
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            DrawToolbar();
            DrawChatArea();
            DrawInputArea();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _chatHistory.Clear();
            }

            GUILayout.FlexibleSpace();

            var settings = ClaudeSettings.Instance;
            if (!settings.HasValidApiKey())
            {
                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
                if (GUILayout.Button("Configure API Key", EditorStyles.toolbarButton))
                {
                    ClaudeSettings.OpenSettings();
                }
                GUI.backgroundColor = prevColor;
            }
            else
            {
                EditorGUILayout.LabelField($"Model: {settings.Model}", EditorStyles.miniLabel, GUILayout.Width(180));
            }

            if (GUILayout.Button(EditorGUIUtility.IconContent("d_Settings"), EditorStyles.toolbarButton, GUILayout.Width(28)))
            {
                ClaudeSettings.OpenSettings();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawChatArea()
        {
            _chatScrollPosition = EditorGUILayout.BeginScrollView(
                _chatScrollPosition,
                GUILayout.ExpandHeight(true));

            foreach (var entry in _chatHistory)
            {
                DrawChatEntry(entry);
            }

            if (_isLoading)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Claude is thinking...", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawChatEntry(ChatEntry entry)
        {
            bool isUser = entry.Role == "user";
            var style = isUser ? _userMessageStyle : _assistantMessageStyle;

            EditorGUILayout.BeginVertical(style);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                isUser ? "You" : "Claude",
                EditorStyles.boldLabel,
                GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                entry.Timestamp.ToString("HH:mm"),
                EditorStyles.miniLabel,
                GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Content - handle code blocks
            DrawFormattedContent(entry.Content);

            EditorGUILayout.EndVertical();
        }

        private void DrawFormattedContent(string content)
        {
            var parts = ParseContent(content);

            foreach (var part in parts)
            {
                if (part.IsCode)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    if (!string.IsNullOrEmpty(part.Language))
                    {
                        EditorGUILayout.LabelField(part.Language, EditorStyles.miniLabel);
                    }

                    // Code block with copy button
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.SelectableLabel(part.Content, _codeBlockStyle,
                        GUILayout.Height(CalculateCodeHeight(part.Content)));

                    if (GUILayout.Button("Copy", GUILayout.Width(50), GUILayout.Height(20)))
                    {
                        EditorGUIUtility.systemCopyBuffer = part.Content;
                        Debug.Log("Code copied to clipboard");
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.LabelField(part.Content, EditorStyles.wordWrappedLabel);
                }
            }
        }

        private float CalculateCodeHeight(string code)
        {
            int lineCount = code.Split('\n').Length;
            return Mathf.Min(lineCount * 14f + 16f, 300f);
        }

        private class ContentPart
        {
            public string Content;
            public bool IsCode;
            public string Language;
        }

        private List<ContentPart> ParseContent(string content)
        {
            var parts = new List<ContentPart>();
            int currentIndex = 0;

            while (currentIndex < content.Length)
            {
                int codeStart = content.IndexOf("```", currentIndex);

                if (codeStart == -1)
                {
                    // No more code blocks
                    string remaining = content.Substring(currentIndex);
                    if (!string.IsNullOrWhiteSpace(remaining))
                    {
                        parts.Add(new ContentPart { Content = remaining, IsCode = false });
                    }
                    break;
                }

                // Add text before code block
                if (codeStart > currentIndex)
                {
                    string textBefore = content.Substring(currentIndex, codeStart - currentIndex);
                    if (!string.IsNullOrWhiteSpace(textBefore))
                    {
                        parts.Add(new ContentPart { Content = textBefore.Trim(), IsCode = false });
                    }
                }

                // Find end of code block
                int languageEnd = content.IndexOf('\n', codeStart + 3);
                int codeEnd = content.IndexOf("```", codeStart + 3);

                if (codeEnd == -1)
                {
                    // Unclosed code block - treat rest as code
                    string code = content.Substring(codeStart + 3);
                    parts.Add(new ContentPart { Content = code, IsCode = true });
                    break;
                }

                string language = "";
                string codeContent;

                if (languageEnd != -1 && languageEnd < codeEnd)
                {
                    language = content.Substring(codeStart + 3, languageEnd - codeStart - 3).Trim();
                    codeContent = content.Substring(languageEnd + 1, codeEnd - languageEnd - 1).Trim();
                }
                else
                {
                    codeContent = content.Substring(codeStart + 3, codeEnd - codeStart - 3).Trim();
                }

                parts.Add(new ContentPart
                {
                    Content = codeContent,
                    IsCode = true,
                    Language = language
                });

                currentIndex = codeEnd + 3;
            }

            return parts;
        }

        private void DrawInputArea()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Context info
            var settings = ClaudeSettings.Instance;
            if (settings.IncludeSelection && Selection.activeObject != null)
            {
                EditorGUILayout.LabelField(
                    $"Context: {Selection.activeObject.name}",
                    EditorStyles.miniLabel);
            }

            // Input text area
            _inputScrollPosition = EditorGUILayout.BeginScrollView(
                _inputScrollPosition,
                GUILayout.Height(80));

            _inputText = EditorGUILayout.TextArea(_inputText, _inputStyle, GUILayout.ExpandHeight(true));

            EditorGUILayout.EndScrollView();

            // Send button row
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(_isLoading || string.IsNullOrWhiteSpace(_inputText));
            if (GUILayout.Button("Send (Ctrl+Enter)", GUILayout.Width(120), GUILayout.Height(28)))
            {
                SendMessage();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // Handle keyboard shortcut
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.control && e.keyCode == KeyCode.Return)
            {
                if (!_isLoading && !string.IsNullOrWhiteSpace(_inputText))
                {
                    SendMessage();
                    e.Use();
                }
            }
        }

        private async void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(_inputText)) return;

            string userMessage = _inputText;
            _inputText = "";

            // Add context if available
            string contextInfo = GetContextInfo();
            if (!string.IsNullOrEmpty(contextInfo))
            {
                userMessage = $"{contextInfo}\n\n{userMessage}";
            }

            _chatHistory.Add(new ChatEntry
            {
                Role = "user",
                Content = userMessage,
                Timestamp = DateTime.Now
            });

            _isLoading = true;
            Repaint();

            try
            {
                var messages = new List<ClaudeAPI.ChatMessage>();

                // Include recent chat history (last 10 messages)
                int startIndex = Mathf.Max(0, _chatHistory.Count - 10);
                for (int i = startIndex; i < _chatHistory.Count; i++)
                {
                    messages.Add(new ClaudeAPI.ChatMessage(
                        _chatHistory[i].Role,
                        _chatHistory[i].Content));
                }

                var response = await ClaudeAPI.SendMessageAsync(messages);

                if (response.Success)
                {
                    _chatHistory.Add(new ChatEntry
                    {
                        Role = "assistant",
                        Content = response.Content,
                        Timestamp = DateTime.Now
                    });

                    Debug.Log($"Claude: {response.InputTokens} input / {response.OutputTokens} output tokens");
                }
                else
                {
                    _chatHistory.Add(new ChatEntry
                    {
                        Role = "assistant",
                        Content = $"Error: {response.Error}",
                        Timestamp = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                _chatHistory.Add(new ChatEntry
                {
                    Role = "assistant",
                    Content = $"Exception: {ex.Message}",
                    Timestamp = DateTime.Now
                });
            }
            finally
            {
                _isLoading = false;
                Repaint();

                // Scroll to bottom
                _chatScrollPosition.y = float.MaxValue;
            }
        }

        private string GetContextInfo()
        {
            var settings = ClaudeSettings.Instance;
            if (!settings.IncludeSelection) return "";

            var sb = new StringBuilder();

            // Include selected script content
            if (Selection.activeObject is MonoScript script)
            {
                string scriptPath = AssetDatabase.GetAssetPath(script);
                string scriptContent = System.IO.File.ReadAllText(scriptPath);

                sb.AppendLine("[Selected Script]");
                sb.AppendLine($"File: {scriptPath}");
                sb.AppendLine("```csharp");
                sb.AppendLine(scriptContent);
                sb.AppendLine("```");
            }
            else if (Selection.activeObject is TextAsset textAsset)
            {
                sb.AppendLine("[Selected Text Asset]");
                sb.AppendLine($"File: {AssetDatabase.GetAssetPath(textAsset)}");
                sb.AppendLine("```");
                sb.AppendLine(textAsset.text);
                sb.AppendLine("```");
            }
            else if (Selection.activeGameObject != null)
            {
                var go = Selection.activeGameObject;
                sb.AppendLine("[Selected GameObject]");
                sb.AppendLine($"Name: {go.name}");
                sb.AppendLine("Components:");
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component != null)
                    {
                        sb.AppendLine($"  - {component.GetType().Name}");
                    }
                }
            }

            return sb.ToString();
        }
    }
}
