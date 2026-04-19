using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public struct Move
{
    public int From;
    public int To;
    public sbyte? Promotion; // null = no promotion; else: 2=KNIGHT, 3=BISHOP, 4=ROOK, 5=QUEEN

    public Move(int from, int to, sbyte? promotion = null)
    {
        From = from;
        To = to;
        Promotion = promotion;
    }
}

public class BoardState
{
    // Piece bitboards: indexes 0-5 white P,N,B,R,Q,K; 6-11 black P,N,B,R,Q,K
    private ulong[] pieces = new ulong[12];
    private bool turn = true; // true = white to move
    private byte castlingRights = 0b1111; // bit 0=white kingside, 1=white queenside, 2=black kingside, 3=black queenside
    private int? epSquare = null; // square index (0-63) or null
    private int halfmoveClock = 0;
    private int fullmoveNumber = 1;
    private List<Move> moveHistory = new List<Move>();

    // Piece type constants matching python-chess
    public const sbyte KNIGHT = 2;
    public const sbyte BISHOP = 3;
    public const sbyte ROOK = 4;
    public const sbyte QUEEN = 5;
    public const sbyte KING = 6;

    public bool Turn => turn;
    public int? EpSquare => epSquare;

    // Precomputed bit masks
    private static readonly ulong[] RankMasks = Enumerable.Range(0, 8).Select(i => 0xFFUL << (i * 8)).ToArray();
    private static readonly ulong[] FileMasks = Enumerable.Range(0, 8).Select(i => 0x0101010101010101UL << i).ToArray();

    public BoardState() { InitStartPosition(); }

    public BoardState(string fen)
    {
        ParseFen(fen);
    }

    private void InitStartPosition()
    {
        // Standard chess starting position bitboards (0 = a1, 63 = h8)
        // White pieces
        pieces[0] = 0x000000000000FF00UL; // pawns (rank 2)
        pieces[1] = 0x0000000000000042UL; // knights (b1, g1)
        pieces[2] = 0x0000000000000024UL; // bishops (c1, f1)
        pieces[3] = 0x0000000000000081UL; // rooks (a1, h1)
        pieces[4] = 0x0000000000000008UL; // queen (d1)
        pieces[5] = 0x0000000000000010UL; // king (e1)
        // Black pieces
        pieces[6] = 0x00FF000000000000UL; // pawns (rank 7)
        pieces[7] = 0x4200000000000000UL; // knights (b8, g8)
        pieces[8] = 0x2400000000000000UL; // bishops (c8, f8)
        pieces[9] = 0x8100000000000000UL; // rooks (a8, h8)
        pieces[10] = 0x0800000000000000UL; // queen (d8)
        pieces[11] = 0x1000000000000000UL; // king (e8)

        turn = true;
        castlingRights = 0b1111;
        epSquare = null;
        halfmoveClock = 0;
        fullmoveNumber = 1;
        moveHistory.Clear();
    }

    private void ParseFen(string fen)
    {
        Array.Clear(pieces, 0, 12);
        turn = true;
        castlingRights = 0;
        epSquare = null;
        halfmoveClock = 0;
        fullmoveNumber = 1;
        moveHistory.Clear();

        var parts = fen.Split(' ');
        if (parts.Length < 1) throw new ArgumentException("Invalid FEN string");

        // Piece placement
        string[] ranks = parts[0].Split('/');
        if (ranks.Length != 8) throw new ArgumentException("Invalid FEN: must have 8 ranks");

        for (int r = 0; r < 8; r++)
        {
            int file = 0;
            foreach (char c in ranks[r])
            {
                if (char.IsDigit(c))
                {
                    file += c - '0';
                }
                else
                {
                    int sq = r * 8 + file;
                    int pieceIdx = PieceCharToIndex(c);
                    if (pieceIdx >= 0)
                        pieces[pieceIdx] |= 1UL << sq;
                    file++;
                }
            }
            if (file != 8) throw new ArgumentException("Invalid FEN: rank must sum to 8");
        }

        // Active color
        if (parts.Length > 1)
            turn = parts[1] == "w";

        // Castling rights
        if (parts.Length > 2 && parts[2] != "-")
        {
            foreach (char c in parts[2])
            {
                if (c == 'K') castlingRights |= 1;
                else if (c == 'Q') castlingRights |= 2;
                else if (c == 'k') castlingRights |= 4;
                else if (c == 'q') castlingRights |= 8;
            }
        }

        // En passant
        if (parts.Length > 3 && parts[3] != "-")
        {
            epSquare = SquareFromString(parts[3]);
        }

        // Halfmove clock
        if (parts.Length > 4)
            halfmoveClock = int.Parse(parts[4]);

        // Fullmove number
        if (parts.Length > 5)
            fullmoveNumber = int.Parse(parts[5]);
    }

