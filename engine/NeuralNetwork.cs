using System;
using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

public class NeuralNetwork : IDisposable
{
    private InferenceSession session;
    private string modelPath;

    public NeuralNetwork(string onnxModelPath)
    {
        modelPath = onnxModelPath;
        
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Model file not found: {modelPath}");
        }

        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        
        session = new InferenceSession(modelPath, sessionOptions);
    }

    public (float[] policyLogits, float value) RunInference(float[] boardTensor)
    {
        var inputTensor = new DenseTensor<float>(new[] { 1, 19, 8, 8 });
        
        for (int i = 0; i < boardTensor.Length && i < 19 * 8 * 8; i++)
        {
            inputTensor[0, i / 64, (i % 64) / 8, i % 8] = boardTensor[i];
        }

        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor("board_input", inputTensor)
        };

        using var results = session.Run(inputs);

        var policyLogits = results[0].AsTensor<float>().ToArray();
        var value = results[1].AsTensor<float>().ToArray();

        return (policyLogits, value[0]);
    }

    public void Dispose()
    {
        session?.Dispose();
    }
}