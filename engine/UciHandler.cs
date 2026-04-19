using System;
using System.IO;
using System.Linq;
using System.Threading;

public class UciHandler
{
    private BoardState board;
    private MCTSEngine engine;
    private Evaluator evaluator;

    private int numSimulations = 800;
    private int temperature = 0;

    private bool isSearching = false;
    private CancellationTokenSource? searchCts;

    public UciHandler(NeuralNetwork network)
    {
        board = new BoardState();
        engine = new MCTSEngine(network, numSimulations, temperature);
        evaluator = new Evaluator(network);
    }

    public void Handle(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        switch (parts[0].ToLower())
        {
            case "uci":
                Console.WriteLine("id name ChessAI");
                Console.WriteLine("id author prata");
                Console.WriteLine("option name SimCount type spin default 800 min 1 max 100000");
                Console.WriteLine("option name Temperature type spin default 0 min 0 max 10");
                Console.WriteLine("uciok");
                Console.Out.Flush();
                break;

            case "isready":
                Console.WriteLine("readyok");
                Console.Out.Flush();
                break;

            case "ucinewgame":
                board = new BoardState();
                break;

            case "position":
                HandlePosition(parts);
                break;

            case "go":
                HandleGo(parts);
                break;

            case "stop":
                HandleStop();
                break;

            case "quit":
                HandleStop();
                Environment.Exit(0);
                break;

            case "setoption":
                HandleSetOption(parts);
                break;
        }
    }

    private void HandlePosition(string[] parts)
    {
        if (parts.Length < 2) return;

        if (parts[1] == "startpos")
        {
            board = new BoardState();

            int moveIndex = Array.IndexOf(parts, "moves");
            if (moveIndex != -1)
            {
                for (int i = moveIndex + 1; i < parts.Length; i++)
                    ApplyMove(parts[i]);
            }
        }
        else if (parts[1] == "fen")
        {
            int moveIndex = Array.IndexOf(parts, "moves");
            string fen = moveIndex == -1
                ? string.Join(" ", parts.Skip(2))
                : string.Join(" ", parts.Skip(2).Take(moveIndex - 2));

            board = new BoardState(fen);

            if (moveIndex != -1)
            {
                for (int i = moveIndex + 1; i < parts.Length; i++)
                    ApplyMove(parts[i]);
            }
        }
    }

    private void ApplyMove(string uciMove)
    {
        var move = board.ParseUci(uciMove);
        if (move != null)
            board.MakeMove(move.Value);
    }

    private void HandleGo(string[] parts)
    {
        if (isSearching)
            HandleStop();

        int movetime = 0;
        int wtime = 0, btime = 0, winc = 0, binc = 0;
        bool infinite = false;

        for (int i = 1; i < parts.Length; i++)
        {
            switch (parts[i])
            {
                case "movetime":
                    movetime = int.Parse(parts[++i]);
                    break;
                case "wtime":
                    wtime = int.Parse(parts[++i]);
                    break;
                case "btime":
                    btime = int.Parse(parts[++i]);
                    break;
                case "winc":
                    winc = int.Parse(parts[++i]);
                    break;
                case "binc":
                    binc = int.Parse(parts[++i]);
                    break;
                case "infinite":
                    infinite = true;
                    break;
            }
        }

        if (!infinite && wtime > 0 && btime > 0)
        {
            int myTime = board.Turn ? wtime : btime;
            int inc = board.Turn ? winc : binc;

            int calculated = myTime / 30 + inc;
            movetime = Math.Max(10, Math.Min(calculated, myTime - 100));
        }

        if (movetime <= 0)
            movetime = 1000; // safe fallback (1 second)

        StartSearch(movetime);
    }

    private void StartSearch(int timeLimitMs)
    {
        isSearching = true;
        searchCts = new CancellationTokenSource();

        try
        {
            DateTime start = DateTime.Now;

            string bestMove = engine.Search(board, timeLimitMs, searchCts.Token);

            int elapsed = (int)(DateTime.Now - start).TotalMilliseconds;

            if (string.IsNullOrWhiteSpace(bestMove))
                bestMove = "0000"; // emergency fallback

            LogSearch(bestMove, elapsed);

            Console.WriteLine($"bestmove {bestMove}");
            Console.Out.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine("info string ERROR: " + ex.Message);
            Console.WriteLine("bestmove 0000");
            Console.Out.Flush();
        }

        isSearching = false;
    }

    private void HandleStop()
    {
        searchCts?.Cancel();
    }

    private void HandleSetOption(string[] parts)
    {
        for (int i = 0; i < parts.Length - 2; i++)
        {
            if (parts[i] == "name")
            {
                string name = parts[i + 1].ToLower();

                if (name == "simcount")
                {
                    numSimulations = int.Parse(parts[^1]);
                    engine.SetSimulations(numSimulations);
                }
                else if (name == "temperature")
                {
                    temperature = int.Parse(parts[^1]);
                    engine.SetTemperature(temperature);
                }
            }
        }
    }

    private void LogSearch(string bestMove, int timeMs)
    {
        try
        {
            string logPath = @"C:\Users\prata\agent01\logs\engine_log.txt";

            var (policy, value) = evaluator.Evaluate(board);

            string entry =
                $"[{DateTime.Now}] FEN: {board.GetFen()} | Move: {bestMove} | Eval: {value:F3} | Time: {timeMs}ms\n";

            File.AppendAllText(logPath, entry);
        }
        catch { }
    }
}