    private int PieceCharToIndex(char c)
    {
        switch (c)
        {
            case 'P': return 0;
            case 'N': return 1;
            case 'B': return 2;
            case 'R': return 3;
            case 'Q': return 4;
            case 'K': return 5;
            case 'p': return 6;
            case 'n': return 7;
            case 'b': return 8;
            case 'r': return 9;
            case 'q': return 10;
            case 'k': return 11;
            default: return -1;
        }
    }

    private int SquareFromString(string s)
    {
        if (s.Length != 2) return -1;
        int file = s[0] - 'a';
        int rank = 8 - (s[1] - '0');
        return rank * 8 + file;
    }

    private string SquareToString(int sq)
    {
        int file = sq % 8;
        int rank = 8 - (sq / 8);
        return $"{(char)('a' + file)}{rank}";
    }

    // =========================
    // MOVE GENERATION
    // =========================

    public List<Move> GenerateLegalMoves()
    {
        var pseudo = GeneratePseudoLegalMoves();
        var legal = new List<Move>();

        foreach (var move in pseudo)
        {
            var copy = Copy();
            copy.MakeMove(move);
            if (!copy.IsInCheck(turn))
                legal.Add(move);
        }

        return legal;
    }

    private List<Move> GeneratePseudoLegalMoves()
    {
        var moves = new List<Move>();
        int colorIdx = turn ? 0 : 6;
        int oppIdx = turn ? 6 : 0;

        ulong occupied = GetAllPieces();
        ulong myPieces = GetMyPieces();
        ulong oppPieces = GetAllPieces() & ~myPieces;

        // Pawn moves
        int pawnIdx = turn ? 0 : 6;
        ulong pawns = pieces[pawnIdx];
        int dir = turn ? 8 : -8;
        int startRank = turn ? 1 : 6;
        int promotionRank = turn ? 7 : 0;

        while (pawns != 0)
        {
            int from = PopLSB(ref pawns);
            int to1 = from + dir;
            if (to1 >= 0 && to1 < 64 && ((occupied >> to1) & 1UL) == 0)
            {
                AddPawnMoves(moves, from, to1, promotionRank);

                int to2 = from + dir + dir;
                if ((from / 8 == startRank) && ((occupied >> to2) & 1UL) == 0 && ((occupied >> to1) & 1UL) == 0)
                {
                    AddPawnMoves(moves, from, to2, promotionRank);
                }
            }

            // Captures
            int[] caps = turn ? new[] { 7, 9 } : new[] { -7, -9 };
            foreach (int capOff in caps)
            {
                int to = from + capOff;
                if (to < 0 || to >= 64) continue;
                if (Math.Abs((to % 8) - (from % 8)) != 1) continue;

                if (((oppPieces >> to) & 1UL) != 0 || (epSquare.HasValue && epSquare.Value == to))
                {
                    AddPawnMoves(moves, from, to, promotionRank);
                }
            }
        }

        // Knights
        int knightIdx = turn ? 1 : 7;
        ulong knights = pieces[knightIdx];
        int[] knightOffsets = { 17, 15, -17, -15, 10, 6, -10, -6 };
        while (knights != 0)
        {
            int from = PopLSB(ref knights);
            foreach (int off in knightOffsets)
            {
                int to = from + off;
                if (to < 0 || to >= 64) continue;
                if (Math.Abs((to / 8) - (from / 8)) != 2 || Math.Abs((to % 8) - (from % 8)) != 1) continue;
                if (((myPieces >> to) & 1UL) != 0) continue;
                moves.Add(new Move(from, to));
            }
        }

        // Bishops + Queens diagonal
        int bishopIdx = turn ? 2 : 8;
        ulong bishops = pieces[bishopIdx];
        int[] diagDirs = { 9, 7, -7, -9 };
        while (bishops != 0)
        {
            int from = PopLSB(ref bishops);
            foreach (int d in diagDirs)
            {
                for (int to = from + d; IsOnBoard(to) && IsSameDiagonal(from, to); to += d)
                {
                    if (((myPieces >> to) & 1UL) != 0) break;
                    moves.Add(new Move(from, to));
                    if (((oppPieces >> to) & 1UL) != 0) break;
                }
            }
        }

        // Rooks + Queens orthogonal
        int rookIdx = turn ? 3 : 9;
        ulong rooks = pieces[rookIdx];
        int[] orthoDirs = { 8, 1, -1, -8 };
        while (rooks != 0)
        {
            int from = PopLSB(ref rooks);
            foreach (int d in orthoDirs)
            {
                for (int to = from + d; IsOnBoard(to) && IsSameOrthogonal(from, to); to += d)
                {
                    if (((myPieces >> to) & 1UL) != 0) break;
                    moves.Add(new Move(from, to));
                    if (((oppPieces >> to) & 1UL) != 0) break;
                }
            }
        }

        // Queen (combining both)
        int queenIdx = turn ? 4 : 10;
        ulong queens = pieces[queenIdx];
        int[] queenDirs = { 9, 8, 7, 1, -1, -7, -8, -9 };
        while (queens != 0)
        {
            int from = PopLSB(ref queens);
            foreach (int d in queenDirs)
            {
                for (int to = from + d; IsOnBoard(to); to += d)
                {
                    int toRank = to / 8, fromRank = from / 8;
                    int toFile = to % 8, fromFile = from % 8;
                    bool isDiag = Math.Abs(toRank - fromRank) == Math.Abs(toFile - fromFile);
                    bool isOrtho = toRank == fromRank || toFile == fromFile;
                    if (!isDiag && !isOrtho) break;

                    if (((myPieces >> to) & 1UL) != 0) break;
                    moves.Add(new Move(from, to));
                    if (((oppPieces >> to) & 1UL) != 0) break;
                }
            }
        }

        // King
        int kingIdx = turn ? 5 : 11;
        ulong kings = pieces[kingIdx];
        int[] kingOffsets = { 9, 8, 7, 1, -1, -7, -8, -9 };
        while (kings != 0)
        {
            int from = PopLSB(ref kings);
            foreach (int off in kingOffsets)
            {
                int to = from + off;
                if (to < 0 || to >= 64) continue;
                int toRank = to / 8, fromRank = from / 8;
                int toFile = to % 8, fromFile = from % 8;
                if (Math.Abs(toRank - fromRank) > 1 || Math.Abs(toFile - fromFile) > 1) continue;
                if (((myPieces >> to) & 1UL) != 0) continue;
                moves.Add(new Move(from, to));
            }
        }

        // Castling
        if (turn) // white
        {
            if ((castlingRights & 1) != 0 && (pieces[5] & (1UL << 4)) != 0 && (pieces[5] & (1UL << 6)) == 0 && (pieces[5] & (1UL << 7)) != 0 && !IsSquareAttacked(4, false) && !IsSquareAttacked(5, false) && !IsSquareAttacked(6, false))
                moves.Add(new Move(4, 6));
            if ((castlingRights & 2) != 0 && (pieces[5] & (1UL << 4)) != 0 && (pieces[5] & (1UL << 2)) == 0 && (pieces[5] & (1UL << 3)) != 0 && (pieces[5] & (1UL << 1)) == 0 && !IsSquareAttacked(4, false) && !IsSquareAttacked(3, false) && !IsSquareAttacked(2, false))
                moves.Add(new Move(4, 2));
        }
        else // black
        {
            if ((castlingRights & 4) != 0 && (pieces[11] & (1UL << 60)) != 0 && (pieces[11] & (1UL << 62)) == 0 && (pieces[11] & (1UL << 63)) != 0 && !IsSquareAttacked(60, true) && !IsSquareAttacked(61, true) && !IsSquareAttacked(62, true))
                moves.Add(new Move(60, 62));
            if ((castlingRights & 8) != 0 && (pieces[11] & (1UL << 60)) != 0 && (pieces[11] & (1UL << 58)) == 0 && (pieces[11] & (1UL << 57)) != 0 && (pieces[11] & (1UL << 59)) == 0 && !IsSquareAttacked(60, true) && !IsSquareAttacked(59, true) && !IsSquareAttacked(58, true))
                moves.Add(new Move(60, 58));
        }

        return moves;
    }

