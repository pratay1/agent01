import random
import chess
import torch
import numpy as np
import time
import traceback
from typing import List, Tuple, Dict, Any, Callable, Optional
from src.mcts import MCTS
from src.board_encoder import board_to_tensor
from src.logger import get_logger

logger = get_logger()


    def generate_random_opening(board: chess.Board, min_moves: int = 6, max_moves: int = 10) -> chess.Board:
        try:
            logger.log_debug(f"Generating random opening with {min_moves}-{max_moves} moves")
            board = chess.Board()
            num_moves = random.randint(min_moves, max_moves)
            logger.log_debug(f"Selected {num_moves} moves for opening")
            
            for i in range(num_moves):
                legal_moves = list(board.legal_moves)
                if not legal_moves:
                    logger.log_warning(f"No legal moves available at move {i}, breaking opening generation")
                    break
                move = random.choice(legal_moves)
                logger.log_debug(f"Opening move {i+1}: {move.uci()}")
                board.push(move)
            logger.log_debug(f"Generated opening with {len(list(board.move_stack))} moves")
            return board
        except Exception as e:
            logger.log_exception(e, "generate_random_opening")
            # Return initial board state as fallback
            return chess.Board()


    def play_game(network, mcts_config: Dict[str, Any], training_mode: str = "midgame", 
               max_think_time: float = 0.5, move_callback: Optional[Callable] = None) -> Tuple[List[torch.Tensor], List[np.ndarray], List[int], str, int]:
        try:
            logger.log_debug(f"Starting play_game with training_mode={training_mode}, max_think_time={max_think_time}")
            board = chess.Board()

            if training_mode == "midgame":
                logger.log_debug("Using midgame mode, generating random opening")
                board = generate_random_opening(board, 6, 10)
            elif training_mode == "opening":
                logger.log_debug("Using opening mode")
                pass
            else:
                logger.log_warning(f"Unknown training_mode: {training_mode}, defaulting to midgame")
                board = generate_random_opening(board, 6, 10)

            start_fen = board.fen()
            logger.log_debug(f"Starting FEN: {start_fen}")

            logger.log_debug("Initializing MCTS")
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

            move_count = 0
            start_time = time.time()
            logger.log_info("Beginning game play loop")

            while not board.is_game_over():
                try:
                    elapsed = time.time() - start_time
                    if elapsed >= max_think_time:
                        old_simulations = mcts.num_simulations
                        mcts.num_simulations = max(5, mcts.num_simulations // 2)
                        logger.log_debug(f"Max think time reached, reducing simulations from {old_simulations} to {mcts.num_simulations}")

                    if training_mode == "opening":
                        if move_count < 8:
                            mcts.temperature = 1.5
                            logger.log_debug(f"Move {move_count}: Setting temperature to 1.5 (opening)")
                        elif move_count < 30:
                            mcts.temperature = 1.0
                            logger.log_debug(f"Move {move_count}: Setting temperature to 1.0 (opening)")
                        else:
                            mcts.temperature = 0.0
                            logger.log_debug(f"Move {move_count}: Setting temperature to 0.0 (opening)")
                    else:
                        mcts.temperature = 1.0 if move_count < 30 else 0.0
                        logger.log_debug(f"Move {move_count}: Setting temperature to {mcts.temperature} (midgame)")

                    board_tensor = board_to_tensor(board)
                    logger.log_debug(f"Board tensor shape: {board_tensor.shape}")

                    visit_counts, best_idx = mcts.search(board, is_root=True)
                    logger.log_debug(f"MCTS search completed, visit_counts shape: {visit_counts.shape}")

                    policy_target = visit_counts / visit_counts.sum() if visit_counts.sum() > 0 else visit_counts
                    logger.log_debug(f"Policy target calculated, sum: {policy_target.sum()}")

                    states.append(board_tensor)
                    policy_targets.append(policy_target)
                    value_signs.append(1 if move_count % 2 == 0 else -1)

                    move = mcts.get_best_move(board)
                    if move is None:
                        logger.log_warning("MCTS returned None move, breaking game loop")
                        break

                    move_uci = move.uci()
                    logger.log_debug(f"Selected move: {move_uci}")

                    if move_callback:
                        try:
                            move_callback({
                                "move": move_uci,
                                "move_num": move_count + 1,
                                "simulations": mcts.num_simulations,
                                "elapsed": elapsed
                            })
                            logger.log_debug(f"Move callback executed for move {move_count + 1}")
                        except Exception as e:
                            logger.log_exception(e, "move callback")
                            # Continue without callback

                    board.push(move)
                    move_count += 1
                    logger.log_debug(f"Move {move_count} played: {move_uci}")

                    # Safety check to prevent infinite games
                    if move_count > 500:  # Maximum reasonable game length
                        logger.log_warning("Game exceeded 500 moves, forcing termination")
                        break

                except Exception as e:
                    logger.log_exception(e, f"game move {move_count}")
                    # If we have some states, return what we have; otherwise raise
                    if states:
                        logger.log_warning("Returning partial game data due to error")
                        break
                    else:
                        raise

            logger.log_debug(f"Game over after {move_count} moves")
            outcome = board.outcome()
            if outcome is not None:
                game_result = 1 if outcome.winner == chess.WHITE else (-1 if outcome.winner == chess.BLACK else 0)
                logger.log_debug(f"Game outcome: {outcome}, result: {game_result}")
            else:
                game_result = 0
                logger.log_debug("Game outcome is None (draw or incomplete), result: 0")

            value_targets = [sign * game_result for sign in value_signs]
            logger.log_debug(f"Generated {len(value_targets)} value targets")

            result = (states, policy_targets, value_targets, start_fen, game_result)
            logger.log_info(f"Game completed: {len(states)} moves, result: {game_result}")
            return result
            
        except Exception as e:
            logger.log_exception(e, "play_game")
            # Return empty results to prevent crashing the training loop
            return [], [], [], "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 0