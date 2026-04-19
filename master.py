import os
import sys
import argparse
import time
import csv
import json
import subprocess
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

    output_exe = os.path.join(build_dir, "ChessEngine_new.exe")
    final_exe = os.path.join(build_dir, "ChessEngine.exe")

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
    else:
        error_msg = f"Expected output executable not found: {output_exe}"
        logger.log_error(error_msg)
        print(f"Build failed: {error_msg}")
        raise FileNotFoundError(error_msg)


def run_training(args):
    try:
        logger.log_info("Starting run_training function")
        from rich.console import Console
        from rich.panel import Panel
        from rich.text import Text
        from rich.table import Table
        from rich.live import Live
        from rich.console import Group

        console = Console()
        
        logger.log_info("Importing training modules")
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

        logger.log_info("Setting up dashboard creation function")
        
        # Data tracking for graphs (declared in outer scope already)
        
        def create_elo_graph():
            if len(elo_history) < 2:
                return "No data yet"
            # Simple ASCII bar graph
            lines = []
            min_elo = min(elo_history)
            max_elo = max(elo_history)
            range_elo = max(max_elo - min_elo, 1)
            height = 8
            for h in range(height, -1, -1):
                threshold = min_elo + (range_elo * h / height)
                line = ""
                for elo in elo_history[-20:]:
                    if elo >= threshold:
                        line += "█"
                    else:
                        line += " "
                lines.append(line)
            return "\n".join(lines)
        
        def create_moves_graph():
            if len(moves_history) < 2:
                return "No data yet"
            # Simple ASCII bar graph for moves per game
            lines = []
            max_moves = max(moves_history[-20:]) if moves_history[-20:] else 1
            height = 8
            for h in range(height, -1, -1):
                threshold = max_moves * h / height
                line = ""
                for moves in moves_history[-20:]:
                    if moves >= threshold:
                        line += "█"
                    else:
                        line += " "
                lines.append(line)
            return "\n".join(lines)
        
        def make_progress_bar(current, total, width=20):
            filled = int(width * current / max(total, 1))
            return "[" + "█" * filled + "░" * (width - filled) + "]"
        
        def make_dashboard():
            try:
                # Calculate win rate from recent games
                recent_games = min(100, total_games)
                game_results_list = []
                if os.path.exists(game_log_path):
                    try:
                        with open(game_log_path, 'r') as f:
                            for line in f:
                                try:
                                    game_results_list.append(json.loads(line).get('result', 0))
                                except:
                                    pass
                        game_results_list = game_results_list[-100:]
                    except:
                        pass
                
                wins = sum(1 for r in game_results_list if r == 1)
                draws = sum(1 for r in game_results_list if r == 0)
                losses = sum(1 for r in game_results_list if r == -1)
                total_recent = wins + draws + losses
                win_rate = (wins / max(total_recent, 1)) * 100
                
                # Stats panel
                stats_table = Table(box=None, padding=(0, 1), show_header=False)
                stats_table.add_column(width=18)
                stats_table.add_column(width=18)
                
                elo_color = "green" if game_state['elo'] >= best_elo else "red"
                mode_color = "cyan" if game_state['mode'] == "Opening" else "yellow"
                
                stats_table.add_row(
                    f"[bold]Epoch:[/] [cyan]{game_state['epoch']}[/]",
                    f"[bold]Elo:[/] [{elo_color}]{game_state['elo']}[/]"
                )
                stats_table.add_row(
                    f"[bold]Total Games:[/] {game_state['games']}",
                    f"[bold]Best Elo:[/] [green]{best_elo}[/]"
                )
                stats_table.add_row(
                    f"[bold]Training:[/] [{mode_color}]{game_state['mode']}[/]",
                    f"[bold]Sims:[/] {args.simulations}"
                )
                stats_table.add_row(
                    f"[bold]Loss:[/] [cyan]{game_state['combined_loss']:.4f}[/]",
                    f"[bold]Time:[/] {game_state['epoch_time']:.1f}s"
                )
                
                # Win rate panel
                winrate_table = Table(box=None, padding=(0, 1), show_header=False)
                winrate_table.add_column(width=25)
                winrate_table.add_row(f"[bold]Last 100 Games:[/]")
                winrate_table.add_row(f"[green]W:[/] {wins} ({wins/max(total_recent,1)*100:.0f}%)  [yellow]D:[/] {draws} ({draws/max(total_recent,1)*100:.0f}%)  [red]L:[/] {losses} ({losses/max(total_recent,1)*100:.0f}%)")
                winrate_bar_len = 20
                win_bar = int(win_rate * winrate_bar_len / 100)
                winrate_table.add_row(f"[green]{'█' * win_bar}[/][red]{'░' * (winrate_bar_len - win_bar)}[/] {win_rate:.1f}%")
                
                # Progress bars
                progress_table = Table(box=None, padding=(0, 1), show_header=False)
                progress_table.add_column(width=30)
                
                epoch_bar = make_progress_bar(current_game_num, args.games_per_epoch, 25)
                progress_table.add_row(f"[bold]Game:[/] {epoch_bar} {current_game_num}/{args.games_per_epoch}")
                
                buffer_bar = make_progress_bar(int(buffer.fill_percentage() * 25), 25)
                progress_table.add_row(f"[bold]Buffer:[/] {buffer_bar} {game_state['buffer_pct']:.0%}")
                
                # Graphs
                graph_table = Table(box=None, padding=(0, 1), show_header=False)
                graph_table.add_column(width=24)
                graph_table.add_column(width=24)
                
                elo_graph = create_elo_graph()
                moves_graph = create_moves_graph()
                
                graph_table.add_row(
                    Panel(elo_graph, title="[bold]Elo History", border_style="white", padding=(0, 0)),
                    Panel(moves_graph, title="[bold]Moves/Game", border_style="white", padding=(0, 0))
                )
                
                # Main layout
                stats_panel = Panel(
                    stats_table,
                    title="[bold]Training Stats",
                    border_style="white",
                    padding=(1, 2)
                )
                
                winrate_panel = Panel(
                    winrate_table,
                    title="[bold]Win Rate",
                    border_style="white",
                    padding=(1, 2)
                )
                
                progress_panel = Panel(
                    progress_table,
                    title="[bold]Progress",
                    border_style="white",
                    padding=(1, 2)
                )
                
                graphs_panel = Panel(
                    graph_table,
                    border_style="white",
                    padding=(1, 1)
                )
                
                status_text = "[bold yellow]Press ESC to stop training and build engine[/]"
                
                return Group(
                    stats_panel,
                    winrate_panel,
                    progress_panel,
                    graphs_panel,
                    Panel(status_text, padding=(0, 1), border_style="white")
                )
            except Exception as e:
                logger.log_exception(e, "make_dashboard")
                return Group(
                    Panel("Dashboard Error", border_style="white"),
                    Panel("[bold yellow]ESC to stop & build[/]", padding=(0, 1), border_style="white")
                )

        logger.log_info("Setting up move callback function")
        def on_move(info):
            try:
                game_state['current_move'] = info.get('move_num', 0)
                game_state['last_moves'] = info.get('last_moves', [])
                game_state['sims'] = info.get('simulations', args.simulations)
            except Exception as e:
                logger.log_exception(e, "on_move callback")
                game_state['current_move'] = 0
                game_state['last_moves'] = []
                game_state['sims'] = args.simulations

        logger.log_info("Printing configuration")
        logger.log_info(f"Config: {args.simulations} sims, {args.games_per_epoch} games/epoch")
        
        logger.log_info("Starting main training loop")
        try:
            with Live(make_dashboard(), console=console, refresh_per_second=4, transient=False) as live:
                logger.log_info("Entered Live display context")
                while True:
                    try:
                        if kb_hit():
                            key = get_key()
                            if key and key == b'\x1b':
                                logger.log_info("ESC key pressed, stopping training")
                                break

                        if epoch < args.epochs_before_opening:
                            training_mode = "midgame"
                            game_state['mode'] = "Mid-game"
                            mcts_config["temperature"] = 1.0
                            logger.log_debug(f"Epoch {epoch}: Using midgame mode")
                        else:
                            if epoch % 4 == 0:
                                training_mode = "opening"
                                game_state['mode'] = "Opening"
                                mcts_config["temperature"] = 1.5
                                logger.log_debug(f"Epoch {epoch}: Using opening mode")
                            else:
                                training_mode = "midgame"
                                game_state['mode'] = "Mid-game"
                                mcts_config["temperature"] = 1.0
                                logger.log_debug(f"Epoch {epoch}: Using midgame mode")

                        game_state['current_move'] = 0
                        game_state['last_moves'] = []
                        game_state['game_result'] = "-"

                        epoch_start = time.time()

                        logger.log_info(f"Starting epoch {epoch} with {args.games_per_epoch} games")
                        for g in range(args.games_per_epoch):
                            try:
                                if kb_hit() and get_key() == b'\x1b':
                                    logger.log_info("ESC key pressed during game generation, stopping training")
                                    raise KeyboardInterrupt()

                                logger.log_debug(f"Starting game {g+1}/{args.games_per_epoch} in epoch {epoch}")
                                states, policy_targets, value_targets, start_fen, game_result = play_game(
                                    network, mcts_config, training_mode,
                                    max_think_time=0.05,
                                    move_callback=on_move
                                )

                                logger.log_debug(f"Game {g+1} completed, adding to buffer")
                                buffer.add_game(states, policy_targets, value_targets)
                                
                                moves_history.append(len(states))
                                current_game_num = g + 1

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

                                live.update(make_dashboard())
                                logger.log_debug(f"Updated dashboard after game {g+1}")
                            except Exception as e:
                                logger.log_exception(e, f"game {g+1} in epoch {epoch}")
                                # Continue with next game rather than stopping entire epoch
                                continue

                        logger.log_info(f"Completed epoch {epoch}, running training step")
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
                                total_games, current_elo, training_mode
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
                        live.update(make_dashboard())
                    except Exception as e:
                        logger.log_exception(e, "training loop iteration")
                        # Continue training despite errors in individual iterations
                        continue

        except Exception as e:
            logger.log_exception(e, "live display context")
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
        parser.add_argument("--epochs-before-opening", type=int, default=999999999)
        parser.add_argument("--simulations", type=int, default=200)
        parser.add_argument("--games-per-epoch", type=int, default=32)
        parser.add_argument("--resume", action="store_true")
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