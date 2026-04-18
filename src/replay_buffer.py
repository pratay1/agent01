import numpy as np
import torch
from typing import List, Tuple


class ReplayBuffer:
    def __init__(self, max_size: int = 500000, batch_size: int = 2048):
        self.max_size = max_size
        self.batch_size = batch_size
        self.buffer: List[Tuple[torch.Tensor, np.ndarray, int]] = []

    def add_game(self, states: List[torch.Tensor], policy_targets: List[np.ndarray], value_targets: List[int]):
        for state, policy, value in zip(states, policy_targets, value_targets):
            if len(self.buffer) >= self.max_size:
                self.buffer.pop(0)
            self.buffer.append((state, policy, value))

    def sample(self) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        if len(self.buffer) < self.batch_size:
            if len(self.buffer) == 0:
                return None, None, None
            indices = np.random.choice(len(self.buffer), len(self.buffer), replace=False)
        else:
            indices = np.random.choice(len(self.buffer), self.batch_size, replace=False)

        states = torch.cat([self.buffer[i][0] for i in indices], dim=0)
        policies = torch.tensor(np.array([self.buffer[i][1] for i in indices]), dtype=torch.float32)
        values = torch.tensor(np.array([self.buffer[i][2] for i in indices]), dtype=torch.float32)

        return states, policies, values

    def __len__(self) -> int:
        return len(self.buffer)

    def fill_percentage(self) -> float:
        return len(self.buffer) / self.max_size if self.max_size > 0 else 0.0