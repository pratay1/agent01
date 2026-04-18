import os
import sys
import signal
import argparse
import time
import csv
import json
import subprocess
from datetime import datetime

import torch

BASE_DIR = r"C:\Users\prata\agent01"

os.makedirs(os.path.join(BASE_DIR, "checkpoints"), exist_ok=True)
os.makedirs(os.path.join(BASE_DIR, "exports"), exist_ok=True)
os.makedirs(os.path.join(BASE_DIR, "logs"), exist_ok=True)


def save_checkpoint_and_export(network, trainer):
    print("\n\nSaving checkpoint and exporting ONNX...")
    checkpoint_path = os.path.join(BASE_DIR, "checkpoints", "checkpoint_latest.pt")
    trainer.save_checkpoint(checkpoint_path)

    from src.export import export_to_onnx
    onnx_path = os.path.join(BASE_DIR, "exports", "model.onnx")
    export_to_onnx(network, onnx_path, checkpoint_path)
    return onnx_path


def build_engine():
    print("\n\nBuilding C# chess engine...")
    engine_dir = os.path.join(BASE_DIR, "engine")
    build_dir = os.path.join(BASE_DIR, "build")

    result = subprocess.run(
        ["dotnet", "publish", "-c", "Release", "-r", "win-x64", "--self-contained", "true",
         "-p:PublishSingleFile=true", "-o", build_dir, "-p:OutputName=ChessEngine_new"],
        cwd=engine_dir,
        capture_output=True,
        text=True
    )

    if result.returncode != 0:
        print(f"Build failed: {result.stderr}")
        return False

    output_exe = os.path.join(build_dir, "ChessEngine_new.exe")
    final_exe = os.path.join(build_dir, "ChessEngine.exe")

    if os.path.exists(output_exe):
        if os.path.exists(final_exe):
            os.remove(final_exe)
        os.rename(output_exe, final_exe)
        print(f"Engine built and replaced: {final_exe}")
        return True
    return False