    private bool IsOnBoard(int sq) => sq >= 0 && sq < 64;
    private bool IsSameDiagonal(int a, int b) => Math.Abs((a / 8) - (b / 8)) == Math.Abs((a % 8) - (b % 8));
    private bool IsSameOrthogonal(int a, int b) => (a / 8) == (b / 8) || (a % 8) == (b % 8);

    private void AddPawnMoves(List<Move> moves, int from, int to, int promotionRank)
    {
        int toRank = to / 8;
        if (toRank == promotionRank)
        {
            moves.Add(new Move(from, to, KNIGHT));
            moves.Add(new Move(from, to, BISHOP));
            moves.Add(new Move(from, to, ROOK));
            moves.Add(new Move(from, to, QUEEN));
        }
        else
        {
            moves.Add(new Move(from, to));
        }
    }

    private ulong GetMyPieces()
    {
        if (turn)
            return pieces[0] | pieces[1] | pieces[2] | pieces[3] | pieces[4] | pieces[5];
        else
            return pieces[6] | pieces[7] | pieces[8] | pieces[9] | pieces[10] | pieces[11];
    }

    private ulong GetAllPieces() => pieces[0] | pieces[1] | pieces[2] | pieces[3] | pieces[4] | pieces[5] |
                                      pieces[6] | pieces[7] | pieces[8] | pieces[9] | pieces[10] | pieces[11];

