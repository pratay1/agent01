import os
import sys
import argparse
import time
import csv
import json
import subprocess
import glob
import random
import traceback
from datetime import datetime

import torch
import matplotlib.pyplot as plt
import io
from PIL import Image
import numpy as np

# Import our logger
from src.logger import get_logger, logger

# Import logging setup
from logging_setup import setup_logging

# Setup logging - only INFO level to remove debug output
import logging
logging.basicConfig(level=logging.INFO)

BASE_DIR = r"C:\Users\prata\agent01"
# Standard starting FEN for "regular chess position"
STARTING_FEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"

# --- Multiprocessing worker globals ---
_worker_network = None
_worker_device = None

def _init_worker(network_state_dict):
    """Initialize each worker process with a copy of the network."""
    global _worker_network, _worker_device
    import torch
    from src.network import create_network
    # Build network on CPU
    _worker_network = create_network()
    _worker_network.load_state_dict(network_state_dict)
    _worker_network.eval()
    _worker_device = torch.device("cpu")

def _play_game_worker(mcts_config, training_mode):
    """Worker function for parallel game generation."""
    global _worker_network
    from src.self_play import play_game
    # Run without move callback
    return play_game(_worker_network, mcts_config, training_mode, max_think_time=0.05, move_callback=None)
# Standard starting FEN for "regular chess position"
STARTING_FEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"


def save_checkpoint_and_export(network, trainer):
    try:
        logger.log_info("Starting checkpoint saving and ONNX export")
        print("\nSaving checkpoint and exporting ONNX...")
        checkpoint_path = os.path.join(BASE_DIR, "checkpoints", "checkpoint_latest.pt")
        trainer.save_checkpoint(checkpoint_path)
        logger.log_info(f"Checkpoint saved to {checkpoint_path}")

        from src.export import export_to_onnx
        onnx_path = os.path.join(BASE_DIR, "exports", "model.onnx")
        export_to_onnx(network, onnx_path, checkpoint_path)
        logger.log_info(f"ONNX export successful: {onnx_path}")
        return onnx_path
    except Exception as e:
        logger.log_exception(e, "save_checkpoint_and_export")
        print(f"Error in save_checkpoint_and_export: {e}")
        raise


def build_engine():
    logger.log_info("Starting C# chess engine build")
    print("\n\nBuilding C# chess engine...")
    engine_dir = os.path.join(BASE_DIR, "engine")
    build_dir = os.path.join(BASE_DIR, "build")

    # Ensure build directory exists
    os.makedirs(build_dir, exist_ok=True)
    logger.log_info(f"Engine directory: {engine_dir}")
    logger.log_info(f"Build directory: {build_dir}")

    try:
        result = subprocess.run(
            ["dotnet", "publish", "-c", "Release", "-r", "win-x64", "--self-contained", "true",
             "-p:PublishSingleFile=true", "-o", build_dir, "-p:OutputName=ChessEngine_new"],
            cwd=engine_dir,
            capture_output=True,
            text=True,
            timeout=300  # 5 minute timeout
        )
        if result.returncode != 0:
            raise RuntimeError(f"Build failed with return code {result.returncode}: {result.stderr}")
    except subprocess.TimeoutExpired as e:
        logger.log_exception(e, "build_engine")
        raise RuntimeError("Build process timed out after 5 minutes") from e
    except Exception as e:
        logger.log_exception(e, "build_engine")
        raise RuntimeError(f"Build failed: {e}") from e

    logger.log_info("Dotnet publish completed successfully")

    # Robustly locate the built executable (may be in a subdirectory or with different name)
    output_exe = os.path.join(build_dir, "ChessEngine_new.exe")
    final_exe = os.path.join(build_dir, "ChessEngine.exe")

    # If the expected exe exists, rename it
    if os.path.exists(output_exe):
        logger.log_info(f"Found output executable: {output_exe}")
        if os.path.exists(final_exe):
            logger.log_info(f"Removing existing final executable: {final_exe}")
            os.remove(final_exe)
        logger.log_info(f"Renaming {output_exe} to {final_exe}")
        os.rename(output_exe, final_exe)
        print(f"Engine built and replaced: {final_exe}")
        logger.log_info(f"Engine build successful: {final_exe}")
        return

    # Search for any .exe in the build directory (recursively)
    exe_candidates = glob.glob(os.path.join(build_dir, "**", "*.exe"), recursive=True)
    if exe_candidates:
        # Pick the first .exe that is not already ChessEngine.exe
        src_exe = None
        for cand in exe_candidates:
            if not cand.lower().endswith("chessengine.exe"):
                src_exe = cand
                break
        if src_exe is None:
            # All exes are named ChessEngine.exe? Something odd but use first
            src_exe = exe_candidates[0]
        logger.log_info(f"Found executable: {src_exe}")
        if os.path.abspath(src_exe) != os.path.abspath(final_exe):
            if os.path.exists(final_exe):
                os.remove(final_exe)
            os.rename(src_exe, final_exe)
            print(f"Engine built and replaced: {final_exe}")
            logger.log_info(f"Engine build successful: {final_exe}")
        else:
            print(f"Engine already built: {final_exe}")
            logger.log_info(f"Engine already built at {final_exe}")
        return

    error_msg = f"Expected output executable not found: {output_exe} and no .exe found in {build_dir}"
    logger.log_error(error_msg)
    print(f"Build failed: {error_msg}")
    raise FileNotFoundError(error_msg)


