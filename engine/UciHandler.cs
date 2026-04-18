using System;
using System.IO;
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

        var command = parts[0].ToLower();

        switch (command)
        {
            case "uci":
                HandleUci();
                break;
            case "isready":
                Console.WriteLine("readyok");
                break;
            case "ucinewgame":
                HandleUciNewGame();
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
                HandleQuit();
                break;
            case "setoption":
                HandleSetOption(parts);
                break;
        }
    }

    private void HandleUci()
    {
        Console.WriteLine("id name ChessAI");
        Console.WriteLine("id author prata");
        Console.WriteLine("option name SimCount type default 800");
        Console.WriteLine("option name Temperature type default 0");
        Console.WriteLine("uciok");
    }

    private void HandleUciNewGame()
    {
        board = new BoardState();
    }

    private void HandlePosition(string[] parts)
    {
        if (parts.Length < 2) return;

        if (parts[1].ToLower() == "startpos")
        {
            board = new BoardState();
            if (parts.Length > 2 && parts[2].ToLower() == "moves")
            {
                for (int i = 2; i < parts.Length; i++)
                {
                    if (parts[i].ToLower() == "moves") continue;
                    ApplyMove(parts[i]);
                }
            }
        }
        else if (parts[1].ToLower() == "fen")
        {
            int fenStart = 2;
            int fenEnd = Array.IndexOf(parts, "moves");
            if (fenEnd == -1) fenEnd = parts.Length;
            
            string fen = string.Join(" ", parts.Skip(fenStart).Take(fenEnd - fenStart));
            board = new BoardState(fen);

            if (fenEnd < parts.Length)
            {
                for (int i = fenEnd + 1; i < parts.Length; i++)
                {
                    ApplyMove(parts[i]);
                }
            }
        }
    }

    private void ApplyMove(string uciMove)
    {
        var move = board.ParseUci(uciMove);
        if (move != null)
        {
            board.MakeMove(move.Value);
        }
    }

    private void HandleGo(string[] parts)
    {
        if (isSearching)
        {
            HandleStop();
        }

        int movetime = 0;
        int wtime = 0, btime = 0, winc = 0, binc = 0;
        bool infinite = false;

        for (int i = 1; i < parts.Length; i++)
        {
            switch (parts[i].ToLower())
            {
                case "movetime":
                    if (i + 1 < parts.Length)
                        movetime = int.Parse(parts[i + 1]);
                    break;
                case "wtime":
                    if (i + 1 < parts.Length)
                        wtime = int.Parse(parts[i + 1]);
                    break;
                case "btime":
                    if (i + 1 < parts.Length)
                        btime = int.Parse(parts[i + 1]);
                    break;
                case "winc":
                    if (i + 1 < parts.Length)
                        winc = int.Parse(parts[i + 1]);
                    break;
                case "binc":
                    if (i + 1 < parts.Length)
                        binc = int.Parse(parts[i + 1]);
                    break;
                case "infinite":
                    infinite = true;
                    break;
            }
        }

        if (wtime > 0 && btime > 0 && !infinite)
        {
            int movesToGo = 30;
            int myTime = board.Turn ? wtime : btime;
            int inc = board.Turn ? winc : binc;
            movetime = Math.Min(myTime / movesToGo + inc, myTime - 1000);
        }

        if (movetime == 0) movetime = 30000;
        movetime = Math.Min(movetime, 30000);

        StartSearch(movetime);
    }

    private void StartSearch(int timeLimitMs)
    {
        isSearching = true;
        searchCts = new CancellationTokenSource();

        Thread searchThread = new Thread(() =>
        {
            DateTime startTime = DateTime.Now;
            var bestMove = engine.Search(board, timeLimitMs, searchCts.Token);
            int elapsedMs = (int)(DateTime.Now - startTime).TotalMilliseconds;

            if (!searchCts.Token.IsCancellationRequested)
            {
                LogSearch(bestMove, elapsedMs);
                Console.WriteLine($"bestmove {bestMove}");
            }
            
            isSearching = false;
        });

        searchThread.IsBackground = true;
        searchThread.Start();
    }

    private void HandleStop()
    {
        if (searchCts != null)
        {
            searchCts.Cancel();
        }
    }

    private void HandleQuit()
    {
        HandleStop();
        Environment.Exit(0);
    }

    private void HandleSetOption(string[] parts)
    {
        if (parts.Length < 5) return;
        
        for (int i = 1; i < parts.Length; i++)
        {
            if (parts[i].ToLower() == "name" && i + 1 < parts.Length)
            {
                string optionName = parts[i + 1];
                if (optionName.ToLower() == "simcount" && i + 2 < parts.Length)
                {
                    numSimulations = int.Parse(parts[i + 2]);
                    engine.SetSimulations(numSimulations);
                }
                else if (optionName.ToLower() == "temperature" && i + 2 < parts.Length)
                {
                    temperature = int.Parse(parts[i + 2]);
                    engine.SetTemperature(temperature);
                }
            }
        }
    }

    private void LogSearch(string bestMove, int timeMs)
    {
        try
        {
            string logPath = Path.Combine("C:\\Users\\prata\\agent01\\logs", "engine_log.txt");
            
            var (policy, value) = evaluator.Evaluate(board);
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FEN: {board.GetFen()} | Best: {bestMove} | Value: {value:F3} | Time: {timeMs}ms\n";
            
            File.AppendAllText(logPath, logEntry);
        }
        catch
        {
        }
    }
}