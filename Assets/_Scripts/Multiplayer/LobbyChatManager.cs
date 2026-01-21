using Fusion;
using System.Collections.Generic;
using UnityEngine;

// A struct to hold the data for a single chat message.
public struct ChatMessage : INetworkStruct
{
    public NetworkString<_32> PlayerName;
    public NetworkString<_128> Message;
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
    public void RPC_SendChatMessage(NetworkString<_128> message, RpcInfo info = default)
    {
        // Safety check - ensure we have a valid message
        if (string.IsNullOrEmpty(message.Value))
        {
            Debug.LogWarning("Received empty chat message");
            return;
        }

        // Limit message length to prevent RPC size issues
        const int MAX_MESSAGE_LENGTH = 100; // Conservative limit to stay under RPC size limit
        NetworkString<_128> safeMessage = message;
        if (message.Length > MAX_MESSAGE_LENGTH)
        {
            Debug.LogWarning($"Truncating long message from {message.Length} to {MAX_MESSAGE_LENGTH} characters");
            safeMessage = message.Value.Substring(0, MAX_MESSAGE_LENGTH);
        }

        // Clean up old messages if needed
        while (Messages.Count >= 30)
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

        // Add the new message
        Messages.Add(new ChatMessage
        {
            PlayerName = playerName,
            Message = safeMessage
        });
    }
}