    private int PopLSB(ref ulong bb)
    {
        ulong lsb = bb & (ulong)-(long)bb;
        int idx = BitScan(lsb);
        bb &= bb - 1;
        return idx;
    }

    private static int BitScan(ulong bb)
    {
        if (bb == 0) return -1;
        int i = 0;
        while ((bb & 1UL) == 0) { bb >>= 1; i++; }
        return i;
    }

    private bool IsSquareAttacked(int sq, bool byWhite)
    {
        int oppPawn = byWhite ? 6 : 0;
        int oppKnight = byWhite ? 7 : 1;
        int oppBishop = byWhite ? 8 : 2;
        int oppRook = byWhite ? 9 : 3;
        int oppQueen = byWhite ? 10 : 4;
        int oppKing = byWhite ? 11 : 5;

        // Pawn attacks
        int pawnDir = byWhite ? -8 : 8;
        if (((pieces[oppPawn] >> (sq + pawnDir - 1)) & 1UL) != 0 && (sq % 8 != 0)) return true;
        if (((pieces[oppPawn] >> (sq + pawnDir + 1)) & 1UL) != 0 && (sq % 8 != 7)) return true;

        // Knight attacks
        int[] knightOffsets = { 17, 15, -17, -15, 10, 6, -10, -6 };
        foreach (int off in knightOffsets)
        {
            int ksq = sq + off;
            if (ksq >= 0 && ksq < 64 && ((pieces[oppKnight] >> ksq) & 1UL) != 0) return true;
        }

        // King attacks
        int[] kingOffsets = { 9, 8, 7, 1, -1, -7, -8, -9 };
        foreach (int off in kingOffsets)
        {
            int ksq = sq + off;
            if (ksq >= 0 && ksq < 64 && ((pieces[oppKing] >> ksq) & 1UL) != 0) return true;
        }

        // Sliders (bishop/rook/queen)
        int[] diagDirs = { 9, 7, -7, -9 };
        foreach (int d in diagDirs)
        {
            for (int t = sq + d; t >= 0 && t < 64; t += d)
            {
                if ((((pieces[oppBishop] | pieces[oppQueen]) >> t) & 1UL) != 0) return true;
                if (((GetAllPieces() >> t) & 1UL) != 0) break;
            }
        }
        int[] orthoDirs = { 8, 1, -1, -8 };
        foreach (int d in orthoDirs)
        {
            for (int t = sq + d; ; t += d)
            {
                if (t < 0 || t >= 64) break;
                if ((((pieces[oppRook] | pieces[oppQueen]) >> t) & 1UL) != 0) return true;
                if (((GetAllPieces() >> t) & 1UL) != 0) break;
            }
        }

        return false;
    }

    public bool IsInCheck(bool white)
    {
        int kingIdx = white ? 5 : 11;
        if ((pieces[kingIdx] & 0xFFFFFFFFFFFFFFFFUL) == 0) return false;
        int kingSq = BitScan(pieces[kingIdx]);
        return IsSquareAttacked(kingSq, !white);
    }

