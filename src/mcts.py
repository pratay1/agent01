import math
import numpy as np
import torch
from typing import Dict, Optional, Tuple
import chess
from src.move_encoder import move_to_index, index_to_move, get_legal_move_indices


class MCTSNode:
    __slots__ = ['visit_count', 'value_sum', 'prior', 'children', 'expanded']

    def __init__(self, prior: float = 0.0):
        self.visit_count = 0
        self.value_sum = 0.0
        self.prior = prior
        self.children: Dict[int, MCTSNode] = {}
        self.expanded = False

    def get_value(self, cpuct: float, parent_visit: int) -> float:
        if self.visit_count == 0:
            return float('inf')
        q = self.value_sum / self.visit_count
        u = cpuct * self.prior * (parent_visit ** 0.5) / (1 + self.visit_count)
        return q + u


class MCTS:
    def __init__(self, network, cpuct: float = 1.25, num_simulations: int = 100,
                 temperature: float = 1.0, dirichlet_alpha: float = 0.3,
                 dirichlet_epsilon: float = 0.25):
        self.network = network
        self.cpuct = cpuct
        self.num_simulations = num_simulations
        self.temperature = temperature
        self.dirichlet_alpha = dirichlet_alpha
        self.dirichlet_epsilon = dirichlet_epsilon

    def search(self, board: chess.Board, is_root: bool = False) -> Tuple[np.ndarray, int]:
        root = MCTSNode()

        if is_root:
            legal_moves = list(board.legal_moves)
            if legal_moves:
                noise = np.random.dirichlet([self.dirichlet_alpha] * len(legal_moves))
                for i, move in enumerate(legal_moves):
                    idx = move_to_index(move, board)
                    node = MCTSNode(prior=(1 - self.dirichlet_epsilon) * (1.0 / len(legal_moves)) +
                                    self.dirichlet_epsilon * noise[i])
                    root.children[idx] = node

        for _ in range(self.num_simulations):
            self._run_simulation(board, root)

        visit_counts = self._get_visit_counts(root)

        if self.temperature == 0:
            best_idx = max(root.children.keys(), key=lambda k: root.children[k].visit_count)
        else:
            visit_counts_powered = np.power(visit_counts, 1.0 / self.temperature)
            if visit_counts_powered.sum() > 0:
                visit_counts_powered /= visit_counts_powered.sum()
                best_idx = np.random.choice(len(visit_counts_powered), p=visit_counts_powered)
            else:
                best_idx = np.random.choice(list(root.children.keys()))

        return visit_counts, int(best_idx)

    def _run_simulation(self, board: chess.Board, node: MCTSNode):
        board = board.copy()
        path = [node]

        while node.expanded and not board.is_game_over():
            best_child = self._select_child(node)
            if best_child is None:
                break

            move = index_to_move(best_child[0], board)
            if move is None:
                break

            board.push(move)
            path.append(best_child[1])
            node = best_child[1]

        if not board.is_game_over():
            self.network.eval()
            with torch.no_grad():
                from src.board_encoder import board_to_tensor
                board_tensor = board_to_tensor(board)
                policy_logits, value = self.network(board_tensor)
                policy_logits = policy_logits.squeeze(0).numpy()

            legal_indices = get_legal_move_indices(board)
            policy = np.zeros(4672)
            for idx in legal_indices:
                policy[idx] = math.exp(policy_logits[idx])
            policy_sum = policy.sum()
            if policy_sum > 0:
                policy /= policy_sum

            node.expanded = True
            for idx in legal_indices:
                node.children[idx] = MCTSNode(prior=policy[idx])
        else:
            outcome = board.outcome()
            if outcome is not None:
                value = -1.0 if outcome.winner == board.turn else (1.0 if outcome.winner else 0.0)
            else:
                value = 0.0

        value_sign = 1.0
        for n in reversed(path):
            n.visit_count += 1
            n.value_sum += value_sign * value
            value_sign *= -1.0

    def _select_child(self, node: MCTSNode) -> Optional[Tuple[int, MCTSNode]]:
        if not node.children:
            return None
        best_value = float('-inf')
        best = None
        for idx, child in node.children.items():
            value = child.get_value(self.cpuct, node.visit_count)
            if value > best_value:
                best_value = value
                best = (idx, child)
        return best

    def _get_visit_counts(self, root: MCTSNode) -> np.ndarray:
        visit_counts = np.zeros(4672)
        for idx, child in root.children.items():
            visit_counts[idx] = child.visit_count
        return visit_counts

    def get_best_move(self, board: chess.Board) -> Optional[chess.Move]:
        _, best_idx = self.search(board, is_root=True)
        return index_to_move(best_idx, board)