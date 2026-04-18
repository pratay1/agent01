\# Chess AI Build Agent Prompt

\## AlphaZero-Style Self-Play Neural Network — UCI-Compatible Executable

\### Target Directory: `C:\\\\Users\\\\prata\\\\agent01`



\---



\## ABSOLUTE CONSTRAINT — READ FIRST



\*\*You are FORBIDDEN from creating any file or folder outside of `C:\\\\Users\\\\prata\\\\agent01`.\*\* This includes temp files, logs, model checkpoints, ONNX exports, binaries, build artifacts, and anything else. Every single file the project produces at runtime must be written inside `C:\\\\Users\\\\prata\\\\agent01` or a subdirectory of it. Enforce this in every file path, every `open()` call, every `torch.save()`, every dotnet publish output path, every log handler. There are no exceptions.



\---



\## Project Overview



Build a chess-playing AI that learns entirely through self-play using a neural network and Monte Carlo Tree Search (MCTS), closely mirroring the AlphaZero methodology. The project has two distinct phases:



\- \*\*Phase 1 (Python):\*\* A training engine that runs indefinitely, learning from self-play games, with a rich visual terminal dashboard. It saves a checkpoint when the user presses `Ctrl+C`.

\- \*\*Phase 2 (C#):\*\* A UCI-compatible chess engine executable (`.exe`) that loads the trained model and plays chess against other engines inside any UCI-compatible chess GUI (Arena, Cutechess, SCID, etc.).



The two languages serve distinct roles: Python for training (PyTorch ecosystem, rapid iteration), C# for the UCI binary (fast .NET runtime, native Windows executable, clean UCI I/O).



\---



\## Workspace File \& Folder Structure



All of the following must live \*\*exclusively\*\* inside `C:\\\\Users\\\\prata\\\\agent01`:



```

C:\\\\Users\\\\prata\\\\agent01\\\\

│

├── train.py                   # Main Python training entry point

├── requirements.txt           # Python dependencies

│

├── src\\\\                       # Python source modules

│   ├── \\\_\\\_init\\\_\\\_.py

│   ├── network.py             # Neural network definition (ResNet)

│   ├── mcts.py                # Monte Carlo Tree Search

│   ├── self\\\_play.py           # Self-play game generation

│   ├── trainer.py             # Training loop, optimizer, loss

│   ├── opening\\\_trainer.py     # Opening phase training logic

│   ├── board\\\_encoder.py       # Board → tensor conversion

│   ├── move\\\_encoder.py        # Move → policy index mapping

│   ├── replay\\\_buffer.py       # Experience replay buffer

│   ├── dashboard.py           # Rich terminal visual dashboard

│   └── export.py              # PyTorch → ONNX export utility

│

├── checkpoints\\\\               # Auto-created by train.py at runtime

│   ├── checkpoint\\\_latest.pt   # Always the most recent checkpoint

│   ├── checkpoint\\\_best.pt     # Best-performing checkpoint by Elo

│   └── checkpoint\\\_epoch\\\_N.pt  # Periodic snapshots every N epochs

│

├── exports\\\\                   # ONNX model exports (auto-created)

│   └── model.onnx             # Final exported model for C# engine

│

├── logs\\\\                      # Training logs (auto-created)

│   ├── training\\\_log.csv       # Per-epoch metrics

│   └── game\\\_log.jsonl         # One JSON line per self-play game

│

├── engine\\\\                    # C# chess engine project

│   ├── ChessEngine.csproj

│   ├── Program.cs             # UCI entry point + main loop

│   ├── UciHandler.cs          # Parses UCI protocol from stdin

│   ├── MctsEngine.cs          # C# MCTS using ONNX model

│   ├── BoardState.cs          # Board representation in C#

│   ├── MoveGenerator.cs       # Legal move generation in C#

│   ├── NeuralNetwork.cs       # Wraps Microsoft.ML.OnnxRuntime

│   └── Evaluator.cs           # Calls neural net, returns policy+value

│

└── build\\\\                     # Dotnet publish output (auto-created)

\&#x20;   └── ChessEngine.exe        # Final UCI-compatible executable

```



\*\*Do not create any file or folder anywhere else on the filesystem.\*\*



\---



\## Phase 1 — Python Training Engine



\### Dependencies (`requirements.txt`)



```

torch>=2.2.0

python-chess>=1.999

numpy>=1.26

rich>=13.7

tqdm>=4.66

onnx>=1.16

onnxruntime>=1.17

```



Install with: `pip install -r requirements.txt` (all inside agent01).



\---



\### Neural Network Architecture (`src/network.py`)



\- \*\*Framework:\*\* PyTorch

\- \*\*Architecture:\*\* Residual Network (ResNet), identical in spirit to AlphaZero

\- \*\*Input:\*\* 19-plane binary tensor of shape `(19, 8, 8)`:

&#x20; - Planes 0–5: Current player's pieces (P, N, B, R, Q, K)

&#x20; - Planes 6–11: Opponent's pieces (P, N, B, R, Q, K)

&#x20; - Plane 12: Current player color (all-ones or all-zeros)

&#x20; - Plane 13: Castling rights kingside current player

&#x20; - Plane 14: Castling rights queenside current player

&#x20; - Plane 15: Castling rights kingside opponent

&#x20; - Plane 16: Castling rights queenside opponent

&#x20; - Plane 17: En passant square (binary plane, one square lit)

&#x20; - Plane 18: Fifty-move rule counter (normalized 0–1)

\- \*\*Residual tower:\*\* Configurable depth, default 10 residual blocks, each with:

&#x20; - Conv2d(256, 256, 3×3, padding=1) → BatchNorm2d → ReLU

&#x20; - Conv2d(256, 256, 3×3, padding=1) → BatchNorm2d → skip-add → ReLU

\- \*\*Policy head:\*\*

&#x20; - Conv2d(256, 2, 1×1) → BatchNorm2d → ReLU → Flatten → Linear(128, 4672)

&#x20; - Output: logits over 4672 possible moves (AlphaZero move encoding)

\- \*\*Value head:\*\*

&#x20; - Conv2d(256, 1, 1×1) → BatchNorm2d → ReLU → Flatten → Linear(64, 1) → Tanh

&#x20; - Output: scalar in `\\\[-1, 1]` (win probability from current player's perspective)

\- \*\*Move encoding:\*\* 4672 = 64 squares × 73 move types (56 queen-like, 8 knight, 9 underpromotion)



The network must be fully exportable to ONNX with `torch.onnx.export()`. The input node name must be `"board\\\_input"` and outputs `"policy\\\_logits"` and `"value"`. This naming is required for the C# engine to reference them by name.



\---



\### Board Encoder (`src/board\\\_encoder.py`)



\- Accept a `python-chess` `Board` object

\- Return a `torch.FloatTensor` of shape `(1, 19, 8, 8)` (batch-ready)

\- Always encode from the \*\*current player's perspective\*\* (flip board if black to move)

\- This flipping must be consistent: the network always "sees" itself as white



\---



\### Move Encoder (`src/move\\\_encoder.py`)



\- Bidirectional mapping: `chess.Move` ↔ policy index (0–4671)

\- Implement `move\\\_to\\\_index(move, board)` → int

\- Implement `index\\\_to\\\_move(index, board)` → `chess.Move`

\- Filter illegal moves from the policy logits before MCTS uses them



\---



\### Monte Carlo Tree Search (`src/mcts.py`)



Use the PUCT variant of MCTS (same as AlphaZero):



\- \*\*UCT formula:\*\* `U(s,a) = Q(s,a) + C\\\_puct \\\* P(s,a) \\\* sqrt(N(s)) / (1 + N(s,a))`

&#x20; - `Q(s,a)`: mean value of action `a` from state `s`

&#x20; - `P(s,a)`: prior probability from the neural network policy head

&#x20; - `N(s)`: visit count of parent node

&#x20; - `N(s,a)`: visit count of child node

&#x20; - `C\\\_puct`: exploration constant, default `1.25`

\- \*\*Simulations per move:\*\* configurable, default `800` during self-play training, `200` for fast play

\- \*\*Temperature:\*\* `τ = 1.0` for the first 30 moves (sample from visit distribution), `τ → 0` (argmax) thereafter

\- \*\*Dirichlet noise:\*\* Add `Dir(α=0.3)` noise to root node priors during self-play to ensure exploration. Noise weight `ε = 0.25`

\- \*\*Virtual loss:\*\* Apply virtual loss during MCTS expansion to support future parallelism

\- \*\*Backup:\*\* Alternate sign on value backup (flip +/- at each ply since evaluation is from mover's perspective)



\---



\### Self-Play Game Generator (`src/self\\\_play.py`)



This is the core training loop. Each game proceeds as follows:



\*\*Mid-game position spawn (primary training mode):\*\*



1\. Load a completely fresh `chess.Board()`

2\. Play a random number of moves between 6 and 10 (uniformly sampled)

&#x20;  - Each random move is sampled from the full legal move list with uniform probability

&#x20;  - This ensures the network is exposed to a wide variety of mid-game positions

3\. From this spawned position, both players use the current neural network + MCTS to play out to terminal state

4\. Terminal state detection: checkmate, stalemate, threefold repetition, fifty-move rule, insufficient material

5\. Assign outcome: `+1` (current player wins), `−1` (current player loses), `0` (draw)

6\. Store each (board\_tensor, policy\_target, value\_target) triple in the replay buffer

7\. Policy target: MCTS visit count distribution (normalized), not the raw network output

8\. Value target: final game outcome propagated back, sign-flipped at each ply



\*\*Stalemate and draw handling:\*\*

\- Draws count as `0` value for both sides

\- The network should learn to prefer decisive games but accept drawn positions if it cannot win

\- Do not penalize draws when the engine is behind on material



\---



\### Opening Book Trainer (`src/opening\\\_trainer.py`)



This module activates after the initial mid-game training epochs establish a baseline policy. It then trains specifically on full games from move 1.



\*\*Opening diversity enforcement:\*\*



\- Maintain an internal opening frequency dictionary keyed by the first 4 half-moves (as UCI strings, e.g., `"e2e4 e7e5"`)

\- Before each opening training game, sample the first move with a \*\*inverse-frequency weighting\*\*: openings that have been played less recently get higher probability of being chosen

\- Inject \*\*Dirichlet noise\*\* at move 1 through move 8 with `α = 0.5` (higher than mid-game to force more variety)

\- At move 1, temperature is always `τ = 1.5` during opening training (even higher exploration)

\- After move 12, fall back to normal self-play temperature schedule

\- Log which openings are being sampled in `logs/training\\\_log.csv`



\*\*Enforced opening variety list (seed the frequency dict with all of these at weight 1.0 so they all get explored):\*\*



\- 1.e4 (King's Pawn)

\- 1.d4 (Queen's Pawn)

\- 1.c4 (English Opening)

\- 1.Nf3 (Reti Opening)

\- 1.g3 (King's Fianchetto)

\- 1.b3 (Nimzo-Larsen)

\- 1.f4 (Bird's Opening)

\- 1.e4 e5 (Open Game)

\- 1.e4 c5 (Sicilian)

\- 1.e4 e6 (French)

\- 1.e4 c6 (Caro-Kann)

\- 1.e4 d6 (Pirc/Modern)

\- 1.d4 d5 (Closed Game)

\- 1.d4 Nf6 (Indian Systems)

\- 1.d4 f5 (Dutch)



The system must detect if a single first move accounts for more than 35% of recent opening training games and temporarily suppress it until balance is restored.



\---



\### Replay Buffer (`src/replay\\\_buffer.py`)



\- Circular buffer with configurable max size, default `500,000` positions

\- Store: `(board\\\_tensor, policy\\\_target, value\\\_target)`

\- On every training step, sample a random mini-batch of size `2048`

\- Never allow the same game's positions to dominate a batch (shuffle aggressively)

\- Track buffer fill percentage for display in the dashboard



\---



\### Training Loop (`src/trainer.py`)



\- \*\*Optimizer:\*\* Adam, learning rate `0.001`, weight decay `1e-4`

\- \*\*LR schedule:\*\* Cosine annealing with warm restarts every `50` epochs

\- \*\*Loss function:\*\* Combined loss = policy loss + value loss

&#x20; - Policy loss: Cross-entropy between MCTS target distribution and network logits

&#x20; - Value loss: MSE between network value output and game outcome

&#x20; - Combined: `L = L\\\_policy + L\\\_value` (equal weight)

\- \*\*Gradient clipping:\*\* `torch.nn.utils.clip\\\_grad\\\_norm\\\_` with max norm `1.0`

\- \*\*Training epochs:\*\* Runs forever. One epoch = one batch of self-play games (configurable, default `64` games per epoch)

\- \*\*Checkpoint saving:\*\*

&#x20; - Save `checkpoints/checkpoint\\\_latest.pt` after every epoch

&#x20; - Save `checkpoints/checkpoint\\\_epoch\\\_N.pt` every 50 epochs

&#x20; - Save `checkpoints/checkpoint\\\_best.pt` whenever estimated Elo improves (see Elo tracker below)

\- \*\*Ctrl+C handler:\*\* Intercept `SIGINT` with a `try/except KeyboardInterrupt` around the main loop. On interrupt: finish the current batch, save checkpoint, export ONNX, print summary, exit cleanly. The ONNX export must be triggered automatically — never require the user to run a separate script.



\---



\### ONNX Export (`src/export.py`)



\- Called automatically on `Ctrl+C` and also callable manually

\- Load `checkpoints/checkpoint\\\_latest.pt`

\- Set model to `eval()` mode

\- Create a dummy input tensor `torch.zeros(1, 19, 8, 8)`

\- Call `torch.onnx.export()` with:

&#x20; - `opset\\\_version=17`

&#x20; - `input\\\_names=\\\["board\\\_input"]`

&#x20; - `output\\\_names=\\\["policy\\\_logits", "value"]`

&#x20; - `dynamic\\\_axes={"board\\\_input": {0: "batch\\\_size"}}`

\- Save to `exports/model.onnx`

\- Print file size and confirm export success



\---



\### Visual Dashboard (`src/dashboard.py`)



Use the `rich` library to render a \*\*live updating terminal dashboard\*\*. This must refresh every second while training is running. Do not use simple `print()` calls — use `rich.live.Live` with a `rich.layout.Layout`.



The dashboard must display all of the following simultaneously:



\*\*Header bar:\*\*

\- Project name ("ChessAI Training") and current timestamp

\- Current training mode ("Mid-game self-play" or "Opening training")



\*\*Left panel — Training metrics:\*\*

\- Current epoch number (e.g., `Epoch 1,204`)

\- Epochs per second (rolling 10-epoch average)

\- Games generated this session

\- Games per second (rolling average)

\- Total positions in replay buffer (with percentage of max capacity)

\- Current learning rate

\- A `tqdm`-style animated progress bar for current epoch's batch progress



\*\*Center panel — Loss \& performance:\*\*

\- Current policy loss (formatted to 4 decimal places)

\- Current value loss (formatted to 4 decimal places)

\- Combined loss with a sparkline of the last 20 loss values (ASCII sparkline using `▁▂▃▄▅▆▇█`)

\- Estimated Elo rating (see Elo estimation below)

\- Win/draw/loss ratio for last 100 self-play games (as a colored bar: green=win, gray=draw, red=loss)



\*\*Right panel — System resources:\*\*

\- GPU name and VRAM usage (if CUDA available)

\- CPU usage percentage

\- RAM usage

\- Disk usage of `C:\\\\Users\\\\prata\\\\agent01` directory



\*\*Bottom panel — Recent games:\*\*

\- Last 5 self-play games: starting FEN (truncated), move count, result, opening name (if detected)



\*\*Opening frequency panel (during opening training only):\*\*

\- Top 8 most-played openings this session with counts and a bar chart



\*\*Status line:\*\*

\- "Press Ctrl+C to stop training and auto-export model.onnx"



\*\*Elo estimation:\*\*

\- Maintain an internal Elo tracker

\- Every 200 epochs, pit the current model against the previous best checkpoint in 20 mini-games (50 MCTS simulations per move for speed)

\- Compute Elo delta from win/draw/loss outcome

\- Display current estimated Elo, change since last evaluation, and a trend arrow (↑ ↓ →)



\---



\### `train.py` — Entry Point



This file is the single command the user runs: `python train.py`



It must:

1\. Parse optional CLI arguments: `--epochs-before-opening INT` (default `200`), `--simulations INT` (default `800`), `--games-per-epoch INT` (default `64`), `--resume` (flag, resumes from `checkpoint\\\_latest.pt` if it exists)

2\. Create all required subdirectories inside `C:\\\\Users\\\\prata\\\\agent01` if they don't exist: `checkpoints\\\\`, `exports\\\\`, `logs\\\\`

3\. Print a startup banner with config summary

4\. Initialize network, MCTS, trainer, buffer, dashboard

5\. Register `signal.signal(signal.SIGINT, handler)` for clean Ctrl+C handling

6\. Run mid-game self-play training for `--epochs-before-opening` epochs

7\. Switch to interleaved training: alternate 3 mid-game epochs for every 1 opening training epoch

8\. Run forever until interrupted

9\. On interrupt: save checkpoint, export ONNX, print final summary



\---



\## Phase 2 — C# UCI Executable



\### Project Setup



\- Framework: `.NET 8.0`

\- Project type: Console Application

\- Output: single self-contained `.exe` (no runtime dependencies)

\- NuGet packages:

&#x20; - `Microsoft.ML.OnnxRuntime` (version `1.17.x` or latest stable)

\- Build command (run from `C:\\\\Users\\\\prata\\\\agent01\\\\engine\\\\`):

&#x20; ```

&#x20; dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\\build\\

&#x20; ```

\- Output goes to `C:\\\\Users\\\\prata\\\\agent01\\\\build\\\\ChessEngine.exe`



\---



\### UCI Protocol (`engine/UciHandler.cs`)



The executable must implement the full UCI (Universal Chess Interface) protocol exactly as specified at http://wbec-ridderkerk.nl/html/UCIProtocol.html.



Required commands to handle on stdin:



\- `uci` → respond with `id name ChessAI\\\\nid author prata\\\\nuciok`

\- `isready` → respond with `readyok`

\- `ucinewgame` → reset internal game state

\- `position startpos moves e2e4 e7e5 ...` → set board from startpos + move list

\- `position fen <FEN> moves ...` → set board from FEN + move list

\- `go movetime <ms>` → search for `<ms>` milliseconds, output `bestmove`

\- `go wtime <ms> btime <ms> winc <ms> binc <ms>` → time-managed search

\- `go infinite` → search until `stop` is received

\- `stop` → stop search immediately and output `bestmove`

\- `quit` → exit process cleanly



During search, output periodic `info` lines:

```

info depth 5 score cp 32 nodes 12400 nps 8200 time 1512 pv e2e4 e7e5 g1f3

```

(depth = MCTS simulation depth approximation, score = value head output × 100 as centipawns, nodes = MCTS simulations, nps = nodes per second)



\---



\### Board State (`engine/BoardState.cs`)



\- Implement a complete bitboard-based chess board in C#

\- Support: piece placement, castling rights, en passant, fifty-move rule, move history

\- Implement `MakeMove(move)` and `UnmakeMove(move)` for MCTS tree traversal

\- Support FEN parsing and FEN generation

\- Support `chess.Move` UCI string format (e.g., `"e2e4"`, `"e7e8q"` for promotion)

\- Detect: checkmate, stalemate, threefold repetition (via Zobrist hashing), fifty-move rule, insufficient material



\---



\### Move Generator (`engine/MoveGenerator.cs`)



\- Generate all fully legal moves (not pseudo-legal — filter king-in-check moves)

\- Return as a `List<string>` in UCI format

\- Must be fast: target < 1ms per position for typical middlegame positions



\---



\### Neural Network Wrapper (`engine/NeuralNetwork.cs`)



\- Load `exports/model.onnx` at startup using `Microsoft.ML.OnnxRuntime`

\- The path to `model.onnx` must be relative to the `.exe` location (i.e., always `..\\\\\\\\exports\\\\\\\\model.onnx` from `build\\\\`)

\- Implement `RunInference(float\\\[] boardTensor)` → `(float\\\[] policyLogits, float value)`

\- Input tensor: flattened `float\\\[19 \\\* 8 \\\* 8]` = `float\\\[1216]`

\- Board encoding in C# must exactly mirror `src/board\\\_encoder.py` (same plane order, same flipping logic for black to move, same normalization)

\- Run on CPU (ONNX Runtime CPU provider) for portability



\---



\### C# MCTS (`engine/MctsEngine.cs`)



\- Implement the same PUCT formula as the Python MCTS

\- `C\\\_puct = 1.25`

\- Default simulation count: `800` (configurable via UCI option `setoption name SimCount value 800`)

\- Support `go movetime <ms>` by running MCTS until time budget is consumed

\- Thread-safe: run MCTS on a background thread, check `stop` flag on each simulation

\- Return best move as UCI string to `UciHandler`



\---



\### `engine/Program.cs`



\- On startup: print nothing to stdout (UCI engines must not print anything before receiving `uci`)

\- Load ONNX model from `..\\\\\\\\exports\\\\\\\\model.onnx` relative to exe

\- If model file not found: write error to stderr and exit with code 1

\- Start UCI read loop: `while (true) { string line = Console.ReadLine(); handler.Handle(line); }`

\- Never crash on unexpected input — log to stderr and continue



\---



\## Quality of Life Features (QOL)



The following QOL features must all be implemented. This list is exhaustive and non-negotiable:



\*\*Training QOL:\*\*

\- `--resume` flag to continue from `checkpoint\\\_latest.pt` automatically

\- Automatic checkpoint recovery: if training crashes and restarts, it detects and loads the latest checkpoint without user action

\- Color-coded loss display: green if loss is decreasing, red if increasing over last 10 epochs

\- ETA display: estimated time until next checkpoint save (always 1 epoch away since it saves every epoch)

\- Session summary on Ctrl+C: total epochs, total games, total time, estimated Elo, final loss values

\- `logs/training\\\_log.csv` with columns: `epoch, timestamp, policy\\\_loss, value\\\_loss, combined\\\_loss, games\\\_generated, estimated\\\_elo, training\\\_mode`

\- `logs/game\\\_log.jsonl` with one JSON object per game: `{epoch, start\\\_fen, moves, result, move\\\_count, opening\\\_name}`

\- Graceful handling of CUDA out-of-memory: automatically reduce batch size by half and retry

\- Display a warning if replay buffer is less than 10% full (not enough data yet for reliable training)

\- Display a warning if GPU utilization drops below 50% (bottleneck on data generation)



\*\*UCI Engine QOL:\*\*

\- UCI option: `setoption name SimCount value 800` (adjustable simulation count)

\- UCI option: `setoption name Temperature value 0` (0 = best move, 1 = sampling — for variety in casual play)

\- Startup check: verify model.onnx exists and is loadable; print informative error if not

\- Log to `logs\\\\engine\\\_log.txt` (inside agent01): date, position FEN, best move, evaluation, time taken

\- Never hang: implement a hard 30-second timeout on any `go` command as a failsafe



\---



\## Training Phases Summary



| Phase | Duration | Position Source | Temperature | Notes |

|---|---|---|---|---|

| Mid-game bootstrap | First 200 epochs | Random 6–10 move positions | 1.0 for moves 1–30 | Build basic tactical understanding |

| Interleaved | Epoch 201+ | 3 mid-game : 1 opening | See above | Develop both phases |

| Opening specialty | Every 4th epoch | Full game from move 1 | 1.5 for moves 1–8 | Force opening variety |



\---



\## Key Implementation Notes



1\. \*\*Always encode board from current player's perspective.\*\* If it's black's turn, flip the board before passing to the network. This single convention is the most common source of bugs — enforce it in both Python and C#.



2\. \*\*Policy targets are MCTS visit distributions, not network outputs.\*\* The network learns from what MCTS actually chose, not what it initially predicted. This is the key insight of AlphaZero.



3\. \*\*Value targets are game outcomes, not evaluations.\*\* The network learns `+1` for win, `0` for draw, `-1` for loss. Never use material count as a value target.



4\. \*\*The C# board encoder must be bit-for-bit identical to the Python encoder.\*\* Write both simultaneously and add a cross-validation test: run a known FEN through both, compare the tensors.



5\. \*\*ONNX export must happen on every Ctrl+C.\*\* The user workflow is: train → Ctrl+C → drag ChessEngine.exe into Arena. This must work without any extra steps.



6\. \*\*Opening diversity is a hard requirement.\*\* The opening training module must actively prevent the network from converging to a single opening. Monitor opening frequency every 10 opening epochs and log a warning if any single opening exceeds 35% frequency.



7\. \*\*All file I/O uses paths relative to `C:\\\\Users\\\\prata\\\\agent01`.\*\* Never use `os.getcwd()` dynamically — hardcode the base path as a constant at the top of `train.py` and reference it everywhere.



\---



\## How to Run



\*\*Training:\*\*

```bash

cd C:\\\\Users\\\\prata\\\\agent01

pip install -r requirements.txt

python train.py

\\# Press Ctrl+C at any time to stop and auto-export model.onnx

```



\*\*Build the UCI engine (after training):\*\*

```bash

cd C:\\\\Users\\\\prata\\\\agent01\\\\engine

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\\\\build\\\\

```



\*\*Use in chess GUI:\*\*

\- Open Arena / Cutechess / SCID

\- Add new engine → browse to `C:\\\\Users\\\\prata\\\\agent01\\\\build\\\\ChessEngine.exe`

\- UCI protocol is auto-detected

\- Ensure `exports\\\\model.onnx` exists (it is auto-created on Ctrl+C during training)



\---



\## Final Checklist (Agent Must Verify Before Finishing)



\- \[ ] Every `open()`, `torch.save()`, `csv.writer`, `json.dump`, log handler uses a path inside `C:\\\\Users\\\\prata\\\\agent01`

\- \[ ] `train.py` runs to completion without crashing on first launch (no model checkpoint needed)

\- \[ ] `Ctrl+C` triggers checkpoint save AND ONNX export in one step

\- \[ ] `ChessEngine.exe` responds correctly to `uci`, `isready`, `position`, `go movetime 1000`, `quit` from a pipe

\- \[ ] C# board encoder produces identical tensor to Python board encoder for any given FEN

\- \[ ] Opening trainer prevents convergence to a single opening line

\- \[ ] Dashboard updates live at 1Hz without flickering

\- \[ ] ONNX export uses correct input/output node names (`board\\\_input`, `policy\\\_logits`, `value`)

\- \[ ] dotnet publish output lands in `C:\\\\Users\\\\prata\\\\agent01\\\\build\\\\`

\- \[ ] No file or folder is created outside `C:\\\\Users\\\\prata\\\\agent01`

if you are confused on anything, refer back to the prompt.md to clarify yourself and minimize context usage.