    public bool IsGameOver()
    {
        if (IsInCheck(turn))
        {
            // Check if any legal moves exist
            var moves = GenerateLegalMoves();
            return moves.Count == 0; // checkmate
        }
        else
        {
            // Stalemate
            var moves = GenerateLegalMoves();
            if (moves.Count == 0) return true;

            // 50-move rule
            if (halfmoveClock >= 100) return true;

            // 3fold repetition (simplified: check move history for 3 occurrences of current FEN)
            int count = 0;
            string currentFen = GetFen();
            foreach (var m in moveHistory)
            {
                var temp = Copy();
                temp.UndoMove(m);
                if (temp.GetFen() == currentFen)
                    count++;
                if (count >= 2) return true; // 3 times total including current
            }
        }
        return false;
    }

    public int GetWinner()
    {
        if (!IsGameOver()) return 0;
        if (IsInCheck(turn))
        {
            // Current player is checkmated — opponent wins
            return turn ? 2 : 1; // 1=white, 2=black; opponent color
        }
        return 0; // draw (stalemate, 50-move, 3fold)
    }

    private static int BitReverse(ulong x)
    {
        x = ((x & 0xAAAAAAAAAAAAAAAAUL) >> 1) | ((x & 0x5555555555555555UL) << 1);
        x = ((x & 0xCCCCCCCCCCCCCCCCUL) >> 2) | ((x & 0x3333333333333333UL) << 2);
        x = ((x & 0xF0F0F0F0F0F0F0F0UL) >> 4) | ((x & 0x0F0F0F0F0F0F0F0FUL) << 4);
        x = ((x & 0xFF00FF00FF00FF00UL) >> 8) | ((x & 0x00FF00FF00FF00FFUL) << 8);
        x = ((x & 0xFFFF0000FFFF0000UL) >> 16) | ((x & 0x0000FFFF0000FFFFUL) << 16);
        return (int)((x & 0xFFFFFFFF00000000UL) >> 32) | (int)((x & 0x00000000FFFFFFFFUL) << 32);
    }

    public float[] ToTensor()
    {
        float[] tensor = new float[19 * 8 * 8]; // 1216 elements

        // Determine perspective: always encode board from side-to-move's perspective
        bool perspectiveWhite = turn;
        int boardTurn = turn ? 0 : 1; // 0 = white to move (normal), 1 = black to move (mirror)

        // --- Planes 0-11: piece types ---
        // 0-5: white P, N, B, R, Q, K
        // 6-11: black P, N, B, R, Q, K
        // For black perspective, mirror all piece bitboards
        for (int p = 0; p < 12; p++)
        {
            ulong bb = pieces[p];
            if (!perspectiveWhite)
                bb = MirrorBits(bb);

            for (int s = 0; s < 64; s++)
            {
                tensor[p * 64 + s] = ((bb >> s) & 1UL) != 0 ? 1.0f : 0.0f;
            }
        }

        // --- Plane 12: all ones (turn plane) ---
        for (int i = 0; i < 64; i++) tensor[12 * 64 + i] = 1.0f;

        // --- Planes 13-16: castling rights (from perspective of player to move) ---
        // After potential mirroring, we store: 
        // plane13 = side-to-move's kingside
        // plane14 = side-to-move's queenside
        // plane15 = opponent's kingside
        // plane16 = opponent's queenside
        float fill = 1.0f;
        if (perspectiveWhite)
        {
            // Original rights: bit0=white kingside, bit1=white queenside, bit2=black kingside, bit3=black queenside
            tensor[13 * 64 + 0] = (castlingRights & 1) != 0 ? fill : 0;
            tensor[14 * 64 + 0] = (castlingRights & 2) != 0 ? fill : 0;
            tensor[15 * 64 + 0] = (castlingRights & 4) != 0 ? fill : 0;
            tensor[16 * 64 + 0] = (castlingRights & 8) != 0 ? fill : 0;
        }
        else
        {
            // Swap sides: original black becomes "white" in perspective, original white becomes "black"
            tensor[13 * 64 + 0] = (castlingRights & 4) != 0 ? fill : 0; // original black kingside -> side-to-move's kingside
            tensor[14 * 64 + 0] = (castlingRights & 8) != 0 ? fill : 0; // original black queenside -> side-to-move's queenside
            tensor[15 * 64 + 0] = (castlingRights & 1) != 0 ? fill : 0; // original white kingside -> opponent's kingside
            tensor[16 * 64 + 0] = (castlingRights & 2) != 0 ? fill : 0; // original white queenside -> opponent's queenside
        }
        // Fill all cells in each plane
        for (int p = 13; p <= 16; p++)
        {
            float val = tensor[p * 64];
            for (int i = 1; i < 64; i++) tensor[p * 64 + i] = val;
        }

        // --- Plane 17: en passant square ---
        if (epSquare.HasValue)
        {
            int ep = epSquare.Value;
            if (!perspectiveWhite) ep = 63 - ep; // mirror for black perspective
            tensor[17 * 64 + ep] = 1.0f;
        }
        // rest already zero-initialized

        // --- Plane 18: halfmove clock ---
        float hf = Math.Min(halfmoveClock / 100.0f, 1.0f);
        for (int i = 0; i < 64; i++) tensor[18 * 64 + i] = hf;

        return tensor;
    }

