using Fusion;
using System.Collections.Generic;
using UnityEngine;

// A struct to hold the data for a single chat message.
public struct ChatMessage : INetworkStruct
{
    public NetworkString<_32> PlayerName;  // Using _32 as it's a common Fusion type
    public NetworkString<_128> Message;    // Using _128 as it's a common Fusion type
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
    [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable, InvokeLocal = false)]
    public void RPC_SendChatMessage(NetworkString<_128> message, RpcInfo info = default)
    {
        // Ensure message is not null and within size limits
        const int MAX_MESSAGE_LENGTH = 100; // Reduced from 128 to be safe
        if (message.Value == null || message.Value.Length > MAX_MESSAGE_LENGTH)
        {
            Debug.LogWarning($"Chat message is too long or null. Length: {message.Value?.Length ?? 0}, Max: {MAX_MESSAGE_LENGTH}");
            return;
        }
        
        // Log message size for debugging
        Debug.Log($"Processing chat message. Length: {message.Value.Length}, Content: {message.Value}");

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
