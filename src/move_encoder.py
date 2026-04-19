import chess
import torch
import logging

logger = logging.getLogger("ChessAI.move_encoder")


def move_to_index(move: chess.Move, board: chess.Board) -> int:
    try:
        logger.debug(f"Converting move to index: {move.uci()}")
        from_square = move.from_square
        to_square = move.to_square
        promotion = move.promotion

        logger.debug(f"Move details: from_square={from_square}, to_square={to_square}, promotion={promotion}")

        from_row = 7 - (from_square // 8)
        from_col = from_square % 8
        to_row = 7 - (to_square // 8)
        to_col = to_square % 8

        delta_row = to_row - from_row
        delta_col = to_col - from_col
        logger.debug(f"Move delta: ({delta_row}, {delta_col})")

        if promotion is None:
            if delta_row == 0 and delta_col > 0:
                move_type = 0
            elif delta_row == 0 and delta_col < 0:
                move_type = 1
            elif delta_row < 0 and delta_col == 0:
                move_type = 2
            elif delta_row > 0 and delta_col == 0:
                move_type = 3
            elif delta_row < 0 and delta_col == delta_row:
                move_type = 4
            elif delta_row < 0 and delta_col == -delta_row:
                move_type = 5
            elif delta_row > 0 and delta_col == delta_row:
                move_type = 6
            elif delta_row > 0 and delta_col == -delta_row:
                move_type = 7
            else:
                directions = [
                    (-2, -1), (-2, 1), (-1, -2), (-1, 2),
                    (1, -2), (1, 2), (2, -1), (2, 1)
                ]
                try:
                    move_type = 8 + next((i for i, d in enumerate(directions) if d == (delta_row, delta_col)), 0)
                except StopIteration:
                    logger.warning(f"Could not find matching direction for ({delta_row}, {delta_col}), defaulting to 0")
                    move_type = 8
            logger.debug(f"Non-promotion move type: {move_type}")
        else:
            promotion_type = {
                chess.KNIGHT: 0,
                chess.BISHOP: 1,
                chess.ROOK: 2,
                chess.QUEEN: 3
            }.get(promotion, 0)
            logger.debug(f"Promotion type: {promotion_type}")

            if delta_col == 0:
                if delta_row < 0:
                    move_type = 56 + promotion_type
                else:
                    move_type = 60 + promotion_type
            elif delta_row == 0:
                if delta_col > 0:
                    move_type = 64 + promotion_type
                else:
                    move_type = 68 + promotion_type
            else:
                move_type = 56 + promotion_type
            logger.debug(f"Promotion move type: {move_type}")

        result = from_square * 73 + move_type
        logger.debug(f"Final move index: {result}")
        return result
    except Exception as e:
        logger.error(f"Error converting move to index: {e}", exc_info=True)
        # Return a safe default index
        return 0


def index_to_move(index: int, board: chess.Board) -> chess.Move:
    try:
        logger.debug(f"Converting index to move: {index}")
        from_square = index // 73
        move_type = index % 73

        logger.debug(f"Decoded: from_square={from_square}, move_type={move_type}")

        from_row = 7 - (from_square // 8)
        from_col = from_square % 8

        delta_row = 0
        delta_col = 0

        if move_type < 8:
            deltas = [
                (0, 1), (0, -1), (-1, 0), (1, 0),
                (-1, -1), (-1, 1), (1, -1), (1, 1)
            ]
            delta_row, delta_col = deltas[move_type]
            logger.debug(f"Standard move delta: ({delta_row}, {delta_col})")
        elif move_type < 16:
            knight_moves = [
                (-2, -1), (-2, 1), (-1, -2), (-1, 2),
                (1, -2), (1, 2), (2, -1), (2, 1)
            ]
            try:
                delta_row, delta_col = knight_moves[move_type - 8]
                logger.debug(f"Knight move delta: ({delta_row}, {delta_col})")
            except IndexError:
                logger.warning(f"Invalid knight move type: {move_type - 8}, defaulting to (0,0)")
                delta_row, delta_col = 0, 0
        elif move_type < 56:
            slide_type = (move_type - 16) // 10
            distance = ((move_type - 16) % 10) + 1
            directions = [(-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1)]
            if slide_type < len(directions):
                dir_row, dir_col = directions[slide_type]
                delta_row = dir_row * distance
                delta_col = dir_col * distance
                logger.debug(f"Sliding move delta: ({delta_row}, {delta_col}), distance={distance}")
            else:
                logger.warning(f"Invalid slide type: {slide_type}")
                return None
        elif move_type < 73:
            promotion_type = (move_type - 56) % 4
            base_type = (move_type - 56) // 4

            if base_type == 0:
                delta_row = -1
                delta_col = 0
            elif base_type == 1:
                delta_row = 1
                delta_col = 0
            elif base_type == 2:
                delta_row = 0
                delta_col = 1
            else:
                delta_row = 0
                delta_col = -1

            promotion = [chess.KNIGHT, chess.BISHOP, chess.ROOK, chess.QUEEN][promotion_type]
            logger.debug(f"Promotion move: base_type={base_type}, promotion={promotion}")
        else:
            logger.warning(f"Invalid move type: {move_type}")
            return None

        to_row = from_row + delta_row
        to_col = from_col + delta_col
        logger.debug(f"Calculated to_square: ({to_row}, {to_col})")

        if not (0 <= to_row < 8 and 0 <= to_col < 8):
            logger.warning(f"Calculated to_square ({to_row}, {to_col}) is outside board bounds")
            return None

        to_square = (7 - to_row) * 8 + to_col
        logger.debug(f"Final to_square: {to_square}")

        promotion = None
        if move_type >= 56 and move_type < 73:
            promotion = [chess.KNIGHT, chess.BISHOP, chess.ROOK, chess.QUEEN][(move_type - 56) % 4]

        move = chess.Move(from_square, to_square, promotion=promotion)
        if not move or move not in board.legal_moves:
            logger.debug(f"Move {move} not legal, trying to find valid move")
            return None
        logger.debug(f"Created move: {move.uci() if move else 'None'}")
        return move
    except Exception as e:
        logger.error(f"Error converting index to move: {e}", exc_info=True)
        return None


def get_legal_move_indices(board: chess.Board) -> set:
    try:
        logger.debug("Getting legal move indices")
        legal_moves = list(board.legal_moves)
        logger.debug(f"Found {len(legal_moves)} legal moves")
        
        indices = set()
        for move in legal_moves:
            try:
                idx = move_to_index(move, board)
                indices.add(idx)
            except Exception as e:
                logger.error(f"Error converting move {move.uci()} to index: {e}", exc_info=True)
                # Skip this move but continue with others
        
        logger.debug(f"Generated {len(indices)} legal move indices")
        return indices
    except Exception as e:
        logger.error(f"Error getting legal move indices: {e}", exc_info=True)
        return set()


def filter_policy(policy: torch.Tensor, board: chess.Board) -> torch.Tensor:
    try:
        logger.debug("Filtering policy tensor")
        logger.debug(f"Policy shape: {policy.shape}")
        
        filtered_policy = torch.full_like(policy, float('-inf'))
        legal_indices = get_legal_move_indices(board)
        logger.debug(f"Found {len(legal_indices)} legal indices to filter")
        
        for idx in legal_indices:
            if 0 <= idx < policy.numel():  # Bounds check
                filtered_policy[idx] = policy[idx]
            else:
                logger.warning(f"Index {idx} is out of bounds for policy tensor of size {policy.numel()}")
        
        logger.debug("Policy filtering completed")
        return filtered_policy
    except Exception as e:
        logger.error(f"Error filtering policy: {e}", exc_info=True)
        # Return a tensor filled with -inf as safe fallback
        return torch.full_like(policy, float('-inf'))