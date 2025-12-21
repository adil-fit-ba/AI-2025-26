/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT WEB - SIGNALR HUB
 * ═══════════════════════════════════════════════════════════════════════════════
 * 
 * Real-time komunikacija sa klijentima.
 * 
 * GRUPE:
 *   - "messages": MessageQueued, MessageScored, MessageMoved
 *   - "models": ModelRetrained, ModelActivated
 *   - "stats": StatsUpdated (periodično)
 * 
 * Klijent se može subscribati na grupe pozivom:
 *   - JoinGroup("messages")
 *   - LeaveGroup("messages")
 */

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using AiAgents.SpamAgent.Web.Models;

namespace AiAgents.SpamAgent.Web.Hubs;

public class SpamAgentHub : Hub
{
    /// <summary>
    /// Klijent se pridružuje grupi za primanje određenih event-a.
    /// </summary>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("Joined", groupName);
    }

    /// <summary>
    /// Klijent napušta grupu.
    /// </summary>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("Left", groupName);
    }

    /// <summary>
    /// Ping za provjeru konekcije.
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }

    public override async Task OnConnectedAsync()
    {
        // Automatski pridruži svim grupama (za jednostavnost)
        await Groups.AddToGroupAsync(Context.ConnectionId, "messages");
        await Groups.AddToGroupAsync(Context.ConnectionId, "models");
        await Groups.AddToGroupAsync(Context.ConnectionId, "stats");
        
        await base.OnConnectedAsync();
    }
}

/// <summary>
/// Extension metode za slanje event-a kroz hub.
/// </summary>
public static class SpamAgentHubExtensions
{
    public static async Task SendMessageQueued(
        this IHubContext<SpamAgentHub> hub, 
        MessageQueuedEvent evt)
    {
        await hub.Clients.Group("messages").SendAsync("MessageQueued", evt);
    }

    public static async Task SendMessageScored(
        this IHubContext<SpamAgentHub> hub, 
        MessageScoredEvent evt)
    {
        await hub.Clients.Group("messages").SendAsync("MessageScored", evt);
    }

    public static async Task SendMessageMoved(
        this IHubContext<SpamAgentHub> hub, 
        MessageMovedEvent evt)
    {
        await hub.Clients.Group("messages").SendAsync("MessageMoved", evt);
    }

    public static async Task SendModelRetrained(
        this IHubContext<SpamAgentHub> hub, 
        ModelRetrainedEvent evt)
    {
        await hub.Clients.Group("models").SendAsync("ModelRetrained", evt);
    }

    public static async Task SendModelActivated(
        this IHubContext<SpamAgentHub> hub, 
        int modelVersion)
    {
        await hub.Clients.Group("models").SendAsync("ModelActivated", new { Version = modelVersion, Timestamp = DateTime.UtcNow });
    }

    public static async Task SendStatsUpdated(
        this IHubContext<SpamAgentHub> hub, 
        StatsUpdatedEvent evt)
    {
        await hub.Clients.Group("stats").SendAsync("StatsUpdated", evt);
    }
}
