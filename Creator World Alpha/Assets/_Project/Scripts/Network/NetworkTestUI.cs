using Unity.Netcode;
using UnityEngine;

namespace CreatorWorld.Network
{
    /// <summary>
    /// Simple UI for testing multiplayer connections.
    /// Supports both local testing and Unity Relay for remote testing.
    /// </summary>
    public class NetworkTestUI : MonoBehaviour
    {
        private string joinCode = "";
        private string statusMessage = "";
        private bool showRelayOptions = false;

        private void Start()
        {
            // Subscribe to SessionGameBridge events if available
            if (SessionGameBridge.Instance != null)
            {
                SessionGameBridge.Instance.OnSessionCreated += code =>
                {
                    statusMessage = $"Session created! Code: {code}";
                };
                SessionGameBridge.Instance.OnSessionJoined += () =>
                {
                    statusMessage = "Joined session!";
                };
                SessionGameBridge.Instance.OnError += error =>
                {
                    statusMessage = $"Error: {error}";
                };
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 350, 450));

            if (NetworkManager.Singleton == null)
            {
                GUILayout.Label("NetworkManager not found!", GUI.skin.box);
                GUILayout.EndArea();
                return;
            }

            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                DrawConnectionMenu();
            }
            else
            {
                DrawConnectedStatus();
            }

            // Status message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(10);
                GUILayout.Label(statusMessage, GUI.skin.box);
            }

            GUILayout.EndArea();
        }

        private void DrawConnectionMenu()
        {
            GUILayout.Label("MULTIPLAYER TEST", GUI.skin.box);
            GUILayout.Space(5);

            // Toggle between local and relay
            showRelayOptions = GUILayout.Toggle(showRelayOptions, "Use Unity Relay (for remote friends)");
            GUILayout.Space(10);

            if (showRelayOptions)
            {
                DrawRelayOptions();
            }
            else
            {
                DrawLocalOptions();
            }
        }

        private void DrawLocalOptions()
        {
            GUILayout.Label("LOCAL TESTING (Same Network)", GUI.skin.box);
            GUILayout.Space(5);

            if (GUILayout.Button("Host (Server + Client)", GUILayout.Height(40)))
            {
                statusMessage = "Starting host...";
                NetworkManager.Singleton.StartHost();
                statusMessage = "Host started!";
            }

            if (GUILayout.Button("Server Only", GUILayout.Height(40)))
            {
                statusMessage = "Starting server...";
                NetworkManager.Singleton.StartServer();
                statusMessage = "Server started!";
            }

            if (GUILayout.Button("Client (Connect to localhost)", GUILayout.Height(40)))
            {
                statusMessage = "Connecting to localhost...";
                NetworkManager.Singleton.StartClient();
            }
        }

        private void DrawRelayOptions()
        {
            GUILayout.Label("RELAY (Different Networks)", GUI.skin.box);
            GUILayout.Space(5);

            var bridge = SessionGameBridge.Instance;

            if (bridge == null)
            {
                GUILayout.Label("SessionGameBridge not found!");
                GUILayout.Label("Add it to your scene.");
                return;
            }

            // Show initialization status
            if (!bridge.IsInitialized)
            {
                GUILayout.Label("Unity Services: Not initialized");
                if (GUILayout.Button("Initialize Services", GUILayout.Height(40)))
                {
                    _ = bridge.InitializeServices();
                    statusMessage = "Initializing...";
                }
                return;
            }

            GUILayout.Label("Unity Services: Ready", GUI.skin.box);
            GUILayout.Space(5);

            // Create Session
            if (GUILayout.Button("Create Session (Host)", GUILayout.Height(40)))
            {
                statusMessage = "Creating session...";
                _ = bridge.CreateSession();
            }

            GUILayout.Space(10);

            // Join Session
            GUILayout.Label("Join with Code:");
            joinCode = GUILayout.TextField(joinCode, GUILayout.Height(30));

            if (GUILayout.Button("Join Session", GUILayout.Height(40)))
            {
                if (string.IsNullOrEmpty(joinCode))
                {
                    statusMessage = "Enter a join code!";
                }
                else
                {
                    statusMessage = $"Joining {joinCode}...";
                    _ = bridge.JoinSession(joinCode);
                }
            }

            GUILayout.Space(10);

            // Quick Join
            if (GUILayout.Button("Quick Join (Auto-match)", GUILayout.Height(40)))
            {
                statusMessage = "Quick joining...";
                _ = bridge.QuickJoin();
            }
        }

        private void DrawConnectedStatus()
        {
            string mode = NetworkManager.Singleton.IsHost ? "Host" :
                          NetworkManager.Singleton.IsServer ? "Server" : "Client";

            GUILayout.Label($"CONNECTED AS: {mode}", GUI.skin.box);
            GUILayout.Space(5);

            GUILayout.Label($"Connected clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");

            if (NetworkManager.Singleton.LocalClient != null)
            {
                GUILayout.Label($"My Client ID: {NetworkManager.Singleton.LocalClientId}");
            }

            // Show join code if we have one
            var bridge = SessionGameBridge.Instance;
            if (bridge != null && !string.IsNullOrEmpty(bridge.CurrentSessionCode))
            {
                GUILayout.Space(10);
                GUILayout.Label("JOIN CODE:", GUI.skin.box);
                GUILayout.TextField(bridge.CurrentSessionCode, GUILayout.Height(30));
                GUILayout.Label("(Share this with friends!)");
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Disconnect", GUILayout.Height(40)))
            {
                if (bridge != null && bridge.IsInSession)
                {
                    _ = bridge.LeaveSession();
                }
                else
                {
                    NetworkManager.Singleton.Shutdown();
                }
                statusMessage = "Disconnected";
            }
        }
    }
}
