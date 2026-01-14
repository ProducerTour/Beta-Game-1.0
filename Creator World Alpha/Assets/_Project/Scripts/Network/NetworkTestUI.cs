using Unity.Netcode;
using UnityEngine;

namespace CreatorWorld.Network
{
    /// <summary>
    /// Simple UI for testing multiplayer connections.
    /// </summary>
    public class NetworkTestUI : MonoBehaviour
    {
        private string joinCode = "";
        private string statusMessage = "Not connected";
        private bool servicesInitialized = false;

        private void OnGUI()
        {
            // Make text bigger and easier to read
            GUI.skin.label.fontSize = 16;
            GUI.skin.button.fontSize = 16;
            GUI.skin.textField.fontSize = 16;
            GUI.skin.box.fontSize = 16;

            GUILayout.BeginArea(new Rect(10, 10, 400, 500));

            if (NetworkManager.Singleton == null)
            {
                GUILayout.Label("ERROR: NetworkManager not found!", GUI.skin.box);
                GUILayout.EndArea();
                return;
            }

            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                DrawMainMenu();
            }
            else
            {
                DrawConnectedStatus();
            }

            GUILayout.EndArea();
        }

        private void DrawMainMenu()
        {
            GUILayout.Label("=== MULTIPLAYER TEST ===", GUI.skin.box);
            GUILayout.Space(10);

            // Status
            GUILayout.Label($"Status: {statusMessage}");
            GUILayout.Space(10);

            // Step 1: Initialize
            GUILayout.Label("STEP 1: Initialize Services");
            if (GUILayout.Button(servicesInitialized ? "Services Ready ✓" : "Initialize Services", GUILayout.Height(50)))
            {
                InitializeServices();
            }
            GUILayout.Space(20);

            // Step 2: Host or Join
            if (servicesInitialized)
            {
                GUILayout.Label("STEP 2: Host or Join");

                if (GUILayout.Button("CREATE SESSION (You are Host)", GUILayout.Height(50)))
                {
                    CreateSession();
                }

                GUILayout.Space(10);
                GUILayout.Label("- OR -");
                GUILayout.Space(10);

                GUILayout.Label("Enter friend's join code:");
                joinCode = GUILayout.TextField(joinCode, GUILayout.Height(40));

                if (GUILayout.Button("JOIN SESSION (Connect to friend)", GUILayout.Height(50)))
                {
                    JoinSession();
                }
            }
            else
            {
                GUILayout.Label("(Initialize services first)");
            }

            GUILayout.Space(20);
            GUILayout.Label("--- LOCAL TESTING ---");
            if (GUILayout.Button("Host (Local Only)", GUILayout.Height(40)))
            {
                NetworkManager.Singleton.StartHost();
                statusMessage = "Running as local host";
            }
        }

        private void DrawConnectedStatus()
        {
            string mode = NetworkManager.Singleton.IsHost ? "HOST" :
                          NetworkManager.Singleton.IsServer ? "SERVER" : "CLIENT";

            GUILayout.Label($"=== CONNECTED AS {mode} ===", GUI.skin.box);
            GUILayout.Space(10);

            GUILayout.Label($"Connected players: {NetworkManager.Singleton.ConnectedClientsIds.Count}");
            GUILayout.Label($"My Client ID: {NetworkManager.Singleton.LocalClientId}");

            // Show join code if available
            var bridge = SessionGameBridge.Instance;
            if (bridge != null && !string.IsNullOrEmpty(bridge.CurrentSessionCode))
            {
                GUILayout.Space(10);
                GUILayout.Label("=== JOIN CODE ===", GUI.skin.box);
                GUILayout.TextField(bridge.CurrentSessionCode, GUILayout.Height(40));
                GUILayout.Label("Share this code with your friend!");
            }

            GUILayout.Space(20);

            if (GUILayout.Button("DISCONNECT", GUILayout.Height(50)))
            {
                Disconnect();
            }
        }

        private async void InitializeServices()
        {
            var bridge = SessionGameBridge.Instance;
            if (bridge == null)
            {
                statusMessage = "ERROR: Add SessionGameBridge to scene!";
                return;
            }

            statusMessage = "Initializing...";
            await bridge.InitializeServices();

            if (bridge.IsInitialized)
            {
                servicesInitialized = true;
                statusMessage = "Services ready! Create or join a session.";
            }
            else
            {
                statusMessage = "Failed to initialize. Check console.";
            }
        }

        private async void CreateSession()
        {
            var bridge = SessionGameBridge.Instance;
            if (bridge == null) return;

            statusMessage = "Creating session...";
            var code = await bridge.CreateSession();

            if (!string.IsNullOrEmpty(code))
            {
                statusMessage = $"Session created! Code: {code}";
            }
            else
            {
                statusMessage = "Failed to create session. Check console.";
            }
        }

        private async void JoinSession()
        {
            if (string.IsNullOrEmpty(joinCode))
            {
                statusMessage = "Enter a join code first!";
                return;
            }

            var bridge = SessionGameBridge.Instance;
            if (bridge == null) return;

            statusMessage = $"Joining {joinCode}...";
            var success = await bridge.JoinSession(joinCode.Trim().ToUpper());

            if (success)
            {
                statusMessage = "Joined successfully!";
            }
            else
            {
                statusMessage = "Failed to join. Check code and try again.";
            }
        }

        private async void Disconnect()
        {
            var bridge = SessionGameBridge.Instance;
            if (bridge != null && bridge.IsInSession)
            {
                await bridge.LeaveSession();
            }
            else
            {
                NetworkManager.Singleton.Shutdown();
            }
            statusMessage = "Disconnected";
        }
    }
}
