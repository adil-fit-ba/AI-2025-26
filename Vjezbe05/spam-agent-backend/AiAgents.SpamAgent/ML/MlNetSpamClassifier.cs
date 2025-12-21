/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - ML.NET IMPLEMENTACIJA
 * ═══════════════════════════════════════════════════════════════════════════════
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace AiAgents.SpamAgent.ML;

/// <summary>
/// ML.NET implementacija spam klasifikatora.
/// Koristi SDCA Logistic Regression sa FeaturizeText.
/// </summary>
public class MlNetSpamClassifier : ISpamClassifier
{
    private readonly MLContext _mlContext;
    private ITransformer? _model;
    private PredictionEngine<SmsInput, SmsPrediction>? _predictionEngine;

    public bool IsModelLoaded => _model != null;

    public MlNetSpamClassifier(int? seed = 42)
    {
        _mlContext = new MLContext(seed);
    }

    public Task<string> TrainAsync(IEnumerable<TrainingSample> trainingData, string modelPath)
    {
        // Konvertuj u ML.NET format
        var data = trainingData.Select(s => new SmsInput
        {
            Text = s.Text,
            Label = s.IsSpam
        }).ToList();

        var trainData = _mlContext.Data.LoadFromEnumerable(data);

        // Pipeline: Text → Features → SDCA Logistic Regression
        var pipeline = _mlContext.Transforms.Text
            .FeaturizeText("Features", nameof(SmsInput.Text))
            .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: nameof(SmsInput.Label),
                featureColumnName: "Features"));

        // Treniraj
        _model = pipeline.Fit(trainData);
        _predictionEngine = _mlContext.Model.CreatePredictionEngine<SmsInput, SmsPrediction>(_model);

        // Sačuvaj model
        var directory = Path.GetDirectoryName(modelPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _mlContext.Model.Save(_model, trainData.Schema, modelPath);

        return Task.FromResult(modelPath);
    }

    public Task<EvaluationMetrics> EvaluateAsync(IEnumerable<TrainingSample> validationData)
    {
        if (_model == null)
        {
            throw new InvalidOperationException("Model nije učitan. Pozovite LoadModelAsync ili TrainAsync prvo.");
        }

        var data = validationData.Select(s => new SmsInput
        {
            Text = s.Text,
            Label = s.IsSpam
        }).ToList();

        var testData = _mlContext.Data.LoadFromEnumerable(data);
        var predictions = _model.Transform(testData);

        var metrics = _mlContext.BinaryClassification.Evaluate(predictions, 
            labelColumnName: nameof(SmsInput.Label));

        // Izračunaj confusion matrix ručno za TP, TN, FP, FN
        var predictionsList = _mlContext.Data
            .CreateEnumerable<SmsPrediction>(predictions, reuseRowObject: false)
            .ToList();

        int tp = 0, tn = 0, fp = 0, fn = 0;
        for (int i = 0; i < data.Count; i++)
        {
            var actual = data[i].Label;
            var predicted = predictionsList[i].PredictedLabel;

            if (actual && predicted) tp++;
            else if (!actual && !predicted) tn++;
            else if (!actual && predicted) fp++;
            else fn++;
        }

        return Task.FromResult(new EvaluationMetrics
        {
            Accuracy = metrics.Accuracy,
            Precision = metrics.PositivePrecision,
            Recall = metrics.PositiveRecall,
            F1Score = metrics.F1Score,
            AreaUnderRocCurve = metrics.AreaUnderRocCurve,
            TruePositives = tp,
            TrueNegatives = tn,
            FalsePositives = fp,
            FalseNegatives = fn
        });
    }

    public Task LoadModelAsync(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Model fajl nije pronađen: {modelPath}");
        }

        DataViewSchema schema;
        _model = _mlContext.Model.Load(modelPath, out schema);
        _predictionEngine = _mlContext.Model.CreatePredictionEngine<SmsInput, SmsPrediction>(_model);

        return Task.CompletedTask;
    }

    public Task<double> PredictAsync(string text)
    {
        if (_predictionEngine == null)
        {
            throw new InvalidOperationException("Model nije učitan.");
        }

        var input = new SmsInput { Text = text };
        var prediction = _predictionEngine.Predict(input);

        return Task.FromResult((double)prediction.Probability);
    }

    public Task<IList<double>> PredictBatchAsync(IEnumerable<string> texts)
    {
        if (_predictionEngine == null)
        {
            throw new InvalidOperationException("Model nije učitan.");
        }

        var results = new List<double>();
        foreach (var text in texts)
        {
            var input = new SmsInput { Text = text };
            var prediction = _predictionEngine.Predict(input);
            results.Add(prediction.Probability);
        }

        return Task.FromResult<IList<double>>(results);
    }

    // ML.NET input/output klase
    private class SmsInput
    {
        public string Text { get; set; } = string.Empty;
        public bool Label { get; set; }
    }

    private class SmsPrediction
    {
        [ColumnName("PredictedLabel")]
        public bool PredictedLabel { get; set; }

        [ColumnName("Probability")]
        public float Probability { get; set; }

        [ColumnName("Score")]
        public float Score { get; set; }
    }
}
