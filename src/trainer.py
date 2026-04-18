import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import DataLoader, TensorDataset
from typing import Dict, Any, Optional, Tuple
import numpy as np
from src.network import create_network
from src.replay_buffer import ReplayBuffer


class Trainer:
    def __init__(self, network: nn.Module, buffer: ReplayBuffer, 
                 learning_rate: float = 0.001, weight_decay: float = 1e-4,
                 batch_size: int = 2048):
        self.network = network
        self.buffer = buffer
        self.batch_size = batch_size

        self.optimizer = optim.Adam(network.parameters(), lr=learning_rate, weight_decay=weight_decay)
        self.scheduler = optim.lr_scheduler.CosineAnnealingWarmRestarts(self.optimizer, T_0=50, T_mult=1)
        
        self.policy_loss_fn = nn.CrossEntropyLoss()
        self.value_loss_fn = nn.MSELoss()

        self.current_policy_loss = 0.0
        self.current_value_loss = 0.0
        self.current_combined_loss = 0.0

    def train_step(self) -> Dict[str, float]:
        states, policies, values = self.buffer.sample()
        
        if states is None:
            return {"policy_loss": 0.0, "value_loss": 0.0, "combined_loss": 0.0}

        self.network.train()
        self.optimizer.zero_grad()

        policy_logits, value_outputs = self.network(states)

        policy_loss = self.policy_loss_fn(policy_logits, policies.argmax(dim=1))
        value_loss = self.value_loss_fn(value_outputs, values)
        
        combined_loss = policy_loss + value_loss
        
        combined_loss.backward()
        torch.nn.utils.clip_grad_norm_(self.network.parameters(), max_norm=1.0)
        self.optimizer.step()
        self.scheduler.step()

        self.current_policy_loss = policy_loss.item()
        self.current_value_loss = value_loss.item()
        self.current_combined_loss = combined_loss.item()

        return {
            "policy_loss": self.current_policy_loss,
            "value_loss": self.current_value_loss,
            "combined_loss": self.current_combined_loss
        }

    def get_learning_rate(self) -> float:
        return self.optimizer.param_groups[0]['lr']

    def save_checkpoint(self, path: str):
        torch.save({
            'network_state_dict': self.network.state_dict(),
            'optimizer_state_dict': self.optimizer.state_dict(),
            'scheduler_state_dict': self.scheduler.state_dict(),
        }, path)

    def load_checkpoint(self, path: str) -> bool:
        try:
            checkpoint = torch.load(path)
            self.network.load_state_dict(checkpoint['network_state_dict'])
            self.optimizer.load_state_dict(checkpoint['optimizer_state_dict'])
            self.scheduler.load_state_dict(checkpoint['scheduler_state_dict'])
            return True
        except:
            return False