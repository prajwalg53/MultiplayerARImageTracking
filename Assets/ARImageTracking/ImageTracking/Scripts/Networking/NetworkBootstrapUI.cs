using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Minimal direct-IP connection UI so the same build can be run as host or client on a LAN.
/// This is intentionally basic: swap in Unity Relay/Lobby later without touching gameplay code,
/// since everything else only depends on NetworkManager being started.
/// </summary>
public class NetworkBootstrapUI : MonoBehaviour
{
    [SerializeField] NetworkManager m_NetworkManager;
    [SerializeField] ushort m_Port = 7777;

    string m_JoinAddress = "127.0.0.1";

    void Awake()
    {
        if (m_NetworkManager == null)
        {
            m_NetworkManager = NetworkManager.Singleton;
        }
    }

    void OnGUI()
    {
        if (m_NetworkManager == null)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(20, 20, 260, 160), GUI.skin.box);

        if (!m_NetworkManager.IsClient && !m_NetworkManager.IsServer)
        {
            GUILayout.Label("Server IP");
            m_JoinAddress = GUILayout.TextField(m_JoinAddress);

            if (GUILayout.Button("Host"))
            {
                SetConnectionData(m_JoinAddress);
                m_NetworkManager.StartHost();
            }

            if (GUILayout.Button("Join as Client"))
            {
                SetConnectionData(m_JoinAddress);
                m_NetworkManager.StartClient();
            }
        }
        else
        {
            string role = m_NetworkManager.IsHost ? "Host" : m_NetworkManager.IsServer ? "Server" : "Client";
            GUILayout.Label($"Connected as {role}");
            GUILayout.Label($"Clients: {m_NetworkManager.ConnectedClients.Count}");

            if (GUILayout.Button("Disconnect"))
            {
                m_NetworkManager.Shutdown();
            }
        }

        GUILayout.EndArea();
    }

    void SetConnectionData(string address)
    {
        var transport = m_NetworkManager.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport != null)
        {
            transport.SetConnectionData(address, m_Port);
        }
    }
}
