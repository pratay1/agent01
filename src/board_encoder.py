import chess
import torch


def board_to_tensor(board: chess.Board) -> torch.Tensor:
    planes = torch.zeros(19, 8, 8, dtype=torch.float32)

    board_for_perspective = board if board.turn == chess.WHITE else board.mirror()

    for square, piece in board_for_perspective.piece_map().items():
        row = 7 - (square // 8)
        col = square % 8
        if piece.color == chess.WHITE:
            plane_idx = [chess.PAWN, chess.KNIGHT, chess.BISHOP, chess.ROOK, chess.QUEEN, chess.KING].index(piece.piece_type)
        else:
            plane_idx = 6 + [chess.PAWN, chess.KNIGHT, chess.BISHOP, chess.ROOK, chess.QUEEN, chess.KING].index(piece.piece_type)
        planes[plane_idx, row, col] = 1.0

    planes[12, :, :] = 1.0 if board.turn == chess.WHITE else 0.0

    if board_for_perspective.has_kingside_castling_rights(chess.WHITE):
        planes[13, :, :] = 1.0
    if board_for_perspective.has_queenside_castling_rights(chess.WHITE):
        planes[14, :, :] = 1.0
    if board_for_perspective.has_kingside_castling_rights(chess.BLACK):
        planes[15, :, :] = 1.0
    if board_for_perspective.has_queenside_castling_rights(chess.BLACK):
        planes[16, :, :] = 1.0

    if board.ep_square is not None:
        row = 7 - (board.ep_square // 8)
        col = board.ep_square % 8
        planes[17, row, col] = 1.0

    planes[18, :, :] = min(board.halfmove_clock / 100.0, 1.0)

    return planes.unsqueeze(0)