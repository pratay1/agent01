import os
import sys
import argparse
import time
import csv
import json
import subprocess
import glob
import random
import multiprocessing
import math
import chess
import torch
from datetime import datetime

import logging

from src.logger import logger
from logging_setup import setup_logging
from src.opening_trainer import OpeningTrainer
from src.mcts import MCTS


# =========================
# CONFIG
# =========================

from src.mcts import MCTS

def evaluate_elo(current_network, opponent_network, games=20, simulations=50, opponent_rating=1200):
    """Play evaluation games and compute Elo rating."""
    current_network.eval()
    opponent_network.eval()
    half = games // 2
    wins = 0
    draws = 0

    for i in range(games):
        board = chess.Board()
        current_white = (i < half)
        white_net = current_network if current_white else opponent_network
        black_net = opponent_network if current_white else current_network

        while not board.is_game_over():
            net = white_net if board.turn == chess.WHITE else black_net
            mcts = MCTS(
                network=net,
                cpuct=1.25,
                num_simulations=simulations,
                temperature=0,
                dirichlet_alpha=0.3,
                dirichlet_epsilon=0.0
            )
            move = mcts.get_best_move(board)
            if move is None:
                move = random.choice(list(board.legal_moves))
            board.push(move)

        outcome = board.outcome()
        if outcome is None:
            result = 0
        else:
            if outcome.winner == chess.WHITE:
                result = 1
            elif outcome.winner == chess.BLACK:
                result = -1
            else:
                result = 0

        if current_white:
            if result == 1:
                wins += 1
            elif result == 0:
                draws += 1
        else:
            if result == -1:
                wins += 1
            elif result == 0:
                draws += 1

    losses = games - wins - draws
    score = (wins + 0.5 * draws) / games
    if score == 0:
        elo = 0
    elif score == 1:
        elo = 10000
    else:
        elo_diff = 400 * math.log10(score / (1 - score))
        elo = int(opponent_rating + elo_diff)
    return elo, score


BASE_DIR = r"C:\Users\prata\agent01"

STARTING_FEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"


# =========================
# WORKER STATE
# =========================

_worker_network = None


def _init_worker(network_state_dict):
    global _worker_network
    from src.network import create_network

    _worker_network = create_network()
    _worker_network.load_state_dict(network_state_dict)
    _worker_network.eval()


def _play_game_worker(args):
    global _worker_network
    from src.self_play import play_game

    mcts_config, mode, start_fen, opening_key = args  # opening_key is unused here

    return play_game(
        _worker_network,
        mcts_config,
        mode,
        max_think_time=0.05,
        move_callback=None,
        start_fen=start_fen
    )


# =========================
# ENGINE BUILD
# =========================

def build_engine():
    logger.log_info("Building C# engine...")

    engine_dir = os.path.join(BASE_DIR, "engine")
    build_dir = os.path.join(BASE_DIR, "build")

    os.makedirs(build_dir, exist_ok=True)

    result = subprocess.run(
        [
            "dotnet", "publish",
            "-c", "Release",
            "-r", "win-x64",
            "--self-contained", "true",
            "-p:PublishSingleFile=true",
            "-o", build_dir
        ],
        cwd=engine_dir,
        capture_output=True,
        text=True
    )

    if result.returncode != 0:
        raise RuntimeError(result.stderr)

    logger.log_info("Engine build complete")


# =========================
# CHECKPOINT + EXPORT
# =========================

def save_checkpoint_and_export(network, trainer):
    logger.log_info("Saving checkpoint + exporting ONNX")

    checkpoint_path = os.path.join(BASE_DIR, "checkpoints", "checkpoint_latest.pt")
    trainer.save_checkpoint(checkpoint_path)

    from src.export import export_to_onnx
    onnx_path = os.path.join(BASE_DIR, "exports", "model.onnx")
    export_to_onnx(network, onnx_path, checkpoint_path)

    logger.log_info("Export complete")


# =========================
# TRAINING LOOP
# =========================

def run_training(args):
    from src.network import create_network
    from src.replay_buffer import ReplayBuffer
    from src.trainer import Trainer
    from src.self_play import play_game

    # Setup directories
    checkpoint_dir = os.path.join(BASE_DIR, "checkpoints")
    export_dir = os.path.join(BASE_DIR, "exports")
    log_dir = os.path.join(BASE_DIR, "logs")
    os.makedirs(checkpoint_dir, exist_ok=True)
    os.makedirs(export_dir, exist_ok=True)
    os.makedirs(log_dir, exist_ok=True)

    checkpoint_path = os.path.join(checkpoint_dir, "checkpoint_latest.pt")
    game_log_path = os.path.join(log_dir, "game_log.jsonl")

    # Opening trainer
    opening_trainer = OpeningTrainer()

    # CSV training log
    training_log_path = os.path.join(log_dir, "training_log.csv")
    if not os.path.exists(training_log_path):
        with open(training_log_path, 'w', newline='') as f:
            writer = csv.writer(f)
            writer.writerow(['epoch','timestamp','policy_loss','value_loss','combined_loss','games_generated','estimated_elo','training_mode'])

    # MCTS configurations
    midgame_mcts_config = {
        "cpuct": 1.25,
        "num_simulations": args.simulations,
        "temperature": 1.0,
        "dirichlet_alpha": 0.3,
        "dirichlet_epsilon": 0.25
    }
    opening_mcts_config = {
        "cpuct": 1.25,
        "num_simulations": args.simulations,
        "temperature": 1.0,
        "dirichlet_alpha": 0.5,
        "dirichlet_epsilon": 0.25
    }

    # Model, buffer, trainer
    network = create_network()
    buffer = ReplayBuffer(max_size=500000, batch_size=2048)
    trainer = Trainer(network, buffer)

    if args.resume and os.path.exists(checkpoint_path):
        trainer.load_checkpoint(checkpoint_path)
        logger.log_info("Resumed training")

    epoch = 0
    total_games = 0
    estimated_elo = 1200.0
    best_elo = 1200.0

    logger.log_info("Training started")

    try:
        while True:
            epoch_start = time.time()

            # Determine training mode
            if epoch < 200:
                training_mode = "midgame"
            else:
                if ((epoch - 200) % 4) == 3:
                    training_mode = "opening"
                else:
                    training_mode = "midgame"

            # Build tasks
            tasks = []
            if training_mode == "opening":
                for _ in range(args.games_per_epoch):
                    opening_move_uci = opening_trainer.sample_first_move()
                    board = chess.Board()
                    board.push_uci(opening_move_uci)
                    start_fen = board.fen()
                    opening_key = opening_move_uci[:4]
                    tasks.append((opening_mcts_config, "opening", start_fen, opening_key))
            else:
                for _ in range(args.games_per_epoch):
                    tasks.append((midgame_mcts_config, "midgame", None, None))

            # Game generation
            if args.parallel:
                network_state = network.state_dict()
                with multiprocessing.Pool(
                    processes=min(args.games_per_epoch, multiprocessing.cpu_count()),
                    initializer=_init_worker,
                    initargs=(network_state,)
                ) as pool:
                    results = pool.map(_play_game_worker, tasks)
            else:
                results = []
                for task in tasks:
                    results.append(play_game(
                        network,
                        task[0],
                        task[1],
                        max_think_time=0.05,
                        move_callback=None,
                        start_fen=task[2]
                    ))

            # Process results
            for task, result in zip(tasks, results):
                states, policy, value, start_fen_ret, game_result = result
                if not states:
                    continue
                buffer.add_game(states, policy, value)
                opening_key = task[3]
                if opening_key is not None:
                    opening_trainer.update_frequency(opening_key)
                total_games += 1

                with open(game_log_path, "a") as f:
                    f.write(json.dumps({
                        "epoch": epoch,
                        "fen": start_fen_ret,
                        "result": game_result,
                        "moves": len(states)
                    }) + "\n")

            # Training step
            losses = trainer.train_step()
            epoch_time = time.time() - epoch_start

            logger.log_info(
                f"Epoch {epoch} | Games {len(results)} | "
                f"Loss {losses['combined_loss']:.4f} | Time {epoch_time:.2f}s"
            )

            # Elo evaluation
            if epoch % args.eval_interval == 0:
                best_ckpt_path = os.path.join(checkpoint_dir, "checkpoint_best.pt")
                if os.path.exists(best_ckpt_path):
                    opponent_net = create_network()
                    ckpt = torch.load(best_ckpt_path, map_location='cpu')
                    opponent_net.load_state_dict(ckpt['network_state_dict'])
                    opponent_net.eval()
                    opp_rating = best_elo
                else:
                    opponent_net = create_network()
                    opponent_net.eval()
                    opp_rating = 1200

                new_elo, score = evaluate_elo(network, opponent_net, games=20, simulations=50, opponent_rating=opp_rating)
                estimated_elo = new_elo
                logger.log_info(f"Elo evaluation: score={score:.2f}, estimated Elo={estimated_elo:.0f}")

                if estimated_elo > best_elo:
                    best_elo = estimated_elo
                    trainer.save_checkpoint(best_ckpt_path)
                    logger.log_info(f"New best model saved (Elo: {best_elo:.0f})")

            # Save latest checkpoint
            trainer.save_checkpoint(checkpoint_path)

            # Periodic checkpoint every 50 epochs
            if epoch % 50 == 0:
                trainer.save_checkpoint(os.path.join(checkpoint_dir, f"epoch_{epoch}.pt"))

            # CSV logging
            with open(training_log_path, 'a', newline='') as f:
                writer = csv.writer(f)
                writer.writerow([epoch, datetime.now().isoformat(),
                                 losses['policy_loss'], losses['value_loss'], losses['combined_loss'],
                                 total_games, f"{estimated_elo:.1f}", training_mode])

            epoch += 1

    except KeyboardInterrupt:
        logger.log_info("Training interrupted by user")
    except Exception as e:
        logger.log_exception(e, "run_training")
        raise
    finally:
        logger.log_info("Saving final checkpoint and exporting ONNX...")
        trainer.save_checkpoint(checkpoint_path)
        from src.export import export_to_onnx
        onnx_path = os.path.join(export_dir, "model.onnx")
        export_to_onnx(network, onnx_path, checkpoint_path)
        build_engine()
        logger.log_info("Training complete. Engine built.")


# =========================
# MAIN
# =========================

def main():
    os.makedirs(os.path.join(BASE_DIR, "checkpoints"), exist_ok=True)
    os.makedirs(os.path.join(BASE_DIR, "exports"), exist_ok=True)
    os.makedirs(os.path.join(BASE_DIR, "logs"), exist_ok=True)

    setup_logging(level=logging.INFO)

    parser = argparse.ArgumentParser()
    parser.add_argument("--simulations", type=int, default=800)
    parser.add_argument("--games-per-epoch", type=int, default=64)
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--parallel", action="store_true")
    parser.add_argument("--eval-interval", type=int, default=200)

    args = parser.parse_args()

    logger.log_info(f"Starting training | parallel={args.parallel}")

    try:
        run_training(args)
    except KeyboardInterrupt:
        logger.log_info("Stopped by user")
    except Exception as e:
        logger.log_exception(e, "main")


if __name__ == "__main__":
    main()