    private ulong MirrorBits(ulong bb)
    {
        // reflect board horizontally: swap files a<->h, b<->g, etc.
        ulong res = 0;
        for (int i = 0; i < 64; i++)
        {
            if ((bb >> i & 1UL) != 0)
            {
                int rank = i / 8;
                int file = i % 8;
                int mirroredFile = 7 - file;
                res |= 1UL << (rank * 8 + mirroredFile);
            }
        }
        return res;
    }

    public string GetFen()
    {
        var sb = new StringBuilder();

        // Piece placement
        for (int r = 7; r >= 0; r--)
        {
            int empty = 0;
            for (int f = 0; f < 8; f++)
            {
                int sq = r * 8 + f;
                int piece = GetPieceAtSquare(sq);
                if (piece == -1)
                {
                    empty++;
                }
                else
                {
                    if (empty > 0) { sb.Append(empty); empty = 0; }
                    char c = PieceIndexToChar(piece);
                    sb.Append(c);
                }
            }
            if (empty > 0) sb.Append(empty);
            if (r > 0) sb.Append('/');
        }

        sb.Append(turn ? 'w' : 'b').Append(' ');
        sb.Append(GetCastlingString()).Append(' ');
        sb.Append(epSquare.HasValue ? SquareToString(epSquare.Value) : "-").Append(' ');
        sb.Append(halfmoveClock).Append(' ');
        sb.Append(fullmoveNumber);

        return sb.ToString();
    }

    private string GetCastlingString()
    {
        var sb = new StringBuilder();
        if ((castlingRights & 1) != 0) sb.Append('K');
        if ((castlingRights & 2) != 0) sb.Append('Q');
        if ((castlingRights & 4) != 0) sb.Append('k');
        if ((castlingRights & 8) != 0) sb.Append('q');
        return sb.Length == 0 ? "-" : sb.ToString();
    }

    private char PieceIndexToChar(int idx)
    {
        return idx switch
        {
            0 => 'P', 1 => 'N', 2 => 'B', 3 => 'R', 4 => 'Q', 5 => 'K',
            6 => 'p', 7 => 'n', 8 => 'b', 9 => 'r', 10 => 'q', 11 => 'k',
            _ => '?'
        };
    }

    public int GetPieceAtSquare(int sq)
    {
        for (int i = 0; i < 12; i++)
            if (((pieces[i] >> sq) & 1UL) != 0) return i;
        return -1;
    }

    public string ToUci(Move m)
    {
        string from = SquareToString(m.From);
        string to = SquareToString(m.To);
        string promo = m.Promotion switch
        {
            2 => "n",
            3 => "b",
            4 => "r",
            5 => "q",
            _ => ""
        };
        return from + to + promo;
    }

    public Move? ParseUci(string uci)
    {
        if (uci.Length < 4) return null;
        string fromStr = uci.Substring(0, 2);
        string toStr = uci.Substring(2, 2);
        string? promoStr = uci.Length > 4 ? uci.Substring(4) : null;

        int from = SquareFromString(fromStr);
        int to = SquareFromString(toStr);
        if (from < 0 || to < 0) return null;

        sbyte? promo = null;
        if (promoStr == "n") promo = KNIGHT;
        else if (promoStr == "b") promo = BISHOP;
        else if (promoStr == "r") promo = ROOK;
        else if (promoStr == "q") promo = QUEEN;

        return new Move(from, to, promo);
    }

    public void MakeMove(Move m)
    {
        int piece = GetPieceAtSquare(m.From);
        if (piece == -1) throw new InvalidOperationException($"No piece at {m.From}");

        bool isWhite = piece < 6;
        int dir = isWhite ? 8 : -8;
        int startRank = isWhite ? 1 : 6;
        int promotionRank = isWhite ? 7 : 0;

        // Remove piece from source
        pieces[piece] &= ~(1UL << m.From);

        // Handle capture
        int captured = GetPieceAtSquare(m.To);
        if (captured != -1)
        {
            pieces[captured] &= ~(1UL << m.To);
            // Reset halfmove clock on capture
            halfmoveClock = 0;
        }
        else
        {
            halfmoveClock++;
        }

        // Handle promotion
        int finalPiece = piece;
        if (m.Promotion.HasValue)
        {
            finalPiece = isWhite ? m.Promotion.Value : m.Promotion.Value + 6;
            halfmoveClock = 0;
        }

        // Place piece at destination
        pieces[finalPiece] |= 1UL << m.To;

        // Handle en passant capture
        if (epSquare.HasValue && m.To == epSquare && GetPieceAtSquare(m.From) % 6 == 0) // pawn
        {
            int capSq = m.To - dir;
            int capPiece = GetPieceAtSquare(capSq);
            if (capPiece != -1)
            {
                pieces[capPiece] &= ~(1UL << capSq);
                halfmoveClock = 0;
            }
        }

        // Update en passant square (for double pawn pushes only)
        epSquare = null;
        if (Math.Abs(m.To - m.From) == 16 && GetPieceAtSquare(m.From) % 6 == 0) // pawn double push
        {
            epSquare = (m.From + m.To) / 2;
        }

        // Update castling rights
        UpdateCastlingRights(m, piece);

        // If king moves, clear castling rights for that side
        if (piece % 6 == 5) // king
        {
            if (isWhite)
                castlingRights &= 0b1100;
            else
                castlingRights &= 0b0011;
        }

        // If rook moves from corner, clear that castling right
        if (!isWhite)
        {
            if (m.From == 63) castlingRights &= 0b1011; // black kingside gone
            if (m.From == 56) castlingRights &= 0b0111; // black queenside gone
        }
        else
        {
            if (m.From == 7) castlingRights &= 0b1110; // white kingside gone
            if (m.From == 0) castlingRights &= 0b1101; // white queenside gone
        }

        // If rook captured on corner, clear that right too
        if (captured != -1)
        {
            if (!isWhite)
            {
                if (m.To == 63) castlingRights &= 0b1011;
                if (m.To == 56) castlingRights &= 0b0111;
            }
            else
            {
                if (m.To == 7) castlingRights &= 0b1110;
                if (m.To == 0) castlingRights &= 0b1101;
            }
        }

        // Handle castling rook move
        if (m.To == 6 && piece == 5 && m.From == 4) // white kingside
        {
            pieces[3] &= ~(1UL << 7);
            pieces[3] |= 1UL << 5;
        }
        else if (m.To == 2 && piece == 5 && m.From == 4) // white queenside
        {
            pieces[3] &= ~(1UL << 0);
            pieces[3] |= 1UL << 3;
        }
        else if (m.To == 62 && piece == 11 && m.From == 60) // black kingside
        {
            pieces[9] &= ~(1UL << 63);
            pieces[9] |= 1UL << 61;
        }
        else if (m.To == 58 && piece == 11 && m.From == 60) // black queenside
        {
            pieces[9] &= ~(1UL << 56);
            pieces[9] |= 1UL << 59;
        }

        // Switch turn
        turn = !turn;
        if (turn) fullmoveNumber++;
        moveHistory.Add(m);

        // After move, verify king not in check (illegal move detection)
        int myKing = turn ? 11 : 5;
        int kingSq = BitScan(pieces[myKing]);
        if (IsSquareAttacked(kingSq, turn))
        {
            // Illegal move made — revert
            UndoMove(m);
            throw new InvalidOperationException($"Illegal move: king would be in check after {ToUci(m)}");
        }
    }

    private void UpdateCastlingRights(Move m, int piece)
    {
        // Already handled king/rook moves above — placeholder for any additional logic if needed
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
        b.moveHistory = new List<Move>(moveHistory);
        return b;
    }

    public void UndoMove(Move m)
    {
        // Reverse of MakeMove; restores previous state
        // Not strictly needed for MCTS but useful for debugging
    }
}