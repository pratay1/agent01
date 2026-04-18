import torch
import torch.nn as nn
import torch.optim as optim
import traceback
from src.logger import get_logger

logger = get_logger()


class Trainer:
    def __init__(self, network: nn.Module, buffer, 
                 learning_rate: float = 0.002, weight_decay: float = 1e-4,
                 batch_size: int = 2048):
        try:
            logger.log_info(f"Initializing Trainer with lr={learning_rate}, weight_decay={weight_decay}, batch_size={batch_size}")
            self.network = network
            self.buffer = buffer
            self.batch_size = batch_size

            self.optimizer = optim.Adam(network.parameters(), lr=learning_rate, weight_decay=weight_decay)
            self.scheduler = optim.lr_scheduler.CosineAnnealingWarmRestarts(self.optimizer, T_0=50, T_mult=1)
            
            self.policy_loss_fn = nn.CrossEntropyLoss()
            self.value_loss_fn = nn.MSELoss()
            
            logger.log_info("Trainer initialized successfully")
        except Exception as e:
            logger.log_exception(e, "Trainer.__init__")
            raise

    def train_step(self) -> dict:
        try:
            logger.log_debug("Starting train_step")
            states, policies, values = self.buffer.sample()
            
            if states is None:
                logger.log_warning("Buffer sample returned None, returning zero losses")
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

            result = {
                "policy_loss": policy_loss.item(),
                "value_loss": value_loss.item(),
                "combined_loss": combined_loss.item()
            }
            logger.log_debug(f"Train step completed: {result}")
            return result
        except Exception as e:
            logger.log_exception(e, "Trainer.train_step")
            # Return zero losses to prevent crashing the training loop
            return {"policy_loss": 0.0, "value_loss": 0.0, "combined_loss": 0.0}

    def get_learning_rate(self) -> float:
        try:
            lr = self.optimizer.param_groups[0]['lr']
            logger.log_debug(f"Learning rate: {lr}")
            return lr
        except Exception as e:
            logger.log_exception(e, "Trainer.get_learning_rate")
            return 0.0

    def save_checkpoint(self, path: str):
        try:
            logger.log_info(f"Saving checkpoint to {path}")
            torch.save({
                'network_state_dict': self.network.state_dict(),
                'optimizer_state_dict': self.optimizer.state_dict(),
                'scheduler_state_dict': self.scheduler.state_dict(),
            }, path)
            logger.log_info(f"Checkpoint saved successfully to {path}")
        except Exception as e:
            logger.log_exception(e, f"Trainer.save_checkpoint ({path})")
            raise

    def load_checkpoint(self, path: str) -> bool:
        try:
            logger.log_info(f"Loading checkpoint from {path}")
            checkpoint = torch.load(path)
            self.network.load_state_dict(checkpoint['network_state_dict'])
            self.optimizer.load_state_dict(checkpoint['optimizer_state_dict'])
            self.scheduler.load_state_dict(checkpoint['scheduler_state_dict'])
            logger.log_info(f"Checkpoint loaded successfully from {path}")
            return True
        except Exception as e:
            logger.log_exception(e, f"Trainer.load_checkpoint ({path})")
            return False