def run_training(args):
    from rich.console import Console
    from rich.panel import Panel
    from rich.text import Text
    from rich.table import Table
    from rich.live import Live
    from rich.console import Group

    console = Console()

    console.print("[bold cyan]╔═══════════════════════════════════════════════════════════╗[/]")
    console.print("[bold cyan]║           ChessAI Training Engine (v2.0)                 ║[/]")
    console.print("[bold cyan]╚═══════════════════════════════════════════════════════════╝[/]")
    console.print("[yellow]Press ESC to stop training, save model, and build new engine[/]")
    console.print()

    from src.network import create_network
    from src.replay_buffer import ReplayBuffer
    from src.trainer import Trainer
    from src.self_play import play_game

    network = create_network()
    buffer = ReplayBuffer(max_size=500000, batch_size=2048)
    trainer = Trainer(network, buffer, learning_rate=0.001, weight_decay=1e-4)

    checkpoint_path = os.path.join(BASE_DIR, "checkpoints", "checkpoint_latest.pt")
    start_epoch = 0
    if args.resume and os.path.exists(checkpoint_path):
        if trainer.load_checkpoint(checkpoint_path):
            start_epoch = 1

    training_log_path = os.path.join(BASE_DIR, "logs", "training_log.csv")
    if not os.path.exists(training_log_path):
        with open(training_log_path, 'w', newline='') as f:
            writer = csv.writer(f)
            writer.writerow(["epoch", "timestamp", "policy_loss", "value_loss", "combined_loss", "games_generated", "estimated_elo", "training_mode"])

    game_log_path = os.path.join(BASE_DIR, "logs", "game_log.jsonl")

    epoch = start_epoch
    total_games = 0
    best_elo = 1200

    mcts_config = {
        "cpuct": 1.25,
        "num_simulations": args.simulations,
        "temperature": 1.0,
        "dirichlet_alpha": 0.3,
        "dirichlet_epsilon": 0.25
    }

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

    import msvcrt
    def kb_hit():
        return msvcrt.kbhit()

    def get_key():
        if msvcrt.kbhit():
            return msvcrt.getch()
        return None

    def make_dashboard():
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

    def on_move(info):
        game_state['current_move'] = info['move_num']
        game_state['last_moves'] = info['last_moves']
        game_state['sims'] = info['simulations']

    console.print(f"[dim]Config: {args.simulations} sims, {args.games_per_epoch} games/epoch[/]")
    
    try:
        with Live(make_dashboard(), console=console, refresh_per_second=4, transient=False) as live:
            while True:
                if kb_hit():
                    key = get_key()
                    if key and key == b'\x1b':
                        console.print("\n[bold red]>>> ESC pressed! Stopping training...[/]")
                        break

                if epoch < args.epochs_before_opening:
                    training_mode = "midgame"
                    game_state['mode'] = "Mid-game"
                    mcts_config["temperature"] = 1.0
                else:
                    if epoch % 4 == 0:
                        training_mode = "opening"
                        game_state['mode'] = "Opening"
                        mcts_config["temperature"] = 1.5
                    else:
                        training_mode = "midgame"
                        game_state['mode'] = "Mid-game"
                        mcts_config["temperature"] = 1.0

                game_state['current_move'] = 0
                game_state['last_moves'] = []
                game_state['game_result'] = "-"

                epoch_start = time.time()

                for g in range(args.games_per_epoch):
                    if kb_hit() and get_key() == b'\x1b':
                        console.print("\n[bold red]>>> ESC pressed! Stopping training...[/]")
                        raise KeyboardInterrupt()

                    states, policy_targets, value_targets, start_fen, game_result = play_game(
                        network, mcts_config, training_mode,
                        max_think_time=1.0,
                        move_callback=on_move
                    )

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

                losses = trainer.train_step()
                epoch_elapsed = time.time() - epoch_start

                current_elo = best_elo + int((losses["combined_loss"] - 1.0) * -100)

                game_state['epoch'] = epoch
                game_state['policy_loss'] = losses['policy_loss']
                game_state['value_loss'] = losses['value_loss']
                game_state['combined_loss'] = losses['combined_loss']
                game_state['elo'] = current_elo
                game_state['epoch_time'] = epoch_elapsed

                with open(training_log_path, 'a', newline='') as f:
                    writer = csv.writer(f)
                    writer.writerow([
                        epoch, datetime.now().isoformat(),
                        losses["policy_loss"], losses["value_loss"], losses["combined_loss"],
                        total_games, current_elo, training_mode
                    ])

                checkpoint_path = os.path.join(BASE_DIR, "checkpoints", "checkpoint_latest.pt")
                trainer.save_checkpoint(checkpoint_path)

                if epoch % 50 == 0:
                    epoch_ckpt = os.path.join(BASE_DIR, "checkpoints", f"checkpoint_epoch_{epoch}.pt")
                    trainer.save_checkpoint(epoch_ckpt)

                if current_elo > best_elo:
                    best_elo = current_elo
                    best_ckpt = os.path.join(BASE_DIR, "checkpoints", "checkpoint_best.pt")
                    trainer.save_checkpoint(best_ckpt)

                epoch += 1
                live.update(make_dashboard())

    except KeyboardInterrupt:
        pass
    except Exception as e:
        console.print(f"[red]Error: {e}[/]")

    console.print("\n[bold yellow]═══════════════════════════════════════════════════════════[/]")
    console.print(f"[bold]  Epochs:[/] {epoch}  |  [bold]Games:[/] {total_games}  |  [bold]Best Elo:[/] {best_elo}")
    console.print("[bold yellow]═══════════════════════════════════════════════════════════[/]")

    save_checkpoint_and_export(network, trainer)
    build_engine()

    console.print("\n[bold green]✓ Done! New engine is ready at build/ChessEngine.exe[/]")


def main():
    parser = argparse.ArgumentParser(description="Chess AI Training")
    parser.add_argument("--epochs-before-opening", type=int, default=999999999)
    parser.add_argument("--simulations", type=int, default=200)
    parser.add_argument("--games-per-epoch", type=int, default=32)
    parser.add_argument("--resume", action="store_true")
    args = parser.parse_args()

    run_training(args)


if __name__ == "__main__":
    main()