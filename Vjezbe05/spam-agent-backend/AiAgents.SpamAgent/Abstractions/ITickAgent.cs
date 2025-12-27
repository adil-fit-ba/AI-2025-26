/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - PRODUKCIJSKI INTERFEJSI ZA AGENTE
 * ═══════════════════════════════════════════════════════════════════════════════
 * 
 * Ovi interfejsi definiraju "tick-based" agente za produkcijsku upotrebu.
 * 
 * RAZLIKA OD AiAgents.Core.IAgent:
 * 
 *   IAgent.Step()              → sync, edukativni, za demo primjere
 *   ITickAgent.TickAsync()     → async, produkcijski, za BackgroundService
 * 
 * "Tick" predstavlja jednu iteraciju host loop-a:
 *   - Može vratiti null (nema posla)
 *   - Podržava CancellationToken
 *   - Vraća rezultat koji host može emitovati (SignalR, logging...)
 */

using System.Threading;
using System.Threading.Tasks;

namespace AiAgents.SpamAgent.Abstractions;

/// <summary>
/// Produkcijski agent sa async tick-om.
/// Jedan tick = jedan ciklus Sense → Think → Act.
/// </summary>
/// <typeparam name="TResult">Tip rezultata jednog tick-a (null = nema posla)</typeparam>
public interface ITickAgent<TResult> where TResult : class
{
    /// <summary>
    /// Izvršava jedan tick agent ciklusa.
    /// </summary>
    /// <param name="ct">Cancellation token za graceful shutdown</param>
    /// <returns>Rezultat tick-a, ili null ako nema posla</returns>
    Task<TResult?> TickAsync(CancellationToken ct = default);
}

/// <summary>
/// Agent koji može biti "nespreman" za rad.
/// Npr. scoring agent bez aktivnog ML modela.
/// </summary>
public interface IReadyCheckAgent
{
    /// <summary>
    /// Provjerava da li je agent spreman za rad.
    /// </summary>
    Task<bool> IsReadyAsync(CancellationToken ct = default);
}

/// <summary>
/// Kombinacija tick agenta sa ready check-om.
/// Većina produkcijskih agenata implementira ovo.
/// </summary>
/// <typeparam name="TResult">Tip rezultata jednog tick-a</typeparam>
public interface IProductionAgent<TResult> : ITickAgent<TResult>, IReadyCheckAgent
    where TResult : class
{
}
