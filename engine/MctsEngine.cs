using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public class MCTSNode
{
    public int VisitCount { get; set; }
    public double ValueSum { get; set; }
    public double Prior { get; set; }
    public Dictionary<int, MCTSNode> Children { get; } = new Dictionary<int, MCTSNode>();
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
        var legalMoves = board.GenerateLegalMoves();
        if (legalMoves.Count == 0)
            return "0000";

        // Initialize root children with move indices
        foreach (var move in legalMoves)
        {
            int idx = MoveEncoder.MoveToIndex(move, board);
            if (idx >= 0)
                root.Children[idx] = new MCTSNode { Prior = 1.0 / legalMoves.Count };
        }

        DateTime startTime = DateTime.Now;
        int simulations = 0;

        while (true)
        {
            if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested) break;
            if (timeLimitMs > 0 && (DateTime.Now - startTime).TotalMilliseconds >= timeLimitMs) break;
            if (simulations >= numSimulations) break;

            RunSimulation(board.Copy(), root);
            simulations++;
        }

        if (root.Children.Count == 0)
            return board.ToUci(legalMoves[0]);

        // Select best move
        if (temperature == 0)
        {
            int bestIdx = root.Children.OrderByDescending(kv => kv.Value.VisitCount).First().Key;
            var bestMove = MoveEncoder.IndexToMove(bestIdx, board);
            return bestMove.HasValue ? board.ToUci(bestMove.Value) : board.ToUci(legalMoves[0]);
        }
        else
        {
            var visits = root.Children.Select(kv => (Idx: kv.Key, Count: Math.Pow(kv.Value.VisitCount + 1e-6, 1.0 / temperature))).ToList();
            double total = visits.Sum(v => v.Count);
            if (total <= 0) return board.ToUci(legalMoves[random.Next(legalMoves.Count)]);

            double r = random.NextDouble() * total;
            double cumulative = 0;
            foreach (var v in visits)
            {
                cumulative += v.Count;
                if (r <= cumulative)
                {
                    var move = MoveEncoder.IndexToMove(v.Idx, board);
                    return move.HasValue ? board.ToUci(move.Value) : board.ToUci(legalMoves[0]);
                }
            }
            var lastMove = MoveEncoder.IndexToMove(visits.Last().Idx, board);
            return lastMove.HasValue ? board.ToUci(lastMove.Value) : board.ToUci(legalMoves[0]);
        }
    }

    private void RunSimulation(BoardState board, MCTSNode root)
    {
        var path = new List<MCTSNode>();
        MCTSNode node = root;
        path.Add(node);

        // SELECTION
        while (node.Expanded && !board.IsGameOver())
        {
            // Select best child based on PUCT
            int bestIdx = -1;
            MCTSNode? bestChild = null;
            double bestScore = double.NegativeInfinity;

            foreach (var kv in node.Children)
            {
                int idx = kv.Key;
                var child = kv.Value;
                double q = child.VisitCount > 0 ? child.ValueSum / child.VisitCount : 0;
                double u = cpuct * child.Prior * Math.Sqrt(node.VisitCount) / (1 + child.VisitCount);
                double score = q + u;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = idx;
                    bestChild = child;
                }
            }

            if (bestIdx == -1 || bestChild == null)
                break;

            var move = MoveEncoder.IndexToMove(bestIdx, board);
            if (move == null)
                break;

            board.MakeMove(move.Value);
            node = bestChild;
            path.Add(node);
        }

        // Ensure leaf node (current node) is in path
        if (!path.Contains(node))
            path.Add(node);

        // EXPANSION + EVALUATION
        double value;
        if (!board.IsGameOver())
        {
            var tensor = board.ToTensor();
            var (policyLogits, eval) = network.RunInference(tensor);

            var legalMoves = board.GenerateLegalMoves();
            double[] policy = new double[4672];
            double sum = 0;

            foreach (var move in legalMoves)
            {
                int idx = MoveEncoder.MoveToIndex(move, board);
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
                foreach (var move in legalMoves)
                {
                    int idx = MoveEncoder.MoveToIndex(move, board);
                    if (idx >= 0 && idx < 4672)
                        policy[idx] = 1.0 / legalMoves.Count;
                }
            }

            node.Expanded = true;
            foreach (var move in legalMoves)
            {
                int idx = MoveEncoder.MoveToIndex(move, board);
                if (idx >= 0)
                    node.Children[idx] = new MCTSNode { Prior = policy[idx] };
            }

            value = eval;
        }
        else
        {
            // Terminal value: -1 if current player is checkmated, 0 for draw
            if (board.IsInCheck(board.Turn))
                value = -1.0;
            else
                value = 0.0;
        }

        // BACKPROPAGATION
        double sign = 1.0;
        for (int i = path.Count - 1; i >= 0; i--)
        {
            var n = path[i];
            n.VisitCount++;
            n.ValueSum += sign * value;
            sign *= -1.0;
        }
    }
}
