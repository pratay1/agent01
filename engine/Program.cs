using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string baseDir = @"C:\Users\prata\agent01";
        string modelPath = Path.Combine(baseDir, "exports", "model.onnx");

        if (!File.Exists(modelPath))
        {
            Console.Error.WriteLine($"Error: Model file not found at {modelPath}");
            Console.Error.WriteLine("Please train the model first by running 'python train.py' and pressing Ctrl+C.");
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