import time
import psutil
import torch
from datetime import datetime
from typing import Dict, Any, List, Optional
from rich.console import Console
from rich.layout import Layout
from rich.panel import Panel
from rich.text import Text
from rich.live import Live
from rich.table import Table
from rich.progress import Progress, BarColumn, TextColumn
import os


class Dashboard:
    def __init__(self):
        self.console = Console()
        self.start_time = time.time()
        
        self.metrics = {
            "epoch": 0,
            "games_generated": 0,
            "policy_loss": 0.0,
            "value_loss": 0.0,
            "combined_loss": 0.0,
            "learning_rate": 0.0,
            "estimated_elo": 1200,
            "elo_change": 0,
            "buffer_fill": 0.0,
            "training_mode": "Mid-game self-play",
            "recent_games": [],
            "loss_history": [],
            "wdl_ratio": {"win": 0, "draw": 0, "loss": 0},
        }

    def update_metrics(self, metrics: Dict[str, Any]):
        self.metrics.update(metrics)
        if "combined_loss" in metrics:
            self.metrics["loss_history"].append(metrics["combined_loss"])
            if len(self.metrics["loss_history"]) > 20:
                self.metrics["loss_history"].pop(0)

    def get_gpu_info(self) -> Dict[str, str]:
        if torch.cuda.is_available():
            return {
                "name": torch.cuda.get_device_name(0),
                "memory": f"{torch.cuda.memory_allocated() / 1024**3:.1f}GB / {torch.cuda.get_device_properties(0).total_memory / 1024**3:.1f}GB"
            }
        return {"name": "N/A", "memory": "N/A"}

    def get_cpu_usage(self) -> str:
        return f"{psutil.cpu_percent()}%"

    def get_ram_usage(self) -> str:
        mem = psutil.virtual_memory()
        return f"{mem.percent}% ({mem.used / 1024**3:.1f}GB / {mem.total / 1024**3:.1f}GB)"

    def get_disk_usage(self) -> str:
        disk = psutil.disk_usage("C:\\")
        return f"{disk.percent}% ({disk.used / 1024**3:.1f}GB / {disk.total / 1024**3:.1f}GB)"

    def _generate_sparkline(self, values: List[float]) -> str:
        if not values:
            return "█" * 10
        
        min_val = min(values)
        max_val = max(values)
        range_val = max_val - min_val if max_val != min_val else 1
        
        chars = " ▁▂▃▄▅▆▇█"
        result = ""
        for v in values:
            idx = int((v - min_val) / range_val * 7)
            result += chars[idx]
        
        return result

    def render(self) -> Layout:
        layout = Layout()
        
        header = Panel(
            Text(f"ChessAI Training | {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}", justify="center"),
            title=self.metrics.get("training_mode", "Training"),
            border_style="cyan"
        )

        left_panel = Panel(
            f"Epoch: {self.metrics.get('epoch', 0)}\n"
            f"Games Generated: {self.metrics.get('games_generated', 0)}\n"
            f"Buffer: {self.metrics.get('buffer_fill', 0):.1%}\n"
            f"Learning Rate: {self.metrics.get('learning_rate', 0):.6f}",
            title="Training Metrics",
            border_style="green"
        )

        center_panel = Panel(
            f"Policy Loss: {self.metrics.get('policy_loss', 0):.4f}\n"
            f"Value Loss: {self.metrics.get('value_loss', 0):.4f}\n"
            f"Combined Loss: {self.metrics.get('combined_loss', 0):.4f}\n"
            f"Loss Trend: {self._generate_sparkline(self.metrics.get('loss_history', []))}\n"
            f"Estimated Elo: {self.metrics.get('estimated_elo', 1200)} ({self._get_elo_arrow()})",
            title="Loss & Performance",
            border_style="yellow"
        )

        right_panel = Panel(
            f"GPU: {self.get_gpu_info()['name']}\n"
            f"VRAM: {self.get_gpu_info()['memory']}\n"
            f"CPU: {self.get_cpu_usage()}\n"
            f"RAM: {self.get_ram_usage()}\n"
            f"Disk: {self.get_disk_usage()}",
            title="System Resources",
            border_style="red"
        )

        wdl = self.metrics.get("wdl_ratio", {"win": 0, "draw": 0, "loss": 0})
        total = wdl["win"] + wdl["draw"] + wdl["loss"]
        if total > 0:
            wdl_bar = f"Win: {wdl['win']/total:.1%} | Draw: {wdl['draw']/total:.1%} | Loss: {wdl['loss']/total:.1%}"
        else:
            wdl_bar = "No games yet"

        bottom_panel = Panel(
            wdl_bar + "\n" +
            "Press Ctrl+C to stop training and auto-export model.onnx",
            title="Status",
            border_style="blue"
        )

        layout.split_column(
            Layout(header, size=3),
            Layout().split_row(
                Layout(left_panel),
                Layout(center_panel),
                Layout(right_panel)
            ),
            Layout(bottom_panel, size=5)
        )

        return layout

    def _get_elo_arrow(self) -> str:
        elo_change = self.metrics.get("elo_change", 0)
        if elo_change > 0:
            return "↑"
        elif elo_change < 0:
            return "↓"
        return "→"

    def start(self):
        pass

    def stop(self):
        pass