import chess
import torch
import logging
import os
import glob

logger = logging.getLogger("ChessAI.move_encoder")

# Queen directions: N, NE, E, SE, S, SW, W, NW (order must match encode/decode)
QUEEN_DIRECTIONS = [(-1, 0), (-1, 1), (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1)]
KNIGHT_MOVES = [(-2, -1), (-2, 1), (-1, -2), (-1, 2), (1, -2), (1, 2), (2, -1), (2, 1)]

# Move type ranges:
# 0-55: queen moves (8 directions * 7 distances = 56)
# 56-63: knight moves (8)
# 64-72: underpromotions (3 pieces * 3 file shifts = 9)
# Total: 73 move types per from_square

def move_to_index(move: chess.Move, board: chess.Board) -> int:
    """Encode a chess move into an integer index in [0, 4671]."""
    try:
        from_square = move.from_square
        to_square = move.to_square
        promotion = move.promotion

        from_row = 7 - (from_square // 8)
        from_col = from_square % 8
        to_row = 7 - (to_square // 8)
        to_col = to_square % 8

        delta_row = to_row - from_row
        delta_col = to_col - from_col
        adr = abs(delta_row)
        adc = abs(delta_col)

        # Underpromotion: pawn promotion to KNIGHT, BISHOP, or ROOK (not QUEEN)
        if promotion is not None and promotion != chess.QUEEN:
            # Underpromotions: 3 piece types × 3 file shifts (-1,0,+1)
            promo_types = {chess.KNIGHT: 0, chess.BISHOP: 1, chess.ROOK: 2}
            ptype = promo_types.get(promotion, 0)
            # File shift: -1 (left), 0 (straight), +1 (right)
            shift = 0
            if delta_col == -1:
                shift = 0
            elif delta_col == 0:
                shift = 1
            elif delta_col == 1:
                shift = 2
            else:
                # Invalid file shift; fallback to straight
                shift = 1
            move_type = 64 + ptype * 3 + shift
            return from_square * 73 + move_type

        # Knight moves
        if (adr, adc) in [(1, 2), (2, 1)]:
            # Find matching knight offset
            for i, (kdr, kdc) in enumerate(KNIGHT_MOVES):
                if kdr == delta_row and kdc == delta_col:
                    move_type = 56 + i
                    return from_square * 73 + move_type
            # Should not happen if move is legal; fall through to queen as safety
            logger.warning(f"Knight pattern not found for ({delta_row},{delta_col})")

        # Queen slides (including pawn promotes to queen, which is a 1-step queen move)
        # Determine direction
        if delta_row == 0 and delta_col != 0:
            dir_idx = 2 if delta_col > 0 else 6  # E or W
        elif delta_col == 0 and delta_row != 0:
            dir_idx = 0 if delta_row < 0 else 4  # N or S
        elif adr == adc and delta_row != 0:
            if delta_row < 0 and delta_col > 0:
                dir_idx = 1  # NE
            elif delta_row < 0 and delta_col < 0:
                dir_idx = 7  # NW
            elif delta_row > 0 and delta_col > 0:
                dir_idx = 3  # SE
            elif delta_row > 0 and delta_col < 0:
                dir_idx = 5  # SW
            else:
                logger.warning(f"Unexpected diagonal ({delta_row},{delta_col})")
                return from_square * 73 + 0
        else:
            logger.warning(f"Unrecognized move delta ({delta_row},{delta_col}), defaulting to queen direction 0")
            dir_idx = 0

        distance = max(adr, adc)
        if distance < 1 or distance > 7:
            logger.warning(f"Distance {distance} out of range, clamping to 1")
            distance = 1
        move_type = dir_idx * 7 + (distance - 1)  # 0-55
        return from_square * 73 + move_type

    except Exception as e:
        logger.error(f"Error converting move to index: {e}", exc_info=True)
        return 0


def index_to_move(index: int, board: chess.Board) -> chess.Move:
    """Decode a move index into a chess.Move, or None if invalid."""
    try:
        from_square = index // 73
        move_type = index % 73

        from_row = 7 - (from_square // 8)
        from_col = from_square % 8

        # Queen slide
        if move_type < 56:
            dir_idx = move_type // 7
            distance = (move_type % 7) + 1
            dr, dc = QUEEN_DIRECTIONS[dir_idx]
            to_row = from_row + dr * distance
            to_col = from_col + dc * distance
            promotion = None

        elif move_type < 64:
            # Knight move
            k_idx = move_type - 56
            dr, dc = KNIGHT_MOVES[k_idx]
            to_row = from_row + dr
            to_col = from_col + dc
            promotion = None

        else:
            # Underpromotion (64-72)
            promo = (move_type - 64) // 3  # 0=KNIGHT,1=BISHOP,2=ROOK
            shift = (move_type - 64) % 3   # 0:left(-1),1:straight(0),2:right(+1)
            # Determine pawn push direction based on from_row
            if from_row == 1:  # white pawn on 7th rank -> moves upward (decrease row)
                delta_row = -1
            elif from_row == 6:  # black pawn on 2nd rank -> moves downward
                delta_row = 1
            else:
                logger.warning(f"Underpromotion from invalid row {from_row}")
                return None
            delta_col = -1 if shift == 0 else (0 if shift == 1 else 1)
            to_row = from_row + delta_row
            to_col = from_col + delta_col
            promotion = [chess.KNIGHT, chess.BISHOP, chess.ROOK][promo]

        # Bounds check
        if not (0 <= to_row < 8 and 0 <= to_col < 8):
            logger.warning(f"Calculated to_square ({to_row},{to_col}) is outside board bounds")
            return None

        to_square = (7 - to_row) * 8 + to_col

        # If the piece is a pawn moving to last rank and promotion is not set, assume queen promotion for queen moves
        piece = board.piece_at(from_square)
        if piece and piece.piece_type == chess.PAWN:
            if (piece.color == chess.WHITE and to_row == 0) or (piece.color == chess.BLACK and to_row == 7):
                if promotion is None:
                    promotion = chess.QUEEN

        move = chess.Move(from_square, to_square, promotion=promotion)
        if move not in board.legal_moves:
            logger.debug(f"Move {move.uci()} not legal")
            return None
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
            if 0 <= idx < policy.numel():
                filtered_policy[idx] = policy[idx]
            else:
                logger.warning(f"Index {idx} is out of bounds for policy tensor of size {policy.numel()}")
        logger.debug("Policy filtering completed")
        return filtered_policy
    except Exception as e:
        logger.error(f"Error filtering policy: {e}", exc_info=True)
        return torch.full_like(policy, float('-inf'))
