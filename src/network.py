import torch
import torch.nn as nn
import torch.nn.functional as F


class AlphaZeroNetwork(nn.Module):
    def __init__(self, num_res_blocks=5, num_channels=128):
        super().__init__()
        self.num_res_blocks = num_res_blocks
        self.num_channels = num_channels

        self.input_conv = nn.Conv2d(19, num_channels, kernel_size=3, padding=1)
        self.input_bn = nn.BatchNorm2d(num_channels)

        self.res_blocks = nn.ModuleList([
            ResidualBlock(num_channels) for _ in range(num_res_blocks)
        ])

        self.policy_head = nn.Sequential(
            nn.Conv2d(num_channels, 2, kernel_size=1),
            nn.BatchNorm2d(2),
            nn.ReLU(),
            nn.Flatten(),
            nn.Linear(2 * 8 * 8, 4672)
        )

        self.value_head = nn.Sequential(
            nn.Conv2d(num_channels, 1, kernel_size=1),
            nn.BatchNorm2d(1),
            nn.ReLU(),
            nn.Flatten(),
            nn.Linear(8 * 8, 32),
            nn.ReLU(),
            nn.Linear(32, 1),
            nn.Tanh()
        )

    def forward(self, x):
        x = F.relu(self.input_bn(self.input_conv(x)))

        for res_block in self.res_blocks:
            x = res_block(x)

        policy = self.policy_head(x)
        value = self.value_head(x)

        return policy, value.squeeze(-1)


class ResidualBlock(nn.Module):
    def __init__(self, channels):
        super().__init__()
        self.conv1 = nn.Conv2d(channels, channels, kernel_size=3, padding=1)
        self.bn1 = nn.BatchNorm2d(channels)
        self.conv2 = nn.Conv2d(channels, channels, kernel_size=3, padding=1)
        self.bn2 = nn.BatchNorm2d(channels)

    def forward(self, x):
        residual = x
        x = F.relu(self.bn1(self.conv1(x)))
        x = self.bn2(self.conv2(x))
        x = F.relu(x + residual)
        return x


def create_network(num_res_blocks=5, num_channels=128):
    return AlphaZeroNetwork(num_res_blocks, num_channels)