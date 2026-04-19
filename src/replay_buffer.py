import numpy as np
import torch
import traceback
from typing import List, Tuple, Optional
from src.logger import get_logger

logger = get_logger()


class ReplayBuffer:
    def __init__(self, max_size: int = 500000, batch_size: int = 2048):
        try:
            logger.log_debug(f"Initializing ReplayBuffer with max_size={max_size}, batch_size={batch_size}")
            self.max_size = max_size
            self.batch_size = batch_size
            self.buffer: List[Tuple[torch.Tensor, np.ndarray, int]] = []
            logger.log_info(f"ReplayBuffer initialized successfully")
        except Exception as e:
            logger.log_exception(e, "ReplayBuffer.__init__")
            raise

    def add_game(self, states: List[torch.Tensor], policy_targets: List[np.ndarray], value_targets: List[int]):
        try:
            logger.log_debug(f"Adding game with {len(states)} states to replay buffer")
            for i, (state, policy, value) in enumerate(zip(states, policy_targets, value_targets)):
                if len(self.buffer) >= self.max_size:
                    self.buffer.pop(0)
                self.buffer.append((state, policy, value))
            logger.log_debug(f"Added game to buffer. Buffer size: {len(self.buffer)}/{self.max_size}")
        except Exception as e:
            logger.log_exception(e, "ReplayBuffer.add_game")
            # Don't raise here to prevent crashing training loop for buffer issues
            pass

    def sample(self) -> Tuple[Optional[torch.Tensor], Optional[torch.Tensor], Optional[torch.Tensor]]:
        try:
            logger.log_debug(f"Sampling from replay buffer. Buffer size: {len(self.buffer)}, batch_size: {self.batch_size}")
            if len(self.buffer) < self.batch_size:
                if len(self.buffer) == 0:
                    logger.log_warning("Replay buffer is empty, returning None")
                    return None, None, None
                logger.log_debug(f"Buffer has {len(self.buffer)} samples (less than batch_size {self.batch_size}), sampling with replacement")
                indices = np.random.choice(len(self.buffer), self.batch_size, replace=True)
            else:
                logger.log_debug(f"Sampling {self.batch_size} samples from buffer")
                indices = np.random.choice(len(self.buffer), self.batch_size, replace=False)

            logger.log_debug(f"Selected {len(indices)} indices for sampling")
            states = torch.cat([self.buffer[i][0] for i in indices], dim=0)
            policies = torch.tensor(np.array([self.buffer[i][1] for i in indices]), dtype=torch.float32)
            values = torch.tensor(np.array([self.buffer[i][2] for i in indices]), dtype=torch.float32)
            
            logger.log_debug(f"Sampled batch: states={states.shape}, policies={policies.shape}, values={values.shape}")
            return states, policies, values
        except Exception as e:
            logger.log_exception(e, "ReplayBuffer.sample")
            # Return None values to prevent crashing training loop
            return None, None, None

    def __len__(self) -> int:
        try:
            length = len(self.buffer)
            logger.log_debug(f"ReplayBuffer length: {length}")
            return length
        except Exception as e:
            logger.log_exception(e, "ReplayBuffer.__len__")
            return 0

    def fill_percentage(self) -> float:
        try:
            if self.max_size > 0:
                percentage = len(self.buffer) / self.max_size
                logger.log_debug(f"Buffer fill percentage: {percentage:.2%}")
                return percentage
            else:
                logger.log_warning("Max size is zero, returning 0.0 for fill percentage")
                return 0.0
        except Exception as e:
            logger.log_exception(e, "ReplayBuffer.fill_percentage")
            return 0.0