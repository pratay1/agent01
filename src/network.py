import torch
import torch.nn as nn
import torch.nn.functional as F
import traceback
from src.logger import get_logger

logger = get_logger()


class AlphaZeroNetwork(nn.Module):
    def __init__(self, num_res_blocks=3, num_channels=64):
        try:
            logger.log_info(f"Initializing AlphaZeroNetwork with {num_res_blocks} residual blocks and {num_channels} channels")
            super().__init__()
            self.num_res_blocks = num_res_blocks
            self.num_channels = num_channels

            self.input_conv = nn.Conv2d(19, num_channels, kernel_size=3, padding=1)
            logger.log_debug(f"Created input conv layer: Conv2d(19, {num_channels}, kernel_size=3, padding=1)")

            self.res_blocks = nn.ModuleList([
                ResidualBlock(num_channels) for _ in range(num_res_blocks)
            ])
            logger.log_debug(f"Created {num_res_blocks} residual blocks")

            self.policy_head = nn.Sequential(
                nn.Conv2d(num_channels, 2, kernel_size=1),
                nn.BatchNorm2d(2),
                nn.ReLU(),
                nn.Flatten(),
                nn.Linear(2 * 8 * 8, 4672)
            )
            logger.log_debug("Created policy head")

            self.value_head = nn.Sequential(
                nn.Conv2d(num_channels, 1, kernel_size=1),
                nn.BatchNorm2d(1),
                nn.ReLU(),
                nn.Flatten(),
                nn.Linear(8 * 8, 1),
                nn.Tanh()
            )
            logger.log_debug("Created value head")
            logger.log_info(f"AlphaZeroNetwork initialized successfully")
        except Exception as e:
            logger.log_exception(e, "AlphaZeroNetwork.__init__")
            raise

    def forward(self, x):
        try:
            logger.log_debug(f"Forward pass with input shape: {x.shape}")
            x = F.relu(self.input_conv(x))
            logger.log_debug(f"After input conv: {x.shape}")
            
            for i, res_block in enumerate(self.res_blocks):
                x = res_block(x)
                logger.log_debug(f"After res block {i}: {x.shape}")
                
            policy = self.policy_head(x)
            value = self.value_head(x)
            logger.log_debug(f"Policy shape: {policy.shape}, Value shape: {value.shape}")
            return policy, value.squeeze(-1)
        except Exception as e:
            logger.log_exception(e, "AlphaZeroNetwork.forward")
            # Return zero tensors of expected shape to prevent crashing
            batch_size = x.shape[0] if len(x.shape) > 0 else 1
            policy = torch.zeros(batch_size, 4672, device=x.device if hasattr(x, 'device') else 'cpu')
            value = torch.zeros(batch_size, device=x.device if hasattr(x, 'device') else 'cpu')
            return policy, value


class ResidualBlock(nn.Module):
    def __init__(self, channels):
        try:
            logger.log_debug(f"Initializing ResidualBlock with {channels} channels")
            super().__init__()
            self.conv1 = nn.Conv2d(channels, channels, kernel_size=3, padding=1)
            self.bn1 = nn.BatchNorm2d(channels)
            self.conv2 = nn.Conv2d(channels, channels, kernel_size=3, padding=1)
            self.bn2 = nn.BatchNorm2d(channels)
            logger.log_debug("ResidualBlock initialized successfully")
        except Exception as e:
            logger.log_exception(e, "ResidualBlock.__init__")
            raise

    def forward(self, x):
        try:
            logger.log_debug(f"ResidualBlock forward with input shape: {x.shape}")
            residual = x
            x = F.relu(self.bn1(self.conv1(x)))
            x = self.bn2(self.conv2(x))
            result = F.relu(x + residual)
            logger.log_debug(f"ResidualBlock output shape: {result.shape}")
            return result
        except Exception as e:
            logger.log_exception(e, "ResidualBlock.forward")
            # Return input as fallback to prevent crashing
            return x


def create_network(num_res_blocks=10, num_channels=256):
    try:
        logger.log_info(f"Creating network with {num_res_blocks} residual blocks and {num_channels} channels")
        network = AlphaZeroNetwork(num_res_blocks, num_channels)
        logger.log_info("Network created successfully")
        return network
    except Exception as e:
        logger.log_exception(e, "create_network")
        raise