using Fusion;
using System.Collections.Generic;
using UnityEngine;

// A struct to hold the data for a single chat message.
public struct ChatMessage : INetworkStruct
{
    public NetworkString<_32> PlayerName;
    public NetworkString<_64> Message; // Reduced from _128 to _64 to prevent RPC size issues
}

public class LobbyChatManager : NetworkBehaviour
{
    [Networked, Capacity(30)]
    private NetworkLinkedList<ChatMessage> Messages { get; }

    public override void Render()
    {
        base.Render();
        if (LobbyUIManager.Instance != null)
        {
            LobbyUIManager.Instance.UpdateChat(Messages);
        }
    }

    // RPC for clients to send a message to the host.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SendChatMessage(NetworkString<_64> message, RpcInfo info = default)
    {
        // Validate message is not empty
        if (string.IsNullOrEmpty(message.Value))
        {
            Debug.LogWarning("Attempted to send empty chat message");
            return;
        }

        if (Messages.Count >= 30)
        {
            // Remove the oldest message if the log is full.
            foreach (var msg in Messages)
            {
                Messages.Remove(msg);
                break; // Exit after removing the first element
            }
        }

        // Get the sender's name from the lobby manager.
        string playerName = "Player";
        if (NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.LobbyPlayers.ContainsKey(info.Source))
        {
            playerName = NetworkLobbyManager.Instance.LobbyPlayers[info.Source].PlayerName.ToString();
        }

        Messages.Add(new ChatMessage
        {
            PlayerName = playerName,
            Message = message
        });
    }
}