def run_training(args):
    try:
        logger.log_info("Starting run_training function")
        from src.network import create_network
        from src.replay_buffer import ReplayBuffer
        from src.trainer import Trainer
        from src.self_play import play_game

        logger.log_info("Creating network, buffer, and trainer")
        network = create_network()
        buffer = ReplayBuffer(max_size=500000, batch_size=2048)
        trainer = Trainer(network, buffer, learning_rate=0.001, weight_decay=1e-4)

        logger.log_info("Setting up checkpoint paths")
        checkpoint_path = os.path.join(BASE_DIR, "checkpoints", "checkpoint_latest.pt")
        start_epoch = 0
        if args.resume and os.path.exists(checkpoint_path):
            try:
                if trainer.load_checkpoint(checkpoint_path):
                    start_epoch = 1
                    logger.log_info(f"Resumed training from checkpoint, starting at epoch {start_epoch}")
                else:
                    logger.log_warning("Failed to load checkpoint, starting from scratch")
            except Exception as e:
                logger.log_exception(e, "loading checkpoint")
                logger.log_warning("Checkpoint loading failed, starting from scratch")

        logger.log_info("Setting up log file paths")
        training_log_path = os.path.join(BASE_DIR, "logs", "training_log.csv")
        if not os.path.exists(training_log_path):
            try:
                with open(training_log_path, 'w', newline='') as f:
                    writer = csv.writer(f)
                    writer.writerow(["epoch", "timestamp", "policy_loss", "value_loss", "combined_loss", "games_generated", "estimated_elo", "training_mode"])
                logger.log_info(f"Created training log file: {training_log_path}")
            except Exception as e:
                logger.log_exception(e, "creating training log file")
                raise

        game_log_path = os.path.join(BASE_DIR, "logs", "game_log.jsonl")
        logger.log_info(f"Game log path: {game_log_path}")

        epoch = start_epoch
        total_games = 0
        best_elo = 1200
        elo_history = []
        moves_history = []
        current_game_num = 0

        logger.log_info("Setting up MCTS configuration")
        mcts_config = {
            "cpuct": 1.25,
            "num_simulations": args.simulations,
            "temperature": 1.0,
            "dirichlet_alpha": 0.3,
            "dirichlet_epsilon": 0.25
        }

        logger.log_info("Initializing game state")
        game_state = {
            "epoch": 0,
            "mode": "Mid-game",
            "policy_loss": 0.0,
            "value_loss": 0.0,
            "combined_loss": 0.0,
            "games": 0,
            "buffer_pct": 0.0,
            "elo": 1200,
            "current_move": 0,
            "last_moves": [],
            "game_result": "-",
            "sims": args.simulations,
            "epoch_time": 0.0
        }

        logger.log_info("Setting up keyboard input handlers")
        import msvcrt
        def kb_hit():
            return msvcrt.kbhit()

        def get_key():
            if msvcrt.kbhit():
                return msvcrt.getch()
            return None

        logger.log_info("Printing configuration")
        logger.log_info(f"Config: {args.simulations} sims, {args.games_per_epoch} games/epoch, parallel={args.parallel}")
        
        # Ask user at runtime if parallel training desired (if not overridden by flag)
        use_parallel = args.parallel
        if not use_parallel:
            try:
                resp = input("Run games in parallel? (y/n): ").strip().lower()
                use_parallel = (resp == 'y' or resp == 'yes')
            except (EOFError, KeyboardInterrupt):
                print()  # newline
                use_parallel = False
        
        if use_parallel:
            logger.log_info(f"Parallel training enabled with {args.games_per_epoch} concurrent workers")
            import multiprocessing
            # Use the module-level _play_game_worker defined above
        
        logger.log_info("Starting main training loop")
        try:
            while True:
                try:
                    if kb_hit():
                        key = get_key()
                        if key and key == b'\x1b':
                            logger.log_info("ESC key pressed, stopping training")
                            break

                    epoch_start = time.time()
                    total_games_start = total_games

                    # --- Parallel game generation ---
                    if use_parallel:
                        # Prepare tasks: each game gets a random mode (70% midgame, 30% opening)
                        tasks = []
                        for _ in range(args.games_per_epoch):
                            mode = "midgame" if random.random() < 0.7 else "opening"
                            tasks.append((mcts_config, mode))
                        
                        # Run in parallel
                        with multiprocessing.Pool(processes=min(args.games_per_epoch, multiprocessing.cpu_count())) as pool:
                            results = pool.starmap(_play_game_worker, tasks)
                        
                        # Process results
                        for states, policy_targets, value_targets, start_fen, game_result in results:
                            if states:  # only add if game produced data
                                buffer.add_game(states, policy_targets, value_targets)
                                moves_history.append(len(states))
                                total_games += 1
                                game_state['games'] = total_games
                                game_state['buffer_pct'] = buffer.fill_percentage()
                                
                                game_data = {
                                    "epoch": epoch,
                                    "start_fen": start_fen,
                                    "result": game_result,
                                    "move_count": len(states),
                                }
                                with open(game_log_path, 'a') as f:
                                    f.write(json.dumps(game_data) + "\n")
                    else:
                        # --- Sequential game generation ---
                        for g in range(args.games_per_epoch):
                            try:
                                if kb_hit() and get_key() == b'\x1b':
                                    logger.log_info("ESC key pressed during game generation, stopping training")
                                    raise KeyboardInterrupt()

                                # Randomly choose training mode per game: 70% midgame (random opening), 30% opening (standard start)
                                training_mode = "midgame" if random.random() < 0.7 else "opening"

                                logger.log_debug(f"Starting game {g+1}/{args.games_per_epoch} in epoch {epoch} with mode={training_mode}")
                                states, policy_targets, value_targets, start_fen, game_result = play_game(
                                    network, mcts_config, training_mode,
                                    max_think_time=0.05,
                                    move_callback=None  # no live updates
                                )

                                logger.log_debug(f"Game {g+1} completed, adding to buffer")
                                buffer.add_game(states, policy_targets, value_targets)
                                
                                moves_history.append(len(states))
                                
                                game_state['game_result'] = "W" if game_result == 1 else "L" if game_result == -1 else "D"

                                game_data = {
                                    "epoch": epoch,
                                    "start_fen": start_fen,
                                    "result": game_result,
                                    "move_count": len(states),
                                }
                                with open(game_log_path, 'a') as f:
                                    f.write(json.dumps(game_data) + "\n")

                                total_games += 1
                                game_state['games'] = total_games
                                game_state['buffer_pct'] = buffer.fill_percentage()

                                logger.log_debug(f"Updated after game {g+1}")
                            except Exception as e:
                                logger.log_exception(e, f"game {g+1} in epoch {epoch}")
                                continue

                    logger.log_info(f"Completed epoch {epoch} with {total_games - total_games_start} games, running training step")
                    # Epoch training mode label for logging and CSV
                    epoch_training_mode = "mixed (70% mid / 30% opening)"
                    losses = trainer.train_step()
                    epoch_elapsed = time.time() - epoch_start

                    current_elo = best_elo + int((losses["combined_loss"] - 1.0) * -100)
                    
                    elo_history.append(current_elo)

                    game_state['epoch'] = epoch
                    game_state['policy_loss'] = losses['policy_loss']
                    game_state['value_loss'] = losses['value_loss']
                    game_state['combined_loss'] = losses['combined_loss']
                    game_state['elo'] = current_elo
                    game_state['epoch_time'] = epoch_elapsed

                    logger.log_training_step(epoch, losses, {
                        "epoch_time": epoch_elapsed,
                        "total_games": total_games,
                        "buffer_pct": buffer.fill_percentage()
                    })

                    with open(training_log_path, 'a', newline='') as f:
                        writer = csv.writer(f)
                        writer.writerow([
                            epoch, datetime.now().isoformat(),
                            losses["policy_loss"], losses["value_loss"], losses["combined_loss"],
                            total_games, current_elo, epoch_training_mode
                        ])

                    checkpoint_path = os.path.join(BASE_DIR, "checkpoints", "checkpoint_latest.pt")
                    trainer.save_checkpoint(checkpoint_path)
                    logger.log_debug(f"Saved checkpoint for epoch {epoch}")

                    if epoch % 50 == 0:
                        epoch_ckpt = os.path.join(BASE_DIR, "checkpoints", f"checkpoint_epoch_{epoch}.pt")
                        trainer.save_checkpoint(epoch_ckpt)
                        logger.log_info(f"Saved epoch checkpoint: {epoch_ckpt}")

                    if current_elo > best_elo:
                        best_elo = current_elo
                        best_ckpt = os.path.join(BASE_DIR, "checkpoints", "checkpoint_best.pt")
                        trainer.save_checkpoint(best_ckpt)
                        logger.log_info(f"New best elo {best_elo}, saved best checkpoint")

                    epoch += 1
                except Exception as e:
                    logger.log_exception(e, "training loop iteration")
                    # Continue training despite errors in individual iterations
                    continue

        except Exception as e:
            logger.log_exception(e, "training loop")
            raise
    except KeyboardInterrupt:
        logger.log_info("Training interrupted by user (KeyboardInterrupt)")
        pass
    except Exception as e:
        logger.log_exception(e, "run_training function")
        print(f"Error: {e}")
        raise  # Re-raise to let outer handlers deal with it

    logger.log_info(f"Training loop completed: Epochs={epoch}, Games={total_games}, Best Elo={best_elo}")

    save_checkpoint_and_export(network, trainer)
    build_engine()

    logger.log_info("Training completed successfully, engine built at build/ChessEngine.exe")

