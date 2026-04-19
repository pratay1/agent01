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

    public void SetSimulations(int count) => numSimulations = Math.Max(1, count);
    public void SetTemperature(int temp) => temperature = Math.Max(0, temp);

    public string Search(BoardState board, int timeLimitMs = 0, CancellationToken? cancellationToken = null)
    {
        var root = new MCTSNode();

        var legalMoves = MoveGenerator.GenerateLegalMoves(board);
        if (legalMoves.Count == 0)
            return "0000"; // ALWAYS return something

        // initialize root
        foreach (var move in legalMoves)
        {
            root.Children[move] = new MCTSNode
            {
                Prior = 1.0 / legalMoves.Count
            };
        }

        DateTime startTime = DateTime.Now;
        int simulationCount = 0;

        while (true)
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

        // SAFETY: if something went wrong
        if (root.Children.Count == 0)
            return legalMoves[0];

        // pick best move
        if (temperature == 0)
        {
            var best = root.Children
                .OrderByDescending(c => c.Value.VisitCount)
                .FirstOrDefault();

            return best.Key ?? legalMoves[0];
        }
        else
        {
            var visits = root.Children
                .Select(c => (Move: c.Key, Count: Math.Pow(c.Value.VisitCount + 1e-6, 1.0 / temperature)))
                .ToList();

            double total = visits.Sum(v => v.Count);

            if (total <= 0)
                return legalMoves[random.Next(legalMoves.Count)];

            double r = random.NextDouble() * total;
            double cumulative = 0;

            foreach (var v in visits)
            {
                cumulative += v.Count;
                if (r <= cumulative)
                    return v.Move;
            }

            return visits.Last().Move;
        }
    }

    private void RunSimulation(BoardState board, MCTSNode node)
    {
        var path = new List<MCTSNode> { node };

        // SELECTION
        while (node.Expanded && !board.IsGameOver())
        {
            var bestChild = SelectChild(node);

            if (bestChild.Key == null || bestChild.Value == null)
                break;

            var move = board.ParseUci(bestChild.Key);
            if (move == null)
                break;

            board.MakeMove(move.Value);
            node = bestChild.Value;
            path.Add(node);
        }

        double value;

        // EXPANSION + EVAL
        if (!board.IsGameOver())
        {
            var tensor = board.ToTensor();
            var (policyLogits, eval) = network.RunInference(tensor);

            var legalMoves = MoveGenerator.GenerateLegalMoves(board);

            double[] policy = new double[4672];
            double sum = 0;

            foreach (var move in legalMoves)
            {
                int idx = MoveToIndex(move, board);
                if (idx >= 0 && idx < 4672)
                {
                    double p = Math.Exp(policyLogits[idx]);
                    policy[idx] = p;
                    sum += p;
                }
            }

            if (sum > 0)
            {
                for (int i = 0; i < policy.Length; i++)
                    policy[i] /= sum;
            }
            else
            {
                // fallback uniform
                foreach (var move in legalMoves)
                {
                    int idx = MoveToIndex(move, board);
                    if (idx >= 0 && idx < 4672)
                        policy[idx] = 1.0 / legalMoves.Count;
                }
            }

            node.Expanded = true;

            foreach (var move in legalMoves)
            {
                int idx = MoveToIndex(move, board);
                double prior = (idx >= 0 && idx < 4672) ? policy[idx] : 0;

                node.Children[move] = new MCTSNode
                {
                    Prior = prior
                };
            }

            value = eval;
        }
        else
        {
            int winner = board.GetWinner();

            if (winner == 0) value = 1.0;
            else if (winner == 1) value = -1.0;
            else value = 0.0;
        }

        // BACKPROP
        double sign = 1.0;
        for (int i = path.Count - 1; i >= 0; i--)
        {
            var n = path[i];
            n.VisitCount++;
            n.ValueSum += sign * value;
            sign *= -1.0;
        }
    }

    private KeyValuePair<string, MCTSNode> SelectChild(MCTSNode node)
    {
        if (node.Children.Count == 0)
            return new KeyValuePair<string, MCTSNode>(null, null);

        string bestMove = null;
        double bestValue = double.NegativeInfinity;

        foreach (var child in node.Children)
        {
            var c = child.Value;

            double q = c.VisitCount > 0 ? c.ValueSum / c.VisitCount : 0;
            double u = cpuct * c.Prior * Math.Sqrt(node.VisitCount + 1) / (1 + c.VisitCount);

            double score = q + u;

            if (score > bestValue)
            {
                bestValue = score;
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
                var dirs = new (int, int)[]
                {
                    (-2, -1), (-2, 1), (-1, -2), (-1, 2),
                    (1, -2), (1, 2), (2, -1), (2, 1)
                };

                int idx = Array.FindIndex(dirs, d => d == (deltaRow, deltaCol));
                moveType = idx >= 0 ? 8 + idx : 0;
            }
        }
        else
        {
            int promo = move.Value.Promotion.Value;
            moveType = 56 + promo;
        }

        return from * 73 + moveType;
    }
}