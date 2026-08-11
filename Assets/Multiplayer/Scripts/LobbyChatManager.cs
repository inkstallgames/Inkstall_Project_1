using Fusion;
using System.Collections.Generic;
using UnityEngine;

// A struct to hold the data for a single chat message.
public struct ChatMessage : INetworkStruct
{
    public NetworkString<_32> PlayerName;
    public NetworkString<_128> Message;
    [Networked] public Color PlayerColor { get; set; }
}

public class LobbyChatManager : NetworkBehaviour
{
    [Networked, Capacity(30)]
    private NetworkLinkedList<ChatMessage> Messages { get; }

    private int _lastDisplayedCount = -1;

    public override void Render()
    {
        base.Render();
        if (LobbyUIManager.Instance == null) return;

        // Only rebuild TMP when the message list actually changes
        int count = Messages.Count;
        if (count == _lastDisplayedCount) return;
        _lastDisplayedCount = count;

        LobbyUIManager.Instance.UpdateChat(Messages);
    }

    // RPC for clients to send a message to the host.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SendChatMessage(string message, RpcInfo info = default)
    {
        // Safety check - ensure we have a valid message
        if (string.IsNullOrEmpty(message))
        {
            // Debug.LogWarning("Received empty chat message");
            return;
        }

        // Limit message length to prevent RPC size issues
        const int MAX_MESSAGE_LENGTH = 80; // Reduced further to account for RPC overhead
        if (message.Length > MAX_MESSAGE_LENGTH)
        {
            // Debug.LogWarning($"Truncating long message from {message.Length} to {MAX_MESSAGE_LENGTH} characters");
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

        // Get the sender's name and color from the lobby manager.
        string playerName = PlayerPrefs.GetString("PlayerName", "Player");
        Color playerColor = Color.white;
        
        PlayerRef sender = info.Source;
        if (sender == PlayerRef.None)
        {
            sender = Runner.LocalPlayer;
        }

        if (NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.LobbyPlayers.ContainsKey(sender))
        {
            var playerData = NetworkLobbyManager.Instance.LobbyPlayers[sender];
            playerName = playerData.PlayerName.ToString();
            playerColor = playerData.PlayerColor;
        }
        else
        {
            // Debug.LogWarning($"[LobbyChatManager] Could not find player data for sender {sender}. Using default name.");
        }

        // Add the new message
        var chatMessage = new ChatMessage
        {
            PlayerName = playerName,
            Message = message,
            PlayerColor = playerColor
        };
        
        Messages.Add(chatMessage);
        
        // Debug the message size for troubleshooting
        //// Debug.Log($"Added message from {playerName}. Length: {message?.Length ?? 0}");
    }
}
