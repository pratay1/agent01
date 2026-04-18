import chess
import torch


def move_to_index(move: chess.Move, board: chess.Board) -> int:
    from_square = move.from_square
    to_square = move.to_square
    promotion = move.promotion

    from_row = 7 - (from_square // 8)
    from_col = from_square % 8
    to_row = 7 - (to_square // 8)
    to_col = to_square % 8

    delta_row = to_row - from_row
    delta_col = to_col - from_col

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
            move_type = 8 + next((i for i, d in enumerate(directions) if d == (delta_row, delta_col)), 0)
    else:
        promotion_type = {
            chess.KNIGHT: 0,
            chess.BISHOP: 1,
            chess.ROOK: 2,
            chess.QUEEN: 3
        }.get(promotion, 0)

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

    return from_square * 73 + move_type


def index_to_move(index: int, board: chess.Board) -> chess.Move:
    from_square = index // 73
    move_type = index % 73

    from_row = 7 - (from_square // 8)
    from_col = from_square % 8

    if move_type < 8:
        deltas = [
            (0, 1), (0, -1), (-1, 0), (1, 0),
            (-1, -1), (-1, 1), (1, -1), (1, 1)
        ]
        delta_row, delta_col = deltas[move_type]
    elif move_type < 16:
        knight_moves = [
            (-2, -1), (-2, 1), (-1, -2), (-1, 2),
            (1, -2), (1, 2), (2, -1), (2, 1)
        ]
        delta_row, delta_col = knight_moves[move_type - 8]
    else:
        promotion_type = (move_type - 56) % 4
        base_type = (move_type - 56) // 4

        if base_type == 0:
            delta_row = -1
            delta_col = 0
            promotion = chess.QUEEN if move_type < 60 else None
        elif base_type == 1:
            delta_row = 1
            delta_col = 0
            promotion = chess.QUEEN if move_type < 64 else None
        elif base_type == 2:
            delta_row = 0
            delta_col = 1
            promotion = chess.QUEEN if move_type < 68 else None
        else:
            delta_row = 0
            delta_col = -1
            promotion = chess.QUEEN

        if promotion is not None and promotion_type < 4:
            promotion = [chess.KNIGHT, chess.BISHOP, chess.ROOK, chess.QUEEN][promotion_type]

    to_row = from_row + delta_row
    to_col = from_col + delta_col

    if not (0 <= to_row < 8 and 0 <= to_col < 8):
        return None

    to_square = (7 - to_row) * 8 + to_col

    promotion = None
    if move_type >= 56:
        promotion = chess.QUEEN

    return chess.Move(from_square, to_square, promotion=promotion)


def get_legal_move_indices(board: chess.Board) -> set:
    legal_moves = board.legal_moves
    return {move_to_index(move, board) for move in legal_moves}


def filter_policy(policy: torch.Tensor, board: chess.Board) -> torch.Tensor:
    filtered_policy = torch.full_like(policy, float('-inf'))
    legal_indices = get_legal_move_indices(board)
    for idx in legal_indices:
        filtered_policy[idx] = policy[idx]
    return filtered_policy