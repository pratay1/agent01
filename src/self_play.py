import random
import chess
import torch
import numpy as np
import time
from typing import List, Tuple, Dict, Any, Callable, Optional
from src.mcts import MCTS
from src.board_encoder import board_to_tensor
from src.move_encoder import move_to_index


def generate_random_opening(board: chess.Board, min_moves: int = 6, max_moves: int = 10) -> chess.Board:
    board = chess.Board()
    num_moves = random.randint(min_moves, max_moves)
    
    for _ in range(num_moves):
        legal_moves = list(board.legal_moves)
        if not legal_moves:
            break
        move = random.choice(legal_moves)
        board.push(move)
    
    return board


def play_game(network, mcts_config: Dict[str, Any], training_mode: str = "midgame", 
              max_think_time: float = 1.0, move_callback: Optional[Callable] = None) -> Tuple[List[torch.Tensor], List[np.ndarray], List[int], str, int]:
    board = chess.Board()

    if training_mode == "midgame":
        board = generate_random_opening(board, 6, 10)
    elif training_mode == "opening":
        pass

    start_fen = board.fen()

    mcts = MCTS(
        network=network,
        cpuct=mcts_config.get("cpuct", 1.25),
        num_simulations=mcts_config.get("num_simulations", 800),
        temperature=mcts_config.get("temperature", 1.0),
        dirichlet_alpha=mcts_config.get("dirichlet_alpha", 0.3),
        dirichlet_epsilon=mcts_config.get("dirichlet_epsilon", 0.25)
    )

    states = []
    policy_targets = []
    value_signs = []
    move_history = []

    move_count = 0
    temperature = mcts_config.get("temperature", 1.0)

    start_time = time.time()

    while not board.is_game_over():
        elapsed = time.time() - start_time
        remaining = max_think_time - elapsed
        if remaining <= 0:
            mcts.num_simulations = max(10, mcts.num_simulations // 2)

        if training_mode == "opening":
            if move_count < 8:
                temperature = 1.5
            elif move_count < 30:
                temperature = 1.0
            else:
                temperature = 0.0
            mcts.temperature = temperature
        else:
            if move_count < 30:
                mcts.temperature = 1.0
            else:
                mcts.temperature = 0.0

        board_tensor = board_to_tensor(board)
        visit_counts, best_idx = mcts.search(board, is_root=True)

        policy_target = visit_counts / visit_counts.sum() if visit_counts.sum() > 0 else visit_counts

        states.append(board_tensor)
        policy_targets.append(policy_target)
        value_signs.append(1 if move_count % 2 == 0 else -1)

        move = mcts.get_best_move(board)
        if move is None:
            break

        move_uci = move.uci()
        move_history.append(move_uci)
        if len(move_history) > 10:
            move_history = move_history[-10:]

        if move_callback:
            move_callback({
                "move": move_uci,
                "move_num": move_count + 1,
                "board": board.fen(),
                "last_moves": move_history[-5:] if len(move_history) >= 5 else move_history,
                "simulations": mcts.num_simulations,
                "elapsed": time.time() - start_time
            })

        board.push(move)
        move_count += 1

    outcome = board.outcome()
    if outcome is not None:
        if outcome.winner == chess.WHITE:
            game_result = 1
        elif outcome.winner == chess.BLACK:
            game_result = -1
        else:
            game_result = 0
    else:
        game_result = 0

    value_targets = []
    for sign in value_signs:
        value_targets.append(sign * game_result)

    return states, policy_targets, value_targets, start_fen, game_result