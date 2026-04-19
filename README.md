# Chess AI - Self-Play Reinforcement Learning Engine

A complete chess-playing AI built from scratch using AlphaZero-style deep reinforcement learning. The model learns entirely through self-play, with no human game data. After training, it exports to ONNX and runs as a UCI-compatible C# engine for performance.

## Overview

This project has two main components:

- **Python training engine**: Trains a neural network using Monte Carlo Tree Search and self-play
- **C# UCI engine**: A lightweight, fast executable that loads the trained model and plays chess in any UCI-compatible GUI

The training process runs indefinitely until you stop it (Ctrl+C). At that point it automatically exports the model to ONNX and builds the C# engine.

## Quick Start

### Prerequisites

- Python 3.10 or higher
- .NET 8 SDK (for building the C# engine)

### Install

```bash
cd C:\Users\username\agent01
pip install -r requirements.txt
```

### Train

```bash
python master.py
```

By default it will ask if you want parallel training. Answer y or n. Training runs until you press Ctrl+C. Each epoch consists of 5 games by default.

When you stop, it saves a checkpoint, exports model.onnx, and builds build/ChessEngine.exe.

### Build the Engine

After training (or anytime), build the C# engine:

```bash
cd engine
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\build\
```

The executable appears at C:\Users\prata\agent01\build\ChessEngine.exe.

## Architecture

### Neural Network

The network is a residual architecture with configurable depth. Default: 3 blocks with 64 channels.

Input: 19 binary planes of shape 8×8:

- Planes 0–5: current player pieces (pawn, knight, bishop, rook, queen, king)
- Planes 6–11: opponent pieces
- Plane 12: current player color
- Planes 13–16: castling rights for both sides
- Plane 17: en passant square
- Plane 18: halfmove clock

Heads:

- Policy head: logits over 4672 possible moves
- Value head: scalar between −1 and +1 (win probability from current player perspective)

### Move Encoding

AlphaZero-style 73-move-per-square encoding:

- 64 from-squares × 73 move types = 4672 total
- Move types: 56 queen-like slides (8 directions × 7 distances), 8 knight moves, 9 underpromotions

Implemented in src/move_encoder.py with move_to_index() and index_to_move().

### Monte Carlo Tree Search

PUCT selection formula: U(s,a) = Q(s,a) + c_puct × P(s,a) × sqrt(N(s)) / (1 + N(s,a))

Parameters:

- c_puct: 1.25
- Simulations: 200–800 (configurable)
- Temperature: 1.0 early, 0 late
- Dirichlet noise at root: α=0.3, ε=0.25

### Self-Play

Each game starts either from the standard position or from a random 6–10 ply opening (70% midgame, 30% opening). Both players use MCTS + neural network. At each move we store the board state, MCTS policy distribution, and final game outcome.

### Replay Buffer

Circular buffer up to 500,000 positions. Each training step samples a random batch of 2048 positions. The buffer fills gradually from self-play games.

### Training

- Optimizer: Adam (lr=0.001, weight decay=1e-4)
- Loss: policy cross-entropy + value MSE
- Batch size: 2048
- LR schedule: cosine annealing with warm restarts every 50 epochs
- Checkpoints: saved every epoch; best checkpoint whenever estimated Elo improves

### Parallel Training

Use --parallel to run games in parallel across CPU cores. Network weights are copied to each worker process; workers generate games independently; results are combined and a single training update happens per epoch.

python master.py --parallel --games-per-epoch 5

If you omit --parallel you will be prompted at startup.

### Auto-Save

After every epoch the latest checkpoint is written. On Ctrl+C the loop finishes cleanly, exports to ONNX, and builds the engine.

## Project Structure

```
C:\Users\username\agent01
│
├── master.py                 # Main training script
├── requirements.txt          # Python dependencies
├── logging_setup.py         # Logging configuration
│
├── src/                      # Python source modules
│   ├── __init__.py
│   ├── network.py           # Neural network (ResNet)
│   ├── mcts.py              # Monte Carlo Tree Search
│   ├── self_play.py         # Self-play game generation
│   ├── trainer.py           # Training step, optimizer
│   ├── board_encoder.py     # Board → tensor
│   ├── move_encoder.py      # Move ↔ policy index
│   ├── replay_buffer.py     # Experience replay
│   ├── logger.py            # Structured logging
│   └── export.py            # PyTorch → ONNX
│
├── checkpoints/              # Created at runtime
│   ├── checkpoint_latest.pt
│   ├── checkpoint_best.pt
│   └── checkpoint_epoch_N.pt
│
├── exports/                  # Created at runtime
│   └── model.onnx           # Exported model for C# engine
│
├── logs/                     # Created at runtime
│   ├── training_log.csv     # Per-epoch metrics
│   ├── game_log.jsonl       # Per-game data
│   ├── main.log             # General logs
│   ├── error.log            # Error logs
│   └── training.log         # Training logs
│
├── engine/                   # C# UCI engine
│   ├── ChessEngine.csproj
│   ├── Program.cs           # Entry point
│   ├── UciHandler.cs        # UCI protocol
│   ├── MctsEngine.cs        # C# MCTS
│   ├── BoardState.cs        # Bitboard board
│   ├── MoveGenerator.cs     # Move generation
│   ├── NeuralNetwork.cs     # ONNX wrapper
│   └── Evaluator.cs         # Inference
│
└── build/                    # Dotnet publish output (created at runtime)
    └── ChessEngine.exe      # Final UCI executable
```

## Command-Line Options

```
python master.py [options]

Options:
  --simulations N         MCTS simulations per move (default: 200)
  --games-per-epoch N     Games per epoch (default: 5)
  --resume                Load latest checkpoint and continue
  --parallel              Enable parallel game generation
```

## Using the Engine

After training (or Ctrl+C), load ChessEngine.exe into any UCI-compatible chess GUI:

1. Open Arena / Cute Chess / SCID / etc.
2. Add new engine
3. Browse to C:\Users\prata\agent01\build\ChessEngine.exe
4. Engine name: ChessAI, author: prata

The engine automatically loads C:\Users\prata\agent01\exports\model.onnx.

## Key Implementation Details

### Board Perspective

Both Python and C# always encode the board from the current player's perspective. When it is black's turn, the board is flipped so the network always sees itself as white. This convention must be matched exactly in both codebases.

### Policy Targets

During self-play the MCTS visit distribution becomes the training target, not the network's initial prediction. This is the AlphaZero insight.

### Value Targets

The value head learns the game outcome (+1 win, 0 draw, −1 loss) from the current player's perspective. No material features are used as targets.

### Checkpointing

- checkpoint_latest.pt: most recent epoch
- checkpoint_best.pt: best Elo so far
- checkpoint_epoch_N.pt: every 50 epochs

### CSV Logs

training_log.csv columns: epoch, timestamp, policy_loss, value_loss, combined_loss, games_generated, estimated_elo, training_mode

game_log.jsonl: one JSON per finished game

## Technical Notes

- The neural network is fully exportable to ONNX with input name "board_input" and outputs "policy_logits" and "value".
- The C# engine uses Microsoft.ML.OnnxRuntime for CPU inference; no GPU required.
- MCTS uses virtual losses to support parallel search (though current implementation runs workers independently).
- The replay buffer samples uniformly without replacement from the entire history.
- Elo is estimated relative to an initial 1200 baseline; improvements are tracked heuristically from loss reduction.

## Credits

Implementation of the AlphaZero algorithm applied to chess. Original paper: Mastering Chess and Shogi by Self-Play with a General Reinforcement Learning Algorithm (Silver et al., DeepMind 2018).