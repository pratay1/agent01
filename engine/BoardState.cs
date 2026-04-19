using System;
using System.Collections.Generic;

public class BoardState
{
    private ulong[] pieces = new ulong[12];
    private bool turn = true; // true = white
    private ulong castlingRights = 0xF;
    private int? epSquare = null;
    private int halfmoveClock = 0;
    private int fullmoveNumber = 1;
    private List<Move> moveHistory = new List<Move>();

    public bool Turn => turn;
    public int? EpSquare => epSquare;

    // piece indexes:
    // 0-5 white P N B R Q K
    // 6-11 black P N B R Q K

    private static readonly int[] KnightOffsets = { 17, 15, 10, 6, -6, -10, -15, -17 };
    private static readonly int[] BishopOffsets = { 9, 7, -7, -9 };
    private static readonly int[] RookOffsets = { 8, 1, -1, -8 };
    private static readonly int[] KingOffsets = { 9, 8, 7, 1, -1, -8, -7, -9 };

    public BoardState() { InitStartPosition(); }

    private void InitStartPosition()
    {
        pieces[0] = 0xFF;
        pieces[1] = 0x42;
        pieces[2] = 0x24;
        pieces[3] = 0x81;
        pieces[4] = 0x08;
        pieces[5] = 0x10;

        pieces[6] = 0xFF00;
        pieces[7] = 0x4200;
        pieces[8] = 0x2400;
        pieces[9] = 0x810000;
        pieces[10] = 0x080000;
        pieces[11] = 0x100000;
    }

    // =========================
    // BIT HELPERS
    // =========================

    private static int PopLSB(ref ulong bb)
    {
        ulong lsb = bb & (ulong)-(long)bb;
        int idx = BitScan(lsb);
        bb &= bb - 1;
        return idx;
    }

    private static int BitScan(ulong bb)
    {
        int i = 0;
        while ((bb >>= 1) != 0) i++;
        return i;
    }

    private bool IsOccupied(int sq) => (GetAllPieces() & (1UL << sq)) != 0;

    private bool IsFriend(int sq) =>
        turn ? (GetWhitePieces() & (1UL << sq)) != 0
             : (GetBlackPieces() & (1UL << sq)) != 0;

    private ulong GetWhitePieces() =>
        pieces[0] | pieces[1] | pieces[2] | pieces[3] | pieces[4] | pieces[5];

    private ulong GetBlackPieces() =>
        pieces[6] | pieces[7] | pieces[8] | pieces[9] | pieces[10] | pieces[11];

    private ulong GetAllPieces() => GetWhitePieces() | GetBlackPieces();

    // =========================
    // MOVE GEN
    // =========================

    public List<Move> GenerateLegalMoves()
    {
        var moves = new List<Move>();

        GeneratePawns(moves);
        GenerateKnights(moves);
        GenerateSliders(moves, 2, BishopOffsets);
        GenerateSliders(moves, 3, RookOffsets);
        GenerateSliders(moves, 4, Combine(BishopOffsets, RookOffsets));
        GenerateKings(moves);

        // legality filter (king safety)
        var legal = new List<Move>();
        foreach (var m in moves)
        {
            var c = Copy();
            c.MakeMove(m);
            if (!c.IsInCheck(turn))
                legal.Add(m);
        }

        return legal;
    }

    private void GeneratePawns(List<Move> moves)
    {
        int dir = turn ? 8 : -8;
        int startRank = turn ? 1 : 6;
        int pawn = turn ? 0 : 6;

        ulong bb = pieces[pawn];

        while (bb != 0)
        {
            int from = PopLSB(ref bb);
            int r = from / 8;
            int c = from % 8;

            int fwd = from + dir;

            if (fwd >= 0 && fwd < 64 && !IsOccupied(fwd))
            {
                AddPawnMove(moves, from, fwd);

                if (r == startRank && !IsOccupied(fwd + dir))
                    moves.Add(new Move(from, fwd + dir));
            }

            int[] caps = turn ? new[] { 7, 9 } : new[] { -7, -9 };

            foreach (var off in caps)
            {
                int to = from + off;
                if (to < 0 || to >= 64) continue;

                if (Math.Abs((to % 8) - c) != 1) continue;

                if (!IsFriend(to) || (epSquare.HasValue && epSquare.Value == to))
                    AddPawnMove(moves, from, to);
            }
        }
    }

    private void AddPawnMove(List<Move> moves, int from, int to)
    {
        int rank = to / 8;
        if (rank == 0 || rank == 7)
        {
            moves.Add(new Move(from, to, 1));
            moves.Add(new Move(from, to, 2));
            moves.Add(new Move(from, to, 3));
            moves.Add(new Move(from, to, 4));
        }
        else moves.Add(new Move(from, to));
    }

    private void GenerateKnights(List<Move> moves)
    {
        int knight = turn ? 1 : 7;
        ulong bb = pieces[knight];

        while (bb != 0)
        {
            int from = PopLSB(ref bb);
            int r1 = from / 8;
            int c1 = from % 8;

            foreach (var off in KnightOffsets)
            {
                int to = from + off;
                if (to < 0 || to >= 64) continue;

                int r2 = to / 8;
                int c2 = to % 8;

                if (Math.Abs(r1 - r2) == 2 && Math.Abs(c1 - c2) == 1 ||
                    Math.Abs(r1 - r2) == 1 && Math.Abs(c1 - c2) == 2)
                {
                    if (!IsFriend(to))
                        moves.Add(new Move(from, to));
                }
            }
        }
    }

    private void GenerateSliders(List<Move> moves, int piece, int[] dirs)
    {
        ulong bb = pieces[turn ? piece : piece + 6];

        while (bb != 0)
        {
            int from = PopLSB(ref bb);

            foreach (var d in dirs)
            {
                int to = from;

                while (true)
                {
                    int prev = to;
                    to += d;

                    if (to < 0 || to >= 64) break;

                    if (Math.Abs((to / 8) - (prev / 8)) > 1) break;

                    if (IsFriend(to)) break;

                    moves.Add(new Move(from, to));

                    if (IsOccupied(to)) break;
                }
            }
        }
    }

    private void GenerateKings(List<Move> moves)
    {
        int king = turn ? 5 : 11;
        ulong bb = pieces[king];

        while (bb != 0)
        {
            int from = PopLSB(ref bb);

            foreach (var off in KingOffsets)
            {
                int to = from + off;
                if (to < 0 || to >= 64) continue;

                if (Math.Abs((to / 8) - (from / 8)) <= 1 &&
                    Math.Abs((to % 8) - (from % 8)) <= 1)
                {
                    if (!IsFriend(to))
                        moves.Add(new Move(from, to));
                }
            }
        }
    }

    // =========================
    // ATTACK / CHECK
    // =========================

    public bool IsInCheck(bool white)
    {
        int king = FindKing(white ? 5 : 11);
        return IsSquareAttacked(king, !white);
    }

    private int FindKing(int idx)
    {
        ulong bb = pieces[idx];
        return BitScan(bb);
    }

    private bool IsSquareAttacked(int sq, bool byWhite)
    {
        int pawn = byWhite ? 0 : 6;
        int dir = byWhite ? -8 : 8;

        foreach (var off in new[] { dir - 1, dir + 1 })
        {
            int to = sq + off;
            if (to >= 0 && to < 64 && (pieces[pawn] & (1UL << to)) != 0)
                return true;
        }

        int knight = byWhite ? 1 : 7;
        ulong bb = pieces[knight];

        while (bb != 0)
        {
            int from = PopLSB(ref bb);
            foreach (var off in KnightOffsets)
                if (from + off == sq) return true;
        }

        return false;
    }

    // =========================
    // MOVE MAKING
    // =========================

    public void MakeMove(Move m)
    {
        int piece = GetPieceAt(m.From);

        pieces[piece] &= ~(1UL << m.From);
        pieces[piece] |= (1UL << m.To);

        // capture
        int cap = GetPieceAt(m.To);
        if (cap != -1 && cap / 6 != piece / 6)
            pieces[cap] &= ~(1UL << m.To);

        // promotion
        if (m.Promotion.HasValue)
        {
            pieces[piece] &= ~(1UL << m.To);
            int promo = turn ? m.Promotion.Value : m.Promotion.Value + 6;
            pieces[promo] |= 1UL << m.To;
        }

        turn = !turn;
        moveHistory.Add(m);
    }

    private int GetPieceAt(int sq)
    {
        for (int i = 0; i < 12; i++)
            if ((pieces[i] & (1UL << sq)) != 0)
                return i;
        return -1;
    }

    public BoardState Copy()
    {
        var b = new BoardState();
        Array.Copy(pieces, b.pieces, 12);
        b.turn = turn;
        b.castlingRights = castlingRights;
        b.epSquare = epSquare;
        b.halfmoveClock = halfmoveClock;
        b.fullmoveNumber = fullmoveNumber;
        return b;
    }

    private static int[] Combine(int[] a, int[] b)
    {
        var r = new int[a.Length + b.Length];
        Array.Copy(a, r, a.Length);
        Array.Copy(b, 0, r, a.Length, b.Length);
        return r;
    }
}