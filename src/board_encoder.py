import chess
import torch
import traceback
from src.logger import get_logger

logger = get_logger()


def board_to_tensor(board: chess.Board) -> torch.Tensor:
    try:
        logger.log_debug(f"Converting board to tensor. Board fen: {board.fen()}")
        planes = torch.zeros(19, 8, 8, dtype=torch.float32)
        logger.log_debug(f"Created planes tensor with shape: {planes.shape}")

        board_for_perspective = board if board.turn == chess.WHITE else board.mirror()
        logger.log_debug(f"Using perspective: {'WHITE' if board.turn == chess.WHITE else 'BLACK'}")

        piece_map = board_for_perspective.piece_map()
        logger.log_debug(f"Found {len(piece_map)} pieces on board")

        for square, piece in piece_map.items():
            try:
                row = 7 - (square // 8)
                col = square % 8
                if piece.color == chess.WHITE:
                    plane_idx = [chess.PAWN, chess.KNIGHT, chess.BISHOP, chess.ROOK, chess.QUEEN, chess.KING].index(piece.piece_type)
                else:
                    plane_idx = 6 + [chess.PAWN, chess.KNIGHT, chess.BISHOP, chess.ROOK, chess.QUEEN, chess.KING].index(piece.piece_type)
                planes[plane_idx, row, col] = 1.0
                logger.log_debug(f"Placed piece {piece.piece_type} at ({row},{col}) in plane {plane_idx}")
            except Exception as e:
                logger.log_exception(e, f"processing piece at square {square}")
                # Continue with other pieces

        planes[12, :, :] = 1.0 if board.turn == chess.WHITE else 0.0
        logger.log_debug(f"Set turn plane (12) to: {1.0 if board.turn == chess.WHITE else 0.0}")

        try:
            if board_for_perspective.has_kingside_castling_rights(chess.WHITE):
                planes[13, :, :] = 1.0
                logger.log_debug("Set white kingside castling rights")
            if board_for_perspective.has_queenside_castling_rights(chess.WHITE):
                planes[14, :, :] = 1.0
                logger.log_debug("Set white queenside castling rights")
            if board_for_perspective.has_kingside_castling_rights(chess.BLACK):
                planes[15, :, :] = 1.0
                logger.log_debug("Set black kingside castling rights")
            if board_for_perspective.has_queenside_castling_rights(chess.BLACK):
                planes[16, :, :] = 1.0
                logger.log_debug("Set black queenside castling rights")
        except Exception as e:
            logger.log_exception(e, "setting castling rights")

        try:
            if board.ep_square is not None:
                row = 7 - (board.ep_square // 8)
                col = board.ep_square % 8
                planes[17, row, col] = 1.0
                logger.log_debug(f"Set en passant square at ({row},{col})")
        except Exception as e:
            logger.log_exception(e, "setting en passant square")

        try:
            halfmove_clock = min(board.halfmove_clock / 100.0, 1.0)
            planes[18, :, :] = halfmove_clock
            logger.log_debug(f"Set halfmove clock plane (18) to: {halfmove_clock}")
        except Exception as e:
            logger.log_exception(e, "setting halfmove clock")
            # Set to zero as fallback
            planes[18, :, :] = 0.0

        result = planes.unsqueeze(0)
        logger.log_debug(f"Final tensor shape: {result.shape}")
        return result
    except Exception as e:
        logger.log_exception(e, "board_to_tensor")
        # Return a zero tensor as fallback to prevent crashing
        return torch.zeros(1, 19, 8, 8, dtype=torch.float32)