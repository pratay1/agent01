import chess
import numpy as np
import torch
from typing import List, Optional, Tuple
from src.move_encoder import move_to_index, index_to_move, get_legal_move_indices


def get_all_valid_moves(board: chess.Board) -> List[chess.Move]:
    return list(board.legal_moves)


def get_valid_move_indices(board: chess.Board) -> set:
    return get_legal_move_indices(board)


def filter_policy_for_legal_moves(policy: np.ndarray, board: chess.Board) -> np.ndarray:
    filtered = np.full_like(policy, -1e10)
    legal_indices = get_legal_move_indices(board)
    for idx in legal_indices:
        if 0 <= idx < len(policy):
            filtered[idx] = policy[idx]
    return filtered


def get_valid_move_uci(board: chess.Board) -> List[str]:
    return [move.uci() for move in board.legal_moves]


def select_best_valid_move(policy_logits: np.ndarray, board: chess.Board, 
                          temperature: float = 0.0) -> Optional[chess.Move]:
    legal_indices = get_legal_move_indices(board)
    if not legal_indices:
        return None
    
    legal_logits = np.array([policy_logits[i] if i in legal_indices else -1e10 
                            for i in range(len(policy_logits))])
    
    if temperature == 0:
        best_idx = np.argmax(legal_logits)
    else:
        exp_logits = np.exp((legal_logits - np.max(legal_logits)) / temperature)
        probs = exp_logits / exp_logits.sum()
        best_idx = np.random.choice(len(probs), p=probs)
    
    move = index_to_move(best_idx, board)
    if move is None:
        move = np.random.choice(list(board.legal_moves))
    
    return move


def validate_and_fix_move(move: Optional[chess.Move], board: chess.Board) -> Optional[chess.Move]:
    if move is None:
        legal_moves = list(board.legal_moves)
        return np.random.choice(legal_moves) if legal_moves else None
    
    if move in board.legal_moves:
        return move
    
    legal_moves = list(board.legal_moves)
    if not legal_moves:
        return None
    
    from_sq = move.from_square
    to_sq = move.to_square
    promotion = move.promotion
    
    for legal in legal_moves:
        if legal.from_square == from_sq and legal.to_square == to_sq:
            if promotion is None or legal.promotion == promotion:
                return legal
    
    return np.random.choice(legal_moves) if legal_moves else None


def get_model_move_simple(network, board: chess.Board, temperature: float = 0.0) -> Optional[chess.Move]:
    from src.board_encoder import board_to_tensor
    
    legal_moves = list(board.legal_moves)
    if not legal_moves:
        return None
    
    network.eval()
    with torch.no_grad():
        board_tensor = board_to_tensor(board)
        policy_logits, value = network(board_tensor)
        policy_logits = policy_logits.squeeze(0).numpy()
    
    filtered_policy = filter_policy_for_legal_moves(policy_logits, board)
    
    if temperature == 0:
        best_idx = np.argmax(filtered_policy)
    else:
        exp_policy = np.exp((filtered_policy - np.max(filtered_policy)) / temperature)
        probs = exp_policy / exp_policy.sum()
        best_idx = np.random.choice(len(probs), p=probs)
    
    move = index_to_move(best_idx, board)
    move = validate_and_fix_move(move, board)
    
    return move


def get_model_move_with_valid_moves(network, board: chess.Board, temperature: float = 0.0) -> Tuple[Optional[chess.Move], np.ndarray]:
    from src.board_encoder import board_to_tensor
    
    legal_moves = list(board.legal_moves)
    if not legal_moves:
        return None, np.zeros(4672)
    
    network.eval()
    with torch.no_grad():
        board_tensor = board_to_tensor(board)
        policy_logits, value = network(board_tensor)
        policy_logits = policy_logits.squeeze(0).numpy()
    
    filtered_policy = filter_policy_for_legal_moves(policy_logits, board)
    
    if temperature == 0:
        best_idx = np.argmax(filtered_policy)
    else:
        exp_policy = np.exp((filtered_policy - np.max(filtered_policy)) / temperature)
        probs = exp_policy / exp_policy.sum()
        best_idx = np.random.choice(len(probs), p=probs)
    
    move = index_to_move(best_idx, board)
    move = validate_and_fix_move(move, board)
    
    return move, policy_logits


def print_valid_moves(board: chess.Board) -> None:
    moves = get_valid_move_uci(board)
    print(f"Valid moves ({len(moves)}): {moves}")