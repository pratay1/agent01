using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;

        // Try candidate paths: relative to executable at various depths, and absolute known location
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "..", "..", "exports", "model.onnx")), // up to repo root
            Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "..", "exports", "model.onnx")), // up to engine\.. (repo root if engine is directly under repo)
            Path.GetFullPath(Path.Combine(exeDir, "..", "exports", "model.onnx")), // simple one-level up
            @"C:\Users\prata\agent01\exports\model.onnx" // absolute fallback
        };

        string modelPath = null;
        foreach (var cand in candidates)
        {
            if (File.Exists(cand))
            {
                modelPath = cand;
                break;
            }
        }

        if (modelPath == null)
        {
            Console.Error.WriteLine("Error: Model file not found in expected locations:");
            foreach (var cand in candidates)
                Console.Error.WriteLine($"  {cand}");
            Console.Error.WriteLine("Please train the model first by running 'python master.py'.");
            Environment.Exit(1);
        }

        NeuralNetwork network;
        try
        {
            network = new NeuralNetwork(modelPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading ONNX model: {ex.Message}");
            Environment.Exit(1);
            return;
        }

        var uciHandler = new UciHandler(network);

        while (true)
        {
            string? line = Console.ReadLine();
            if (line == null) break;
            
            try
            {
                uciHandler.Handle(line);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing command: {ex.Message}");
            }
        }

        network.Dispose();
    }
}