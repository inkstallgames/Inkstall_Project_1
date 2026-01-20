using Fusion;
using System.Collections.Generic;

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
        // Ensure message is not null and within size limits
        if (message.Value == null || message.Value.Length > 100) // Reduced from 128 to 100 to leave room for player name and other data
        {
            Debug.LogWarning("Chat message is too long or null. Max length is 100 characters.");
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
