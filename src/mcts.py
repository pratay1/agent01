import math
import numpy as np
import torch
import logging
from typing import Dict, Optional, Tuple
import chess
from src.move_encoder import move_to_index, index_to_move, get_legal_move_indices
from src.board_encoder import board_to_tensor

logger = logging.getLogger("ChessAI.mcts")


class MCTSNode:
    __slots__ = ['visit_count', 'value_sum', 'prior', 'children', 'expanded']

    def __init__(self, prior: float = 0.0):
        try:
            self.visit_count = 0
            self.value_sum = 0.0
            self.prior = prior
            self.children: Dict[int, MCTSNode] = {}
            self.expanded = False
            logger.debug(f"MCTSNode created with prior={prior}")
        except Exception as e:
            logger.error(f"Failed to initialize MCTSNode: {e}", exc_info=True)
            raise

    def get_value(self, cpuct: float, parent_visit: int) -> float:
        try:
            if self.visit_count == 0:
                return float('inf')
            q = self.value_sum / self.visit_count
            u = cpuct * self.prior * (parent_visit ** 0.5) / (1 + self.visit_count)
            return q + u
        except Exception as e:
            logger.error(f"Error calculating node value: {e}", exc_info=True)
            return 0.0  # Return neutral value on error


class MCTS:
    def __init__(self, network, cpuct: float = 1.25, num_simulations: int = 100,
                 temperature: float = 1.0, dirichlet_alpha: float = 0.3,
                 dirichlet_epsilon: float = 0.25):
        try:
            logger.debug(f"Initializing MCTS with cpuct={cpuct}, num_simulations={num_simulations}, "
                        f"temperature={temperature}, dirichlet_alpha={dirichlet_alpha}, "
                        f"dirichlet_epsilon={dirichlet_epsilon}")
            self.network = network
            self.cpuct = cpuct
            self.num_simulations = num_simulations
            self.temperature = temperature
            self.dirichlet_alpha = dirichlet_alpha
            self.dirichlet_epsilon = dirichlet_epsilon
            logger.info("MCTS initialized successfully")
        except Exception as e:
            logger.error(f"Failed to initialize MCTS: {e}", exc_info=True)
            raise

    def search(self, board: chess.Board, is_root: bool = False) -> Tuple[np.ndarray, int]:
        try:
            logger.debug(f"Starting MCTS search with is_root={is_root}")
            root = MCTSNode()

            if is_root:
                legal_moves = list(board.legal_moves)
                logger.debug(f"Found {len(legal_moves)} legal moves for root node")
                if legal_moves:
                    # Get network policy for root
                    self.network.eval()
                    with torch.no_grad():
                        board_tensor = board_to_tensor(board)
                        policy_logits, _ = self.network(board_tensor)
                        policy_logits = policy_logits.squeeze(0).numpy()
                    # Build policy over legal moves
                    legal_indices = [move_to_index(m, board) for m in legal_moves]
                    policy = np.zeros(4672)
                    for idx in legal_indices:
                        policy[idx] = math.exp(policy_logits[idx])
                    policy_sum = policy.sum()
                    if policy_sum > 0:
                        policy /= policy_sum
                    else:
                        for idx in legal_indices:
                            policy[idx] = 1.0 / len(legal_indices)
                    # Add Dirichlet noise
                    noise = np.random.dirichlet([self.dirichlet_alpha] * len(legal_moves))
                    for i, idx in enumerate(legal_indices):
                        prior = (1 - self.dirichlet_epsilon) * policy[idx] + self.dirichlet_epsilon * noise[i]
                        root.children[idx] = MCTSNode(prior=prior)
                    root.expanded = True
                else:
                    logger.warning("No legal moves available for root node")

            logger.debug(f"Running {self.num_simulations} simulations")
            for i in range(self.num_simulations):
                try:
                    self._run_simulation(board, root)
                except Exception as e:
                    logger.error(f"Error in simulation {i}: {e}", exc_info=True)

            visit_counts = self._get_visit_counts(root)
            logger.debug(f"Visit counts calculated, shape: {visit_counts.shape}")

            if self.temperature == 0:
                if root.children:
                    best_idx = max(root.children.keys(), key=lambda k: root.children[k].visit_count)
                    logger.debug(f"Selected best move with temperature=0: index {best_idx}")
                else:
                    logger.warning("No children in root node for temperature=0 selection")
                    best_idx = 0
            else:
                try:
                    visit_counts_powered = np.power(visit_counts, 1.0 / self.temperature)
                    if visit_counts_powered.sum() > 0:
                        visit_counts_powered /= visit_counts_powered.sum()
                        best_idx = np.random.choice(len(visit_counts_powered), p=visit_counts_powered)
                        logger.debug(f"Selected move with temperature={self.temperature}: index {best_idx}")
                    else:
                        logger.warning("Visit counts powered sum to zero, selecting random child")
                        if root.children:
                            best_idx = np.random.choice(list(root.children.keys()))
                        else:
                            best_idx = 0
                except Exception as e:
                    logger.error(f"Error in temperature-based selection: {e}", exc_info=True)
                    if root.children:
                        best_idx = max(root.children.keys(), key=lambda k: root.children[k].visit_count)
                    else:
                        best_idx = 0

            logger.debug(f"MCTS search completed, returning visit_counts and best_idx={best_idx}")
            return visit_counts, int(best_idx)
        except Exception as e:
            logger.error(f"Fatal error in MCTS search: {e}", exc_info=True)
            return np.zeros(4672), 0

    def _run_simulation(self, board: chess.Board, node: MCTSNode):
        try:
            logger.debug(f"Starting MCTS simulation")
            board = board.copy()
            path = [node]

            while node.expanded and not board.is_game_over():
                try:
                    best_child = self._select_child(node)
                    if best_child is None:
                        logger.debug("No valid child found during selection, treating node as leaf")
                        break

                    move = index_to_move(best_child[0], board)
                    if move is None:
                        logger.debug(f"index_to_move returned None for index {best_child[0]}, trying random move")
                        legal_moves = list(board.legal_moves)
                        if legal_moves:
                            move = np.random.choice(legal_moves)
                        else:
                            break
                    if move is None:
                        logger.warning("Failed to get move from index")
                        break

                    logger.debug(f"Playing move: {move.uci()}")
                    board.push(move)
                    path.append(best_child[1])
                    node = best_child[1]
                except Exception as e:
                    logger.error(f"Error during simulation traversal: {e}", exc_info=True)
                    break

            # Ensure the leaf node (current node) is in the path before expansion
            if node not in path:
                path.append(node)

            if not board.is_game_over():
                logger.debug("Expanding leaf node")
                try:
                    self.network.eval()
                    with torch.no_grad():
                        from src.board_encoder import board_to_tensor
                        board_tensor = board_to_tensor(board)
                        policy_logits, value = self.network(board_tensor)
                        policy_logits = policy_logits.squeeze(0).numpy()
                        logger.debug(f"Network inference completed: policy_logits shape={policy_logits.shape}, value={value}")

                    legal_indices = get_legal_move_indices(board)
                    logger.debug(f"Found {len(legal_indices)} legal move indices")
                    policy = np.zeros(4672)
                    for idx in legal_indices:
                        policy[idx] = math.exp(policy_logits[idx])
                    policy_sum = policy.sum()
                    if policy_sum > 0:
                        policy /= policy_sum
                    else:
                        logger.warning("Policy sum is zero, using uniform distribution")
                        # Uniform distribution over legal moves
                        for idx in legal_indices:
                            policy[idx] = 1.0 / len(legal_indices)

                    node.expanded = True
                    for idx in legal_indices:
                        node.children[idx] = MCTSNode(prior=policy[idx])
                    logger.debug(f"Expanded node with {len(legal_indices)} children")
                except Exception as e:
                    logger.error(f"Error during node expansion: {e}", exc_info=True)
                    # Mark as expanded anyway to prevent infinite retry
                    node.expanded = True
            else:
                logger.debug("Game over in simulation, calculating terminal value")
                try:
                    outcome = board.outcome()
                    if outcome is None:
                        value = 0.0
                    elif outcome.winner == board.turn:
                        value = 1.0
                    else:
                        value = -1.0
                    logger.debug(f"Terminal value from game outcome: {value}")
                except Exception as e:
                    logger.error(f"Error calculating terminal value: {e}", exc_info=True)
                    value = 0.0

            try:
                value_sign = 1.0
                for n in reversed(path):
                    n.visit_count += 1
                    n.value_sum += value_sign * value
                    value_sign *= -1.0
                logger.debug(f"Backpropagated value {value} through {len(path)} nodes")
            except Exception as e:
                logger.error(f"Error during backpropagation: {e}", exc_info=True)
        except Exception as e:
            logger.error(f"Fatal error in MCTS simulation: {e}", exc_info=True)

    def _select_child(self, node: MCTSNode) -> Optional[Tuple[int, MCTSNode]]:
        try:
            if not node.children:
                logger.debug("Node has no children")
                return None
            best_value = float('-inf')
            best = None
            for idx, child in node.children.items():
                try:
                    value = child.get_value(self.cpuct, node.visit_count)
                    if value > best_value:
                        best_value = value
                        best = (idx, child)
                except Exception as e:
                    logger.debug(f"Error getting value for child {idx}: {e}")
            if best is None and node.children:
                logger.debug("No valid child found after evaluation, selecting random")
                idx = np.random.choice(list(node.children.keys()))
                best = (idx, node.children[idx])
            return best
        except Exception as e:
            logger.error(f"Error in _select_child: {e}", exc_info=True)
            return None

    def _get_visit_counts(self, root: MCTSNode) -> np.ndarray:
        try:
            visit_counts = np.zeros(4672)
            for idx, child in root.children.items():
                visit_counts[idx] = child.visit_count
            logger.debug(f"Generated visit counts for {len(root.children)} children")
            return visit_counts
        except Exception as e:
            logger.error(f"Error getting visit counts: {e}", exc_info=True)
            return np.zeros(4672)

    def get_best_move(self, board: chess.Board) -> Optional[chess.Move]:
        try:
            logger.debug("Getting best move from MCTS")
            _, best_idx = self.search(board, is_root=True)
            move = index_to_move(best_idx, board)
            if move is None:
                logger.debug(f"index_to_move returned None for index {best_idx}, trying random move")
                legal_moves = list(board.legal_moves)
                if legal_moves:
                    move = np.random.choice(legal_moves)
                    logger.debug(f"Selected random move: {move.uci()}")
                else:
                    logger.warning("No legal moves available")
            else:
                logger.debug(f"Best move: {move.uci()}")
            return move
        except Exception as e:
            logger.error(f"Error getting best move: {e}", exc_info=True)
            return None