using System;
using System.IO;

public class Evaluator
{
    private NeuralNetwork network;

    public Evaluator(NeuralNetwork network)
    {
        this.network = network;
    }

    public (float[] policy, float value) Evaluate(BoardState board)
    {
        var tensor = board.ToTensor();
        return network.RunInference(tensor);
    }

    public string GetEvaluationString(BoardState board, int depth, long nodes, long nps, int timeMs)
    {
        var (policy, value) = Evaluate(board);
        
        int centipawns = (int)(value * 100);
        string scoreStr = $"cp {centipawns}";

        if (value > 0.9) scoreStr = "mate 10";
        else if (value < -0.9) scoreStr = "mate -10";

        return $"info depth {depth} score {scoreStr} nodes {nodes} nps {nps} time {timeMs}";
    }

    public static string FormatPv(BoardState board, string bestMove)
    {
        return $"pv {bestMove}";
    }
}