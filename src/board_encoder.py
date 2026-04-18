import chess
import torch


def board_to_tensor(board: chess.Board) -> torch.Tensor:
    planes = torch.zeros(19, 8, 8, dtype=torch.float32)

    board_for_perspective = board if board.turn == chess.WHITE else board.mirror()

    piece_map = board_for_perspective.piece_map()
    for square, piece in piece_map.items():
        row = 7 - (square // 8)
        col = square % 8

        if piece.color == chess.WHITE:
            plane_idx = {
                chess.PAWN: 0,
                chess.KNIGHT: 1,
                chess.BISHOP: 2,
                chess.ROOK: 3,
                chess.QUEEN: 4,
                chess.KING: 5
            }[piece.piece_type]
        else:
            plane_idx = {
                chess.PAWN: 6,
                chess.KNIGHT: 7,
                chess.BISHOP: 8,
                chess.ROOK: 9,
                chess.QUEEN: 10,
                chess.KING: 11
            }[piece.piece_type]

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

    planes[18, :, :] = board.halfmove_clock / 100.0

    return planes.unsqueeze(0)