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
    public void RPC_SendChatMessage(string message, RpcInfo info = default)
    {
        // Safety check - ensure we have a valid message
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("Received empty chat message");
            return;
        }

        // Limit message length to prevent RPC size issues
        const int MAX_MESSAGE_LENGTH = 80;
        if (message.Length > MAX_MESSAGE_LENGTH)
        {
            Debug.LogWarning($"Truncating long message from {message.Length} to {MAX_MESSAGE_LENGTH} characters");
            message = message.Substring(0, MAX_MESSAGE_LENGTH);
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

        // Get the sender's name from the lobby manager
        string playerName = "Player";
        
        // First try to get the name from the player's local preferences
        if (info.Source == Runner.LocalPlayer)
        {
            playerName = PlayerPrefs.GetString("PlayerName", "Player");
        }
        
        // Then try to get it from the lobby players list
        if (NetworkLobbyManager.Instance != null)
        {
            // First check if the player is in the lobby
            if (NetworkLobbyManager.Instance.LobbyPlayers.ContainsKey(info.Source))
            {
                playerName = NetworkLobbyManager.Instance.LobbyPlayers[info.Source].PlayerName.ToString();
            }
            // If not found, try to find by player ID in the runner
            else if (Runner.TryGetPlayerObject(info.Source, out var networkObject) && 
                    networkObject != null && 
                    networkObject.TryGetComponent<PlayerNetworkData>(out var playerData))
            {
                playerName = playerData.PlayerName;
            }
        }
        
        Debug.Log($"[Chat] Message from {playerName} (Player {info.Source.PlayerId}): {message}");

        // Add the new message
        // Create the network message with the truncated string
        var chatMessage = new ChatMessage
        {
            PlayerName = playerName,
            Message = message
        };
        
        Messages.Add(chatMessage);
        
        // Debug the message size for troubleshooting
        //Debug.Log($"Added message from {playerName}. Length: {message?.Length ?? 0}");
    }
}
