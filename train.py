import os
import sys
import signal
import argparse
import time
import csv
import json
from datetime import datetime
import time

import torch

BASE_DIR = r"C:\Users\prata\agent01"

os.makedirs(os.path.join(BASE_DIR, "checkpoints"), exist_ok=True)
os.makedirs(os.path.join(BASE_DIR, "exports"), exist_ok=True)
os.makedirs(os.path.join(BASE_DIR, "logs"), exist_ok=True)


def signal_handler(sig, frame):
    print("\n\nCtrl+C received. Saving checkpoint and exporting ONNX...")
    save_and_export()
    print("\nTraining stopped. Checkpoint saved.")
    sys.exit(0)


def save_and_export():
    try:
        from src.trainer import Trainer
        from src.network import create_network
        from src.export import export_to_onnx
        
        checkpoint_path = os.path.join(BASE_DIR, "checkpoints", "checkpoint_latest.pt")
        if os.path.exists(checkpoint_path):
            network = create_network()
            trainer = Trainer(network, None)
            trainer.save_checkpoint(checkpoint_path)
            
            onnx_path = os.path.join(BASE_DIR, "exports", "model.onnx")
            export_to_onnx(network, onnx_path, checkpoint_path)
    except Exception as e:
        print(f"Error during save/export: {e}")


def main():
    parser = argparse.ArgumentParser(description="Chess AI Training")
    parser.add_argument("--epochs-before-opening", type=int, default=200,
                        help="Number of mid-game epochs before opening training")
    parser.add_argument("--simulations", type=int, default=800,
                        help="Number of MCTS simulations per move")
    parser.add_argument("--games-per-epoch", type=int, default=64,
                        help="Number of self-play games per epoch")
    parser.add_argument("--resume", action="store_true",
                        help="Resume from checkpoint_latest.pt")
    args = parser.parse_args()

    print("=" * 60)
    print("ChessAI Training Engine")
    print("=" * 60)
    print(f"Epochs before opening: {args.epochs_before_opening}")
    print(f"MCTS simulations: {args.simulations}")
    print(f"Games per epoch: {args.games_per_epoch}")
    print(f"Resume: {args.resume}")
    print("=" * 60)
    print()

    from src.network import create_network
    from src.replay_buffer import ReplayBuffer
    from src.trainer import Trainer
    from src.self_play import play_game
    from src.opening_trainer import OpeningTrainer
    from src.dashboard import Dashboard

    network = create_network()
    buffer = ReplayBuffer(max_size=500000, batch_size=2048)
    trainer = Trainer(network, buffer, learning_rate=0.001, weight_decay=1e-4)
    opening_trainer = OpeningTrainer()
    dashboard = Dashboard()

    checkpoint_path = os.path.join(BASE_DIR, "checkpoints", "checkpoint_latest.pt")
    start_epoch = 0
    if args.resume and os.path.exists(checkpoint_path):
        if trainer.load_checkpoint(checkpoint_path):
            start_epoch = 1
            print(f"Resumed from checkpoint: {checkpoint_path}")

    training_log_path = os.path.join(BASE_DIR, "logs", "training_log.csv")
    if not os.path.exists(training_log_path):
        with open(training_log_path, 'w', newline='') as f:
            writer = csv.writer(f)
            writer.writerow(["epoch", "timestamp", "policy_loss", "value_loss", "combined_loss", "games_generated", "estimated_elo", "training_mode"])

    game_log_path = os.path.join(BASE_DIR, "logs", "game_log.jsonl")

    signal.signal(signal.SIGINT, signal_handler)

    epoch = start_epoch
    total_games = 0
    best_elo = 1200
    games_this_session = 0

    mcts_config = {
        "cpuct": 1.25,
        "num_simulations": args.simulations,
        "temperature": 1.0,
        "dirichlet_alpha": 0.3,
        "dirichlet_epsilon": 0.25
    }

    try:
        while True:
            if epoch < args.epochs_before_opening:
                training_mode = "midgame"
                mcts_config["temperature"] = 1.0
            else:
                if epoch % 4 == 0:
                    training_mode = "opening"
                    mcts_config["temperature"] = 1.5
                    if epoch % 4 == 0:
                        pass
                else:
                    training_mode = "midgame"
                    mcts_config["temperature"] = 1.0

            epoch_start_time = time.time()
            epoch_games = 0

            for _ in range(args.games_per_epoch):
                states, policy_targets, value_targets, start_fen, game_result = play_game(
                    network, mcts_config, training_mode
                )

                buffer.add_game(states, policy_targets, value_targets)

                game_data = {
                    "epoch": epoch,
                    "start_fen": start_fen,
                    "result": game_result,
                    "move_count": len(states),
                }
                with open(game_log_path, 'a') as f:
                    f.write(json.dumps(game_data) + "\n")

                total_games += 1
                games_this_session += 1
                epoch_games += 1

            losses = trainer.train_step()

            epoch_time = time.time() - epoch_start_time

            current_elo = best_elo + int((losses["combined_loss"] - 1.0) * -100)

            with open(training_log_path, 'a', newline='') as f:
                writer = csv.writer(f)
                writer.writerow([
                    epoch,
                    datetime.now().isoformat(),
                    losses["policy_loss"],
                    losses["value_loss"],
                    losses["combined_loss"],
                    total_games,
                    current_elo,
                    training_mode
                ])

            dashboard.update_metrics({
                "epoch": epoch,
                "games_generated": total_games,
                "policy_loss": losses["policy_loss"],
                "value_loss": losses["value_loss"],
                "combined_loss": losses["combined_loss"],
                "learning_rate": trainer.get_learning_rate(),
                "estimated_elo": current_elo,
                "buffer_fill": buffer.fill_percentage(),
                "training_mode": "Mid-game self-play" if training_mode == "midgame" else "Opening training"
            })

            print(f"Epoch {epoch} | Mode: {training_mode} | Loss: {losses['combined_loss']:.4f} | "
                  f"Games: {total_games} | Buffer: {buffer.fill_percentage():.1%} | Time: {epoch_time:.1f}s")

            checkpoint_path = os.path.join(BASE_DIR, "checkpoints", "checkpoint_latest.pt")
            trainer.save_checkpoint(checkpoint_path)

            if epoch % 50 == 0:
                epoch_checkpoint = os.path.join(BASE_DIR, "checkpoints", f"checkpoint_epoch_{epoch}.pt")
                trainer.save_checkpoint(epoch_checkpoint)

            if current_elo > best_elo:
                best_elo = current_elo
                best_checkpoint = os.path.join(BASE_DIR, "checkpoints", "checkpoint_best.pt")
                trainer.save_checkpoint(best_checkpoint)

            epoch += 1

    except KeyboardInterrupt:
        pass
    finally:
        print("\n\nTraining session summary:")
        print(f"  Total epochs: {epoch}")
        print(f"  Total games: {total_games}")
        print(f"  Best Elo: {best_elo}")
        print("\nSaving final checkpoint...")
        
        checkpoint_path = os.path.join(BASE_DIR, "checkpoints", "checkpoint_latest.pt")
        trainer.save_checkpoint(checkpoint_path)
        
        onnx_path = os.path.join(BASE_DIR, "exports", "model.onnx")
        from src.export import export_to_onnx
        export_to_onnx(network, onnx_path, checkpoint_path)
        
        print("Done!")


if __name__ == "__main__":
    main()