using System;
using System.Collections.Generic;
using System.Linq;

public static class MoveEncoder
{
    // Match Python order: N, NE, E, SE, S, SW, W, NW
    private static readonly (int dr, int dc)[] QueenDirections = new (int, int)[]
    {
        (-1, 0), (-1, 1), (0, 1), (1, 1),
        (1, 0), (1, -1), (0, -1), (-1, -1)
    };

    private static readonly (int dr, int dc)[] KnightMoves = new (int, int)[]
    {
        (-2, -1), (-2, 1), (-1, -2), (-1, 2),
        (1, -2), (1, 2), (2, -1), (2, 1)
    };

    private static int FileShiftToIndex(int dc)
    {
        if (dc == -1) return 0;
        if (dc == 0) return 1;
        if (dc == 1) return 2;
        return 1;
    }

    private static int IndexToFileShift(int idx) => idx switch { 0 => -1, 1 => 0, 2 => 1, _ => 0 };

    private static int PieceToPromoIndex(int pieceType) => pieceType switch { 2 => 0, 3 => 1, 4 => 2, _ => 0 };
    private static int PromoIndexToPiece(int idx) => idx switch { 0 => 2, 1 => 3, 2 => 4, _ => 2 };

    public static int MoveToIndex(Move move, BoardState board)
    {
        int from = move.From;
        int to = move.To;

        int fromRow = 7 - (from / 8);
        int fromCol = from % 8;
        int toRow = 7 - (to / 8);
        int toCol = to % 8;

        int deltaRow = toRow - fromRow;
        int deltaCol = toCol - fromCol;

        int adr = Math.Abs(deltaRow);
        int adc = Math.Abs(deltaCol);

        // Underpromotion (exclude queen promotion)
        if (move.Promotion.HasValue && move.Promotion.Value != BoardState.QUEEN)
        {
            int pIdx = PieceToPromoIndex(move.Promotion.Value);
            int shiftIdx = FileShiftToIndex(deltaCol);
            return from * 73 + (64 + pIdx * 3 + shiftIdx);
        }

        // Knight moves
        if ((adr == 2 && adc == 1) || (adr == 1 && adc == 2))
        {
            for (int i = 0; i < 8; i++)
            {
                var (kdr, kdc) = KnightMoves[i];
                if (kdr == deltaRow && kdc == deltaCol)
                    return from * 73 + (56 + i);
            }
            return from * 73;
        }

        // Queen slides
        int dirIdx = -1;
        if (deltaRow == 0 && deltaCol != 0)
            dirIdx = deltaCol > 0 ? 2 : 6;
        else if (deltaCol == 0 && deltaRow != 0)
            dirIdx = deltaRow < 0 ? 0 : 4;
        else if (adr == adc && deltaRow != 0)
        {
            if (deltaRow < 0 && deltaCol > 0) dirIdx = 1;
            else if (deltaRow < 0 && deltaCol < 0) dirIdx = 7;
            else if (deltaRow > 0 && deltaCol > 0) dirIdx = 3;
            else if (deltaRow > 0 && deltaCol < 0) dirIdx = 5;
        }

        if (dirIdx == -1) return from * 73;

        int distance = Math.Max(adr, adc);
        if (distance < 1 || distance > 7) distance = 1;
        int moveType = dirIdx * 7 + (distance - 1);
        return from * 73 + moveType;
    }

    public static Move? IndexToMove(int index, BoardState board)
    {
        int from = index / 73;
        int moveType = index % 73;

        int fromRow = 7 - (from / 8);
        int fromCol = from % 8;

        int toRow, toCol;
        sbyte? promotion = null;

        if (moveType < 56)
        {
            int dirIdx = moveType / 7;
            int distance = (moveType % 7) + 1;
            var (dr, dc) = QueenDirections[dirIdx];
            toRow = fromRow + dr * distance;
            toCol = fromCol + dc * distance;
        }
        else if (moveType < 64)
        {
            int kIdx = moveType - 56;
            var (dr, dc) = KnightMoves[kIdx];
            toRow = fromRow + dr;
            toCol = fromCol + dc;
        }
        else
        {
            int promo = (moveType - 64) / 3;
            int shift = (moveType - 64) % 3;
            promotion = (sbyte)PromoIndexToPiece(promo);

            if (fromRow == 6) toRow = fromRow + 1;
            else if (fromRow == 1) toRow = fromRow - 1;
            else return null;

            toCol = fromCol + IndexToFileShift(shift);
        }

        if (toRow < 0 || toRow >= 8 || toCol < 0 || toCol >= 8)
            return null;

        int to = (7 - toRow) * 8 + toCol;

        // Auto-queen promotion if pawn reaches last rank without explicit promotion
        int piece = board.GetPieceAtSquare(from);
        if (piece != -1 && piece % 6 == 0) // pawn
        {
            bool isWhite = piece < 6;
            int target = isWhite ? 7 : 0;
            if (toRow == target && !promotion.HasValue)
                promotion = BoardState.QUEEN;
        }

        return new Move(from, to, promotion);
    }

    public static int[] GetLegalIndices(BoardState board)
    {
        var moves = board.GenerateLegalMoves();
        return moves.Select(m => MoveToIndex(m, board)).Where(i => i >= 0 && i < 4672).Distinct().ToArray();
    }
}