def main():
    try:
        # Ensure required directories exist
        os.makedirs(os.path.join(BASE_DIR, "checkpoints"), exist_ok=True)
        os.makedirs(os.path.join(BASE_DIR, "exports"), exist_ok=True)
        os.makedirs(os.path.join(BASE_DIR, "logs"), exist_ok=True)
        # Setup logging
        setup_logging(level=logging.INFO)

        logger.log_info("Starting ChessAI Training Engine")
        parser = argparse.ArgumentParser(description="Chess AI Training")
        parser.add_argument("--simulations", type=int, default=200)
        parser.add_argument("--games-per-epoch", type=int, default=5,
                            help="Number of games to play per epoch (default: 5)")
        parser.add_argument("--resume", action="store_true")
        parser.add_argument("--parallel", action="store_true",
                            help="Run games in parallel using multiprocessing")
        args = parser.parse_args()
        
        logger.log_info(f"Arguments parsed: epochs_before_opening={args.epochs_before_opening}, simulations={args.simulations}, games_per_epoch={args.games_per_epoch}, resume={args.resume}")
        
        run_training(args)
        logger.log_info("Training completed successfully")
        
    except Exception as e:
        logger.log_exception(e, "main function")
        print(f"Fatal error in main: {e}")
        sys.exit(1)


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        logger.log_exception(e, "script execution")
        print(f"Fatal error: {e}")
        sys.exit(1)