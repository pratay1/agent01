using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public class MCTSNode
{
    public int VisitCount { get; set; }
    public double ValueSum { get; set; }
    public double Prior { get; set; }
    public Dictionary<string, MCTSNode> Children { get; } = new();
    public bool Expanded { get; set; }
}

public class MCTSEngine
{
    private NeuralNetwork network;
    private double cpuct = 1.25;
    private int numSimulations = 800;
    private int temperature = 0;
    private Random random = new Random();

    public MCTSEngine(NeuralNetwork network, int simCount = 800, int temp = 0)
    {
        this.network = network;
        this.numSimulations = simCount;
        this.temperature = temp;
    }

    public void SetSimulations(int count) => numSimulations = count;
    public void SetTemperature(int temp) => temperature = temp;

    public string Search(BoardState board, int timeLimitMs = 0, CancellationToken? cancellationToken = null)
    {
        var root = new MCTSNode();

        var legalMoves = MoveGenerator.GenerateLegalMoves(board);
        if (legalMoves.Count == 0)
            return "";

        foreach (var move in legalMoves)
        {
            root.Children[move] = new MCTSNode { Prior = 1.0 / legalMoves.Count };
        }

        DateTime startTime = DateTime.Now;
        int simulationCount = 0;

        while (simulationCount < numSimulations)
        {
            if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested)
                break;
            
            if (timeLimitMs > 0 && (DateTime.Now - startTime).TotalMilliseconds >= timeLimitMs)
                break;

            if (simulationCount >= numSimulations)
                break;

            RunSimulation(board.Copy(), root);
            simulationCount++;
        }

        if (temperature == 0 || root.Children.Count == 0)
        {
            var best = root.Children.OrderByDescending(c => c.Value.VisitCount).First();
            return best.Key;
        }
        else
        {
            var visits = root.Children.Select(c => (c.Key, Count: Math.Pow(c.Value.VisitCount, 1.0 / temperature))).ToList();
            var total = visits.Sum(v => v.Count);
            var probs = visits.Select(v => v.Count / total).ToArray();
            var idx = random.Next(probs.Length);
            return visits[idx].Key;
        }
    }

    private void RunSimulation(BoardState board, MCTSNode node)
    {
        var path = new List<MCTSNode> { node };
        var movePath = new List<string>();

        while (node.Expanded && !board.IsGameOver())
        {
            var bestChild = SelectChild(node, board);
            if (bestChild.Key == null)
                break;

            var move = board.ParseUci(bestChild.Key);
            if (move == null)
                break;

            board.MakeMove(move.Value);
            path.Add(bestChild.Value);
            movePath.Add(bestChild.Key);
            node = bestChild.Value;
        }

        double value;
        if (!board.IsGameOver())
        {
            var tensor = board.ToTensor();
            var (policyLogits, eval) = network.RunInference(tensor);

            var legalMoves = MoveGenerator.GenerateLegalMoves(board);
            var policy = new double[4672];
            double sum = 0;
            foreach (var move in legalMoves)
            {
                int idx = MoveToIndex(move, board);
                if (idx >= 0 && idx < 4672)
                {
                    policy[idx] = Math.Exp(policyLogits[idx]);
                    sum += policy[idx];
                }
            }
            if (sum > 0)
            {
                for (int i = 0; i < policy.Length; i++)
                    policy[i] /= sum;
            }

            node.Expanded = true;
            foreach (var move in legalMoves)
            {
                int idx = MoveToIndex(move, board);
                if (idx >= 0 && idx < 4672)
                {
                    node.Children[move] = new MCTSNode { Prior = policy[idx] };
                }
            }

            value = eval;
        }
        else
        {
            var winner = board.GetWinner();
            if (winner == 0)
                value = 1.0;
            else if (winner == 1)
                value = -1.0;
            else
                value = 0.0;
        }

        double sign = 1.0;
        for (int i = path.Count - 1; i >= 0; i--)
        {
            path[i].VisitCount++;
            path[i].ValueSum += sign * value;
            sign *= -1.0;
        }
    }

    private KeyValuePair<string, MCTSNode> SelectChild(MCTSNode node, BoardState board)
    {
        if (node.Children.Count == 0)
            return new KeyValuePair<string, MCTSNode>(null, null);

        string bestMove = null;
        double bestValue = double.NegativeInfinity;

        foreach (var child in node.Children)
        {
            double q = child.Value.VisitCount > 0 ? child.Value.ValueSum / child.Value.VisitCount : 0;
            double u = cpuct * child.Value.Prior * Math.Sqrt(node.VisitCount) / (1 + child.Value.VisitCount);
            double value = q + u;

            if (value > bestValue)
            {
                bestValue = value;
                bestMove = child.Key;
            }
        }

        if (bestMove == null)
            bestMove = node.Children.Keys.First();

        return new KeyValuePair<string, MCTSNode>(bestMove, node.Children[bestMove]);
    }

    private int MoveToIndex(string uciMove, BoardState board)
    {
        var move = board.ParseUci(uciMove);
        if (move == null) return -1;

        int from = move.Value.From;
        int to = move.Value.To;

        int fromRow = 7 - (from / 8);
        int fromCol = from % 8;
        int toRow = 7 - (to / 8);
        int toCol = to % 8;

        int deltaRow = toRow - fromRow;
        int deltaCol = toCol - fromCol;

        int moveType;
        if (!move.Value.Promotion.HasValue)
        {
            if (deltaRow == 0 && deltaCol > 0) moveType = 0;
            else if (deltaRow == 0 && deltaCol < 0) moveType = 1;
            else if (deltaRow < 0 && deltaCol == 0) moveType = 2;
            else if (deltaRow > 0 && deltaCol == 0) moveType = 3;
            else if (deltaRow < 0 && deltaCol == deltaRow) moveType = 4;
            else if (deltaRow < 0 && deltaCol == -deltaRow) moveType = 5;
            else if (deltaRow > 0 && deltaCol == deltaRow) moveType = 6;
            else if (deltaRow > 0 && deltaCol == -deltaRow) moveType = 7;
            else
            {
                var dirs = new (int, int)[] { (-2, -1), (-2, 1), (-1, -2), (-1, 2), (1, -2), (1, 2), (2, -1), (2, 1) };
                moveType = 8 + Array.FindIndex(dirs, d => d == (deltaRow, deltaCol));
            }
        }
        else
        {
            int promo = move.Value.Promotion.Value;
            if (deltaCol == 0) moveType = 56 + promo;
            else if (deltaRow == 0) moveType = 64 + promo;
            else moveType = 56 + promo;
        }

        return from * 73 + moveType;
    }
}