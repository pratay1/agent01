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
from datetime import datetime

import logging

from src.logger import logger
from logging_setup import setup_logging


# =========================
# CONFIG
# =========================

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

    mcts_config, mode = args

    return play_game(
        _worker_network,
        mcts_config,
        mode,
        max_think_time=0.05,
        move_callback=None
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

    network = create_network()
    buffer = ReplayBuffer(max_size=500000, batch_size=2048)
    trainer = Trainer(network, buffer)

    checkpoint_path = os.path.join(BASE_DIR, "checkpoints", "checkpoint_latest.pt")

    if args.resume and os.path.exists(checkpoint_path):
        trainer.load_checkpoint(checkpoint_path)
        logger.log_info("Resumed training")

    # logs
    os.makedirs(os.path.join(BASE_DIR, "logs"), exist_ok=True)

    game_log_path = os.path.join(BASE_DIR, "logs", "game_log.jsonl")

    mcts_config = {
        "cpuct": 1.25,
        "num_simulations": args.simulations,
        "temperature": 1.0,
        "dirichlet_alpha": 0.3,
        "dirichlet_epsilon": 0.25
    }

    epoch = 0
    total_games = 0

    logger.log_info("Training started")

    while True:
        epoch_start = time.time()

        tasks = []
        for _ in range(args.games_per_epoch):
            mode = "midgame" if random.random() < 0.7 else "opening"
            tasks.append((mcts_config, mode))

        # =========================
        # GAME GENERATION
        # =========================

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
            for t in tasks:
                results.append(_play_game_worker(t))

        # =========================
        # PROCESS GAMES
        # =========================

        for states, policy, value, start_fen, result in results:
            if not states:
                continue

            buffer.add_game(states, policy, value)
            total_games += 1

            with open(game_log_path, "a") as f:
                f.write(json.dumps({
                    "epoch": epoch,
                    "fen": start_fen,
                    "result": result,
                    "moves": len(states)
                }) + "\n")

        # =========================
        # TRAIN STEP
        # =========================

        losses = trainer.train_step()

        epoch_time = time.time() - epoch_start

        logger.log_info(
            f"Epoch {epoch} | Games {len(results)} | "
            f"Loss {losses['combined_loss']:.4f} | Time {epoch_time:.2f}s"
        )

        # =========================
        # SAVE CHECKPOINT
        # =========================

        trainer.save_checkpoint(checkpoint_path)

        if epoch % 50 == 0:
            trainer.save_checkpoint(
                os.path.join(BASE_DIR, "checkpoints", f"epoch_{epoch}.pt")
            )

        epoch += 1


# =========================
# MAIN
# =========================

def main():
    os.makedirs(os.path.join(BASE_DIR, "checkpoints"), exist_ok=True)
    os.makedirs(os.path.join(BASE_DIR, "exports"), exist_ok=True)
    os.makedirs(os.path.join(BASE_DIR, "logs"), exist_ok=True)

    setup_logging(level=logging.INFO)

    parser = argparse.ArgumentParser()
    parser.add_argument("--simulations", type=int, default=200)
    parser.add_argument("--games-per-epoch", type=int, default=5)
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--parallel", action="store_true")

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