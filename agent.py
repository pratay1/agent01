import os
import sys
import signal
import argparse
import time
import csv
import json
import subprocess
import traceback
from datetime import datetime

import torch

# Import our logger
from src.logger import get_logger, logger

# Import logging setup (but don't override our logger)
from logging_setup import setup_logging

BASE_DIR = r"C:\Users\prata\agent01"

os.makedirs(os.path.join(BASE_DIR, "checkpoints"), exist_ok=True)
os.makedirs(os.path.join(BASE_DIR, "exports"), exist_ok=True)
os.makedirs(os.path.join(BASE_DIR, "logs"), exist_ok=True)

# Setup logging (returns a logger, but we'll keep our custom logger for now)
setup_logging()


def save_checkpoint_and_export(network, trainer):
    try:
        logger.log_info("Starting checkpoint saving and ONNX export")
        print("\n\nSaving checkpoint and exporting ONNX...")
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
    except Exception as e:
        logger.error(f"Error in save_checkpoint_and_export: {e}", exc_info=True)
        print(f"[red]Error saving checkpoint/exporting: {e}[/]")
        raise


def build_engine():
    try:
        logger.log_info("Starting C# chess engine build")
        print("\n\nBuilding C# chess engine...")
        engine_dir = os.path.join(BASE_DIR, "engine")
        build_dir = os.path.join(BASE_DIR, "build")

        # Ensure build directory exists
        os.makedirs(build_dir, exist_ok=True)
        logger.log_info(f"Engine directory: {engine_dir}")
        logger.log_info(f"Build directory: {build_dir}")

        result = subprocess.run(
            ["dotnet", "publish", "-c", "Release", "-r", "win-x64", "--self-contained", "true",
             "-p:PublishSingleFile=true", "-o", build_dir, "-p:OutputName=ChessEngine_new"],
            cwd=engine_dir,
            capture_output=True,
            text=True,
            timeout=300  # 5 minute timeout
        )

        if result.returncode != 0:
            error_msg = f"Build failed with return code {result.returncode}: {result.stderr}"
            logger.log_error(error_msg)
            print(f"Build failed: {result.stderr}")
            return False

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
            return True
        else:
            error_msg = f"Expected output executable not found: {output_exe}"
            logger.log_error(error_msg)
            print(f"Build failed: Output executable not found at {output_exe}")
            return False
    except subprocess.TimeoutExpired as e:
        error_msg = "Build process timed out after 5 minutes"
        logger.log_exception(e, "build_engine")
        print(f"Build failed: {error_msg}")
        return False
    except Exception as e:
        logger.log_exception(e, "build_engine")
        print(f"Error in build_engine: {e}")
        return False

        logger.info("Dotnet publish completed successfully")
        logger.debug(f"Build stdout: {result.stdout}")

        output_exe = os.path.join(build_dir, "ChessEngine_new.exe")
        final_exe = os.path.join(build_dir, "ChessEngine.exe")
        
        logger.debug(f"Checking for output executable: {output_exe}")
        if os.path.exists(output_exe):
            logger.info("Output executable found")
            if os.path.exists(final_exe):
                logger.debug(f"Removing existing final executable: {final_exe}")
                os.remove(final_exe)
            logger.debug(f"Renaming {output_exe} to {final_exe}")
            os.rename(output_exe, final_exe)
            print(f"Engine built and replaced: {final_exe}")
            logger.info(f"Engine built successfully: {final_exe}")
            return True
        else:
            error_msg = f"Expected output executable not found: {output_exe}"
            logger.error(error_msg)
            print(f"Build failed: {error_msg}")
            return False
            
    except subprocess.TimeoutExpired:
        error_msg = "Build process timed out after 5 minutes"
        logger.error(error_msg)
        print(f"Build failed: {error_msg}")
        return False
    except Exception as e:
        error_msg = f"Unexpected error during build: {str(e)}"
        logger.error(error_msg, exc_info=True)
        print(f"Build failed: {error_msg}")
        return False


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
        # Use ASCII characters to avoid Unicode encoding issues on Windows
        console.print("[bold cyan]+======================================================+[/]")
        console.print("[bold cyan]|           ChessAI Training Engine (v2.0)                 |[/]")
        console.print("[bold cyan]+======================================================+[/]")
        console.print("[yellow]Press ESC to stop training, save model, and build new engine[/]")
        console.print()

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
        def make_dashboard():
            try:
                t = Table(box=None, padding=(0, 2), show_header=False)
                t.add_column(width=35)
                t.add_column(width=35)
                t.add_column(width=35)

                elo_color = "green" if game_state['elo'] >= best_elo else "red"
                
                t.add_row(
                    f"[bold]Epoch:[/] [cyan]{game_state['epoch']}[/]",
                    f"[bold]Mode:[/] [yellow]{game_state['mode']}[/]",
                    f"[bold]Elo:[/] [{elo_color}]{game_state['elo']}[/]"
                )
                t.add_row(
                    f"[bold]Total Games:[/] {game_state['games']}",
                    f"[bold]Buffer:[/] [green]{game_state['buffer_pct']:.1%}[/]",
                    f"[bold]MCTS Sims:[/] [magenta]{game_state['sims']}[/]"
                )
                t.add_row(
                    f"[bold]Policy Loss:[/] [green]{game_state['policy_loss']:.4f}[/]",
                    f"[bold]Value Loss:[/] [yellow]{game_state['value_loss']:.4f}[/]",
                    f"[bold]Combined:[/] [cyan]{game_state['combined_loss']:.4f}[/]"
                )
                t.add_row(
                    f"[bold]Epoch Time:[/] [green]{game_state['epoch_time']:.1f}s[/]",
                    f"[bold]Best Elo:[/] [yellow]{best_elo}[/]",
                    f"[bold]Games/Epoch:[/] {args.games_per_epoch}"
                )

                moves_display = "  ".join(game_state['last_moves'][-5:]) if game_state['last_moves'] else "[dim]waiting for moves...[/]"
                
                result_color = {
                    "W": "green",
                    "L": "red",
                    "D": "yellow",
                    "-": "dim"
                }.get(str(game_state['game_result']), "white")

                game_panel = Panel(
                    f"[bold]Move:[/] [cyan]{game_state['current_move']}[/]\n"
                    f"[bold]Last 5:[/] {moves_display}\n"
                    f"[bold]Result:[/] [{result_color}]{game_state['game_result']}[/]",
                    title="[bold green]Current Game[/]",
                    border_style="green",
                    padding=(1, 2)
                )

                status_text = "[bold yellow]Press ESC to stop & build[/]"
                
                main_panel = Panel(
                    t,
                    title="[bold cyan]ChessAI Training Dashboard[/]",
                    border_style="cyan",
                    padding=(1, 2)
                )

                return Group(main_panel, game_panel, Panel(status_text, padding=(0, 1)))
            except Exception as e:
                logger.log_exception(e, "make_dashboard")
                # Return a simple fallback dashboard
                return Group(
                    Panel("Dashboard Error", title="Error"),
                    Panel("Dashboard Error", title="Error"),
                    Panel("[bold yellow]Press ESC to stop & build[/]", padding=(0, 1))
                )

        logger.log_info("Setting up move callback function")
        def on_move(info):
            try:
                game_state['current_move'] = info.get('move_num', 0)
                game_state['last_moves'] = info.get('last_moves', [])
                game_state['sims'] = info.get('simulations', args.simulations)
            except Exception as e:
                logger.log_exception(e, "on_move callback")
                # Set default values on error
                game_state['current_move'] = 0
                game_state['last_moves'] = []
                game_state['sims'] = args.simulations

        logger.log_info("Printing configuration")
        console.print(f"[dim]Config: {args.simulations} sims, {args.games_per_epoch} games/epoch[/]")
        
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
                                console.print("\n[bold red]>>> ESC pressed! Stopping training...[/]")
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
                                    console.print("\n[bold red]>>> ESC pressed! Stopping training...[/]")
                                    raise KeyboardInterrupt()

                                logger.log_debug(f"Starting game {g+1}/{args.games_per_epoch} in epoch {epoch}")
                                states, policy_targets, value_targets, start_fen, game_result = play_game(
                                    network, mcts_config, training_mode,
                                    max_think_time=1.0,
                                    move_callback=on_move
                                )

                                logger.log_debug(f"Game {g+1} completed, adding to buffer")
                                buffer.add_game(states, policy_targets, value_targets)

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
        console.print(f"[red]Error: {e}[/]")
        raise  # Re-raise to let outer handlers deal with it

    logger.log_info("Training loop completed, saving final results")
    console.print("\n[bold yellow]+======================================================+[/]")
    console.print(f"[bold]  Epochs:[/] {epoch}  |  [bold]Games:[/] {total_games}  |  [bold]Best Elo:[/] {best_elo}")
    console.print("[bold yellow]+======================================================+[/]")

    save_checkpoint_and_export(network, trainer)
    build_engine()

    console.print("\n[bold green]Done! New engine is ready at build/ChessEngine.exe[/]")
    logger.log_info("Training completed successfully")

def main():
    try:
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