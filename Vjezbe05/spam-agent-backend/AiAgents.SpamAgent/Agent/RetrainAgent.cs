/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - RETRAIN AGENT
 * ═══════════════════════════════════════════════════════════════════════════════
 * 
 * Ovaj agent implementira Sense → Think → Act → Learn ciklus za auto-retrain:
 * 
 *   Sense:  Provjeri NewGoldSinceLastTrain counter
 *   Think:  Odluči da li treba retrenirati (counter >= threshold)
 *   Act:    Ako da, pokreni trening novog modela
 *   Learn:  Resetuj counter, logiraj rezultate
 * 
 * Ovaj agent se pokreće periodično ili nakon svake batch review sesije.
 */

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiAgents.Core;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Infrastructure;
using AiAgents.SpamAgent.Application.Services;
using System.Linq;

namespace AiAgents.SpamAgent.Agent;

// ════════════════════════════════════════════════════════════════════════════════
//                     TIPOVI ZA RETRAIN AGENT
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Stanje koje agent opaža - broj novih gold labela.
/// </summary>
public record RetrainState(
    int NewGoldCount,
    int Threshold,
    bool AutoRetrainEnabled,
    int? CurrentModelVersion
);

/// <summary>
/// Odluka da li retrenirati.
/// </summary>
public record RetrainDecision(
    bool ShouldRetrain,
    TrainTemplate Template,
    string Reason
);

/// <summary>
/// Rezultat retraininga.
/// </summary>
public record RetrainResult(
    bool Success,
    int? NewModelVersion,
    double? Accuracy,
    string Message
);

/// <summary>
/// Iskustvo za učenje - šta se desilo.
/// </summary>
public record RetrainExperience(
    RetrainState StateBefore,
    RetrainDecision Decision,
    RetrainResult Result
);

// ════════════════════════════════════════════════════════════════════════════════
//                     KOMPONENTE AGENTA
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Percepcija: čita gold counter iz baze.
/// </summary>
public class RetrainStatePerception : IPerceptionSource<RetrainState>
{
    private readonly SpamAgentDbContext _context;

    public RetrainStatePerception(SpamAgentDbContext context)
    {
        _context = context;
    }

    public RetrainState? Observe()
    {
        var settings = _context.SystemSettings
            .Include(s => s.ActiveModelVersion)
            .FirstOrDefault();

        if (settings == null) return null;

        return new RetrainState(
            settings.NewGoldSinceLastTrain,
            settings.RetrainGoldThreshold,
            settings.AutoRetrainEnabled,
            settings.ActiveModelVersion?.Version
        );
    }

    public bool HasNext => true; // Uvijek može provjeriti
}

/// <summary>
/// Politika: odlučuje da li retrenirati.
/// </summary>
public class RetrainPolicy : IPolicy<RetrainState, RetrainDecision>
{
    private readonly TrainTemplate _defaultTemplate;

    public RetrainPolicy(TrainTemplate defaultTemplate = TrainTemplate.Medium)
    {
        _defaultTemplate = defaultTemplate;
    }

    public RetrainDecision SelectAction(RetrainState state)
    {
        if (!state.AutoRetrainEnabled)
        {
            return new RetrainDecision(false, _defaultTemplate, "Auto-retrain je isključen.");
        }

        if (state.NewGoldCount < state.Threshold)
        {
            return new RetrainDecision(
                false, 
                _defaultTemplate, 
                $"Nedovoljno gold labela: {state.NewGoldCount}/{state.Threshold}"
            );
        }

        return new RetrainDecision(
            true,
            _defaultTemplate,
            $"Dostignuto {state.NewGoldCount} gold labela (threshold: {state.Threshold})"
        );
    }
}

/// <summary>
/// Aktuator: pokreće trening ako je odlučeno.
/// </summary>
public class RetrainActuator : IActuator<RetrainDecision, RetrainResult>
{
    private readonly TrainingService _trainingService;

    public RetrainActuator(TrainingService trainingService)
    {
        _trainingService = trainingService;
    }

    public RetrainResult Execute(RetrainDecision decision)
    {
        if (!decision.ShouldRetrain)
        {
            return new RetrainResult(true, null, null, decision.Reason);
        }

        try
        {
            // Sinhrono za kompatibilnost sa interfejsom
            var trainTask = _trainingService.TrainModelAsync(decision.Template, activate: true);
            trainTask.Wait();
            var model = trainTask.Result;

            return new RetrainResult(
                true,
                model.Version,
                model.Accuracy,
                $"Novi model v{model.Version} treniran i aktiviran. Accuracy: {model.Accuracy:P2}"
            );
        }
        catch (Exception ex)
        {
            return new RetrainResult(false, null, null, $"Greška pri treningu: {ex.Message}");
        }
    }
}

/// <summary>
/// Learner: logira rezultate retraininga.
/// </summary>
public class RetrainLearner : ILearningComponent<RetrainExperience>
{
    public event Action<RetrainExperience>? OnRetrainCompleted;

    public void Learn(RetrainExperience experience)
    {
        // Ovdje bi se moglo:
        // - Logirati u fajl
        // - Slati notifikacije
        // - Ažurirati dashboard metrike

        OnRetrainCompleted?.Invoke(experience);
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     RETRAIN AGENT
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Agent koji automatski retrenira model kad se nakupi dovoljno gold labela.
/// </summary>
public class RetrainAgent : SoftwareAgent<RetrainState, RetrainDecision, RetrainResult, RetrainExperience>
{
    private readonly RetrainLearner _learner;

    public RetrainAgent(
        RetrainStatePerception perception,
        RetrainPolicy policy,
        RetrainActuator actuator,
        RetrainLearner learner)
        : base(
            perception: perception,
            policy: policy,
            actuator: actuator,
            experienceBuilder: (state, decision, result) => 
                new RetrainExperience(state, decision, result),
            learner: learner,
            goalChecker: null  // Ovaj agent nema "cilj" - radi periodično
        )
    {
        _learner = learner;
    }

    /// <summary>
    /// Event koji se okida kad se završi retrain.
    /// </summary>
    public event Action<RetrainExperience>? OnRetrainCompleted
    {
        add => _learner.OnRetrainCompleted += value;
        remove => _learner.OnRetrainCompleted -= value;
    }

    /// <summary>
    /// Izvršava provjeru i eventualni retrain.
    /// </summary>
    public RetrainResult CheckAndRetrain()
    {
        var state = _perception.Observe();
        if (state == null)
        {
            return new RetrainResult(false, null, null, "Nije moguće pročitati stanje.");
        }

        var decision = _policy.SelectAction(state);
        var result = _actuator.Execute(decision);

        if (_learner != null && _experienceBuilder != null)
        {
            var experience = _experienceBuilder(state, decision, result);
            _learner.Learn(experience);
        }

        return result;
    }
}
