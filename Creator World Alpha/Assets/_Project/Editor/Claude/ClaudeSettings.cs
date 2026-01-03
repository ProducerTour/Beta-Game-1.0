using UnityEngine;
using UnityEditor;
using System.IO;

namespace CreatorWorld.Editor.Claude
{
    /// <summary>
    /// Stores Claude API settings. Create via Assets > Create > Creator World > Claude Settings
    /// </summary>
    public class ClaudeSettings : ScriptableObject
    {
        private const string SettingsPath = "Assets/_Project/Editor/Claude/ClaudeSettings.asset";

        [Header("API Configuration")]
        [Tooltip("Your Anthropic API key. Get one at https://console.anthropic.com/")]
        [SerializeField] private string apiKey = "";

        [Tooltip("The Claude model to use")]
        [SerializeField] private string model = "claude-sonnet-4-20250514";

        [Header("Request Settings")]
        [Tooltip("Maximum tokens in the response")]
        [SerializeField] private int maxTokens = 4096;

        [Tooltip("Request timeout in seconds")]
        [SerializeField] private int timeoutSeconds = 60;

        [Header("Context Settings")]
        [Tooltip("Include selected code in context")]
        [SerializeField] private bool includeSelection = true;

        [Tooltip("Include project structure in context")]
        [SerializeField] private bool includeProjectContext = true;

        public string ApiKey => apiKey;
        public string Model => model;
        public int MaxTokens => maxTokens;
        public int TimeoutSeconds => timeoutSeconds;
        public bool IncludeSelection => includeSelection;
        public bool IncludeProjectContext => includeProjectContext;

        private static ClaudeSettings _instance;

        public static ClaudeSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetDatabase.LoadAssetAtPath<ClaudeSettings>(SettingsPath);

                    if (_instance == null)
                    {
                        _instance = CreateInstance<ClaudeSettings>();

                        string directory = Path.GetDirectoryName(SettingsPath);
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        AssetDatabase.CreateAsset(_instance, SettingsPath);
                        AssetDatabase.SaveAssets();
                        Debug.Log($"Created Claude Settings at {SettingsPath}");
                    }
                }
                return _instance;
            }
        }

        public bool HasValidApiKey()
        {
            return !string.IsNullOrEmpty(apiKey) && apiKey.StartsWith("sk-");
        }

        [MenuItem("Assets/Create/Creator World/Claude Settings")]
        public static void CreateSettingsAsset()
        {
            var settings = Instance;
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Tools/Claude Assistant/Settings")]
        public static void OpenSettings()
        {
            Selection.activeObject = Instance;
            EditorGUIUtility.PingObject(Instance);
        }
    }
}
