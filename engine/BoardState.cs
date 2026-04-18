using System;
using System.Collections.Generic;
using System.Linq;

public class BoardState
{
    private ulong[] pieces = new ulong[12];
    private bool turn = true;
    private ulong castlingRights = 0xF;
    private int? epSquare = null;
    private int halfmoveClock = 0;
    private int fullmoveNumber = 1;
    private List<Move> moveHistory = new List<Move>();

    public const int A1 = 0, B1 = 1, C1 = 2, D1 = 3, E1 = 4, F1 = 5, G1 = 6, H1 = 7;
    public const int A2 = 8, B2 = 9, C2 = 10, D2 = 11, E2 = 12, F2 = 13, G2 = 14, H2 = 15;
    public const int A3 = 16, B3 = 17, C3 = 18, D3 = 19, E3 = 20, F3 = 21, G3 = 22, H3 = 23;
    public const int A4 = 24, B4 = 25, C4 = 26, D4 = 27, E4 = 28, F4 = 29, G4 = 30, H4 = 31;
    public const int A5 = 32, B5 = 33, C5 = 34, D5 = 35, E5 = 36, F5 = 37, G5 = 38, H5 = 39;
    public const int A6 = 40, B6 = 41, C6 = 42, D6 = 43, E6 = 44, F6 = 45, G6 = 46, H6 = 47;
    public const int A7 = 48, B7 = 49, C7 = 50, D7 = 51, E7 = 52, F7 = 53, G7 = 54, H7 = 55;
    public const int A8 = 56, B8 = 57, C8 = 58, D8 = 59, E8 = 60, F8 = 61, G8 = 62, H8 = 63;

    private static readonly int[] PawnOffsets = { 8, 9, 7, -8, -9, -7 };
    private static readonly int[] KnightOffsets = { 17, 15, 10, 6, -6, -10, -15, -17 };
    private static readonly int[] BishopOffsets = { 9, 7, -7, -9 };
    private static readonly int[] RookOffsets = { 8, 1, -1, -8 };
    private static readonly int[] KingOffsets = { 9, 8, 7, 1, -1, -8, -7, -9 };

    public bool Turn => turn;
    public int? EpSquare => epSquare;

    public BoardState()
    {
        pieces[0] = 0xFF;
        pieces[1] = 0x42;
        pieces[2] = 0x24;
        pieces[3] = 0x81;
        pieces[4] = 0x10;
        pieces[5] = 0x08;
        pieces[6] = 0xFF00;
        pieces[7] = 0x4200;
        pieces[8] = 0x2400;
        pieces[9] = 0x810000;
        pieces[10] = 0x100000;
        pieces[11] = 0x080000;
    }

    public BoardState(string fen)
    {
        ParseFen(fen);
    }

    private void ParseFen(string fen)
    {
        var parts = fen.Split(' ');
        var position = parts[0];
        var rows = position.Split('/');

        pieces = new ulong[12];
        for (int i = 0; i < 8; i++)
        {
            var row = rows[7 - i];
            int file = 0;
            foreach (char c in row)
            {
                if (char.IsDigit(c))
                {
                    file += c - '0';
                }
                else
                {
                    int square = (7 - i) * 8 + file;
                    int pieceIndex = GetPieceIndex(c);
                    if (pieceIndex >= 0) pieces[pieceIndex] |= (1UL << square);
                    file++;
                }
            }
        }

        turn = parts[1] == "w";

        castlingRights = 0;
        if (parts[2].Contains('K')) castlingRights |= 0x1;
        if (parts[2].Contains('Q')) castlingRights |= 0x2;
        if (parts[2].Contains('k')) castlingRights |= 0x4;
        if (parts[2].Contains('q')) castlingRights |= 0x8;

        epSquare = parts[3] == "-" ? null : (parts[3][0] - 'a') + (8 - (parts[3][1] - '0')) * 8;
        halfmoveClock = int.Parse(parts[4]);
        fullmoveNumber = int.Parse(parts[5]);
    }

    private int GetPieceIndex(char c)
    {
        return c switch
        {
            'P' => 0, 'N' => 1, 'B' => 2, 'R' => 3, 'Q' => 4, 'K' => 5,
            'p' => 6, 'n' => 7, 'b' => 8, 'r' => 9, 'q' => 10, 'k' => 11,
            _ => -1
        };
    }

    public string GetFen()
    {
        var parts = new List<string>();
        
        for (int row = 7; row >= 0; row--)
        {
            var rowStr = "";
            int empty = 0;
            for (int col = 0; col < 8; col++)
            {
                int square = row * 8 + col;
                bool found = false;
                for (int i = 0; i < 12; i++)
                {
                    if ((pieces[i] & (1UL << square)) != 0)
                    {
                        if (empty > 0)
                        {
                            rowStr += empty.ToString();
                            empty = 0;
                        }
                        rowStr += "PNBRQKpnbrqk"[i];
                        found = true;
                        break;
                    }
                }
                if (!found) empty++;
            }
            if (empty > 0) rowStr += empty.ToString();
            parts.Add(rowStr);
        }
        var fen = string.Join("/", parts);

        fen += " " + (turn ? "w" : "b");

        var castling = "";
        if ((castlingRights & 0x1) != 0) castling += "K";
        if ((castlingRights & 0x2) != 0) castling += "Q";
        if ((castlingRights & 0x4) != 0) castling += "k";
        if ((castlingRights & 0x8) != 0) castling += "q";
        fen += " " + (castling.Length > 0 ? castling : "-");

        if (epSquare.HasValue)
        {
            int col = epSquare.Value % 8;
            int row = 7 - epSquare.Value / 8;
            fen += " " + ((char)('a' + col) + row.ToString());
        }
        else
        {
            fen += " -";
        }

        fen += " " + halfmoveClock + " " + fullmoveNumber;
        return fen;
    }

    public float[] ToTensor()
    {
        float[] tensor = new float[19 * 8 * 8];
        
        var board = this;
        if (!turn)
        {
            board = FlipBoard();
        }

        for (int i = 0; i < 6; i++)
        {
            ulong mask = board.pieces[i];
            while (mask != 0)
            {
                int square = BitOperations.ExtractLeastSignificantBit(ref mask);
                int row = 7 - (square / 8);
                int col = square % 8;
                tensor[i * 64 + row * 8 + col] = 1.0f;
            }
        }

        for (int i = 6; i < 12; i++)
        {
            ulong mask = board.pieces[i];
            while (mask != 0)
            {
                int square = BitOperations.ExtractLeastSignificantBit(ref mask);
                int row = 7 - (square / 8);
                int col = square % 8;
                tensor[i * 64 + row * 8 + col] = 1.0f;
            }
        }

        if (turn) tensor[12 * 64 + 0] = 1.0f;

        if ((castlingRights & 0x1) != 0) tensor[13 * 64 + 0] = 1.0f;
        if ((castlingRights & 0x2) != 0) tensor[14 * 64 + 0] = 1.0f;
        if ((castlingRights & 0x4) != 0) tensor[15 * 64 + 0] = 1.0f;
        if ((castlingRights & 0x8) != 0) tensor[16 * 64 + 0] = 1.0f;

        if (epSquare.HasValue)
        {
            int row = 7 - (epSquare.Value / 8);
            int col = epSquare.Value % 8;
            tensor[17 * 64 + row * 8 + col] = 1.0f;
        }

        tensor[18 * 64 + 0] = halfmoveClock / 100.0f;

        return tensor;
    }

    private BoardState FlipBoard()
    {
        var flipped = new BoardState();
        for (int i = 0; i < 6; i++)
        {
            flipped.pieces[i] = FlipSquare(pieces[i + 6]);
            flipped.pieces[i + 6] = FlipSquare(pieces[i]);
        }
        flipped.turn = !turn;
        flipped.castlingRights = ((castlingRights & 0x3) << 2) | ((castlingRights & 0xC) >> 2);
        flipped.epSquare = epSquare.HasValue ? FlipSquareIndex(epSquare.Value) : null;
        flipped.halfmoveClock = halfmoveClock;
        flipped.fullmoveNumber = fullmoveNumber;
        return flipped;
    }

    private static ulong FlipSquare(ulong mask)
    {
        ulong result = 0;
        for (int i = 0; i < 64; i++)
        {
            if ((mask & (1UL << i)) != 0)
            {
                int newRow = 7 - (i / 8);
                int newCol = 7 - (i % 8);
                result |= 1UL << (newRow * 8 + newCol);
            }
        }
        return result;
    }

    private static int FlipSquareIndex(int square)
    {
        return (7 - (square / 8)) * 8 + (7 - (square % 8));
    }

    public List<Move> GenerateLegalMoves()
    {
        var moves = new List<Move>();
        int kingSquare = FindKing(turn ? 0 : 6);
        
        GeneratePawnMoves(moves);
        GenerateKnightMoves(moves);
        GenerateSliderMoves(moves, 2, 4, BishopOffsets);
        GenerateSliderMoves(moves, 3, 4, RookOffsets);
        GenerateSliderMoves(moves, 4, 8, BishopOffsets.Concat(RookOffsets).ToArray());
        GenerateKingMoves(moves);

        if (HasLegalCapture(moves, kingSquare))
        {
            return moves.Where(m => !GivesCheck(m, kingSquare)).ToList();
        }

        return moves;
    }

    private int FindKing(int kingIndex)
    {
        ulong kingMask = pieces[kingIndex];
        for (int i = 0; i < 64; i++)
        {
            if ((kingMask & (1UL << i)) != 0) return i;
        }
        return -1;
    }

    private void GeneratePawnMoves(List<Move> moves)
    {
        int startRow = turn ? 1 : 6;
        int direction = turn ? 8 : -8;
        int pieceIndex = turn ? 0 : 6;

        for (int from = 0; from < 64; from++)
        {
            if ((pieces[pieceIndex] & (1UL << from)) == 0) continue;

            int row = from / 8;
            int col = from % 8;

            int to1 = from + direction;
            if (to1 >= 0 && to1 < 64 && !IsOccupied(to1))
            {
                if (to1 / 8 == (turn ? 7 : 0))
                {
                    foreach (var promo in new[] { 3, 2, 1, 4 })
                        moves.Add(new Move(from, to1, promo));
                }
                else
                {
                    moves.Add(new Move(from, to1));
                }

                int startRowVal = turn ? 1 : 6;
                if (row == startRowVal)
                {
                    int to2 = from + 2 * direction;
                    if (!IsOccupied(to2))
                        moves.Add(new Move(from, to2));
                }
            }

            int[] captureOffsets = turn ? new[] { 9, 7 } : new[] { -7, -9 };
            foreach (int offset in captureOffsets)
            {
                int toCap = from + offset;
                if (toCap >= 0 && toCap < 64)
                {
                    int toCol = toCap % 8;
                    if (Math.Abs(col - toCol) == 1)
                    {
                        if (turn)
                        {
                            if ((pieces[6] & (1UL << toCap)) != 0 || (epSquare.HasValue && epSquare.Value == toCap))
                            {
                                if (toCap / 8 == 7)
                                {
                                    foreach (var promo in new[] { 3, 2, 1, 4 })
                                        moves.Add(new Move(from, toCap, promo));
                                }
                                else
                                {
                                    moves.Add(new Move(from, toCap));
                                }
                            }
                        }
                        else
                        {
                            if ((pieces[0] & (1UL << toCap)) != 0 || (epSquare.HasValue && epSquare.Value == toCap))
                            {
                                if (toCap / 8 == 0)
                                {
                                    foreach (var promo in new[] { 3, 2, 1, 4 })
                                        moves.Add(new Move(from, toCap, promo));
                                }
                                else
                                {
                                    moves.Add(new Move(from, toCap));
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void GenerateKnightMoves(List<Move> moves)
    {
        int knightIndex = turn ? 1 : 7;
        ulong knights = pieces[knightIndex];
        
        while (knights != 0)
        {
            int from = BitOperations.ExtractLeastSignificantBit(ref knights);
            foreach (int offset in KnightOffsets)
            {
                int to = from + offset;
                if (to >= 0 && to < 64 && Math.Abs((to / 8) - (from / 8)) <= 2 && Math.Abs((to % 8) - (from % 8)) <= 2)
                {
                    if (!IsOccupiedByColor(to, turn))
                        moves.Add(new Move(from, to));
                }
            }
        }
    }

    private void GenerateSliderMoves(List<Move> moves, int pieceIndex, int maxSteps, int[] offsets)
    {
        ulong sliders = pieces[pieceIndex];
        if (!turn) sliders = pieces[pieceIndex + 6];
        
        while (sliders != 0)
        {
            int from = BitOperations.ExtractLeastSignificantBit(ref sliders);
            foreach (int offset in offsets)
            {
                int to = from;
                for (int step = 0; step < maxSteps; step++)
                {
                    to += offset;
                    if (to < 0 || to >= 64) break;
                    if (Math.Abs((to / 8) - ((to - offset) / 8)) > 1 || Math.Abs((to % 8) - ((to - offset) % 8)) > 1) break;
                    
                    if (IsOccupiedByColor(to, turn))
                    {
                        moves.Add(new Move(from, to));
                        break;
                    }
                    else
                    {
                        moves.Add(new Move(from, to));
                    }
                }
            }
        }
    }

    private void GenerateKingMoves(List<Move> moves)
    {
        int kingIndex = turn ? 5 : 11;
        ulong king = pieces[kingIndex];
        
        while (king != 0)
        {
            int from = BitOperations.ExtractLeastSignificantBit(ref king);
            foreach (int offset in KingOffsets)
            {
                int to = from + offset;
                if (to >= 0 && to < 64 && Math.Abs((to / 8) - (from / 8)) <= 1 && Math.Abs((to % 8) - (from % 8)) <= 1)
                {
                    if (!IsOccupiedByColor(to, turn))
                        moves.Add(new Move(from, to));
                }
            }

            if (turn && from == E1)
            {
                if ((castlingRights & 0x1) != 0 && !IsOccupied(F1) && !IsOccupied(G1) && !IsSquareAttacked(E1, false) && !IsSquareAttacked(F1, false))
                    moves.Add(new Move(E1, G1));
                if ((castlingRights & 0x2) != 0 && !IsOccupied(D1) && !IsOccupied(C1) && !IsOccupied(B1) && !IsSquareAttacked(E1, false) && !IsSquareAttacked(D1, false))
                    moves.Add(new Move(E1, C1));
            }
            else if (!turn && from == E8)
            {
                if ((castlingRights & 0x4) != 0 && !IsOccupied(F8) && !IsOccupied(G8) && !IsSquareAttacked(E8, true) && !IsSquareAttacked(F8, true))
                    moves.Add(new Move(E8, G8));
                if ((castlingRights & 0x8) != 0 && !IsOccupied(D8) && !IsOccupied(C8) && !IsOccupied(B8) && !IsSquareAttacked(E8, true) && !IsSquareAttacked(D8, true))
                    moves.Add(new Move(E8, C8));
            }
        }
    }

    private bool IsOccupied(int square) => (GetAllPieces() & (1UL << square)) != 0;
    private bool IsOccupiedByColor(int square, bool isWhite) => isWhite ? (GetWhitePieces() & (1UL << square)) != 0 : (GetBlackPieces() & (1UL << square)) != 0;
    private ulong GetAllPieces() => pieces.Aggregate(0UL, (a, b) => a | b);
    private ulong GetWhitePieces() => pieces.Take(6).Aggregate(0UL, (a, b) => a | b);
    private ulong GetBlackPieces() => pieces.Skip(6).Aggregate(0UL, (a, b) => a | b);

    private bool IsSquareAttacked(int square, bool byWhite)
    {
        int pawnIndex = byWhite ? 6 : 0;
        int pawnDir = byWhite ? -8 : 8;
        
        if (square + pawnDir >= 0 && square + pawnDir < 64)
        {
            int col = square % 8;
            int targetCol = (square + pawnDir) % 8;
            if (Math.Abs(col - targetCol) == 1 && (pieces[pawnIndex] & (1UL << (square + pawnDir))) != 0)
                return true;
        }

        int knightIndex = byWhite ? 7 : 1;
        foreach (int offset in KnightOffsets)
        {
            int to = square + offset;
            if (to >= 0 && to < 64 && Math.Abs((to / 8) - (square / 8)) <= 2 && Math.Abs((to % 8) - (square % 8)) <= 2)
                if ((pieces[knightIndex] & (1UL << to)) != 0) return true;
        }

        foreach (int offset in BishopOffsets)
        {
            int to = square;
            for (int i = 0; i < 7; i++)
            {
                to += offset;
                if (to < 0 || to >= 64) break;
                if (Math.Abs((to / 8) - ((to - offset) / 8)) > 1 || Math.Abs((to % 8) - ((to - offset) % 8)) > 1) break;
                if (IsOccupied(to))
                {
                    int pieceIndex = GetPieceAt(to);
                    if (pieceIndex >= 2 && pieceIndex <= 4)
                        return true;
                    break;
                }
            }
        }

        foreach (int offset in RookOffsets)
        {
            int to = square;
            for (int i = 0; i < 7; i++)
            {
                to += offset;
                if (to < 0 || to >= 64) break;
                if (Math.Abs((to / 8) - ((to - offset) / 8)) > 1 || Math.Abs((to % 8) - ((to - offset) % 8)) > 1) break;
                if (IsOccupied(to))
                {
                    int pieceIndex = GetPieceAt(to);
                    if (pieceIndex >= 3 && pieceIndex <= 4)
                        return true;
                    break;
                }
            }
        }

        foreach (int offset in KingOffsets)
        {
            int to = square + offset;
            if (to >= 0 && to < 64 && Math.Abs((to / 8) - (square / 8)) <= 1 && Math.Abs((to % 8) - (square % 8)) <= 1)
            {
                int pieceIndex = GetPieceAt(to);
                if ((byWhite && pieceIndex == 5) || (!byWhite && pieceIndex == 11))
                    return true;
            }
        }

        return false;
    }

    private int GetPieceAt(int square)
    {
        for (int i = 0; i < 12; i++)
            if ((pieces[i] & (1UL << square)) != 0) return i;
        return -1;
    }

    private bool HasLegalCapture(List<Move> moves, int kingSquare)
    {
        foreach (var move in moves)
        {
            var copy = Copy();
            copy.MakeMove(move);
            if (!copy.IsInCheck(turn))
                return true;
        }
        return false;
    }

    private bool GivesCheck(Move move, int kingSquare)
    {
        var copy = Copy();
        copy.MakeMove(move);
        return copy.IsInCheck(turn);
    }

    public bool IsInCheck(bool isWhite)
    {
        int kingSquare = FindKing(isWhite ? 0 : 6);
        return IsSquareAttacked(kingSquare, !isWhite);
    }

    public bool IsGameOver()
    {
        return IsCheckmate() || IsStalemate() || IsThreefoldRepetition() || IsFiftyMoveRule() || IsInsufficientMaterial();
    }

    public bool IsCheckmate()
    {
        if (!IsInCheck(turn)) return false;
        return GenerateLegalMoves().Count == 0;
    }

    public bool IsStalemate()
    {
        if (IsInCheck(turn)) return false;
        return GenerateLegalMoves().Count == 0;
    }

    public bool IsThreefoldRepetition()
    {
        var positions = new Dictionary<string, int>();
        string currentFen = GetFen();
        for (int i = 0; i < moveHistory.Count; i++)
        {
            if (i >= moveHistory.Count - 1) break;
            positions.TryGetValue(currentFen, out int count);
            positions[currentFen] = count + 1;
            var copy = Copy();
            copy.MakeMove(moveHistory[i]);
            currentFen = copy.GetFen();
        }
        return positions.Any(p => p.Value >= 3);
    }

    public bool IsFiftyMoveRule()
    {
        return halfmoveClock >= 100;
    }

    public bool IsInsufficientMaterial()
    {
        int whitePieces = 0, blackPieces = 0;
        foreach (int i in new[] { 0, 1, 2, 3, 4, 5 })
            if (pieces[i] != 0) whitePieces++;
        foreach (int i in new[] { 6, 7, 8, 9, 10, 11 })
            if (pieces[i] != 0) blackPieces++;

        if (whitePieces == 1 && blackPieces == 1) return true;
        if (whitePieces == 1 && blackPieces == 2 && (pieces[8] != 0 || pieces[7] != 0)) return true;
        if (blackPieces == 1 && whitePieces == 2 && (pieces[2] != 0 || pieces[1] != 0)) return true;
        return false;
    }

    public int? GetWinner()
    {
        if (IsCheckmate()) return turn ? 1 : 0;
        return null;
    }

    public void MakeMove(Move move)
    {
        int from = move.From;
        int to = move.To;
        int piece = GetPieceAt(from);

        pieces[piece] &= ~(1UL << from);
        pieces[piece] |= 1UL << to;

        if (move.Promotion.HasValue)
        {
            pieces[piece] &= ~(1UL << to);
            int promoIndex = turn ? move.Promotion.Value : move.Promotion.Value + 6;
            pieces[promoIndex] |= 1UL << to;
        }

        if ((pieces[5] & (1UL << E1)) == 0 && piece == 5)
        {
            castlingRights &= 0xC;
        }
        if ((pieces[11] & (1UL << E8)) == 0 && piece == 11)
        {
            castlingRights &= 0x3;
        }

        if (move.From == A1 || move.To == A1) castlingRights &= 0xE;
        if (move.From == H1 || move.To == H1) castlingRights &= 0xD;
        if (move.From == A8 || move.To == A8) castlingRights &= 0x7;
        if (move.From == H8 || move.To == H8) castlingRights &= 0xB;

        if (epSquare.HasValue && piece == 0 && to == epSquare.Value)
        {
            pieces[6] &= ~(1UL << (to - 8));
        }
        if (epSquare.HasValue && piece == 6 && to == epSquare.Value)
        {
            pieces[0] &= ~(1UL << (to + 8));
        }

        epSquare = null;
        if (piece == 0 && from / 8 == 1 && to / 8 == 3) epSquare = from + 8;
        if (piece == 6 && from / 8 == 6 && to / 8 == 4) epSquare = from - 8;

        if (move.IsCastling)
        {
            if (to == G1)
            {
                pieces[3] &= ~(1UL << H1);
                pieces[3] |= 1UL << F1;
            }
            else if (to == C1)
            {
                pieces[3] &= ~(1UL << A1);
                pieces[3] |= 1UL << D1;
            }
            else if (to == G8)
            {
                pieces[9] &= ~(1UL << H8);
                pieces[9] |= 1UL << F8;
            }
            else if (to == C8)
            {
                pieces[9] &= ~(1UL << A8);
                pieces[9] |= 1UL << D8;
            }
        }

        halfmoveClock++;
        if (piece == 0 || piece == 6) halfmoveClock = 0;

        if (!turn) fullmoveNumber++;

        turn = !turn;
        moveHistory.Add(move);
    }

    public BoardState Copy()
    {
        var copy = new BoardState();
        Array.Copy(pieces, copy.pieces, pieces.Length);
        copy.turn = turn;
        copy.castlingRights = castlingRights;
        copy.epSquare = epSquare;
        copy.halfmoveClock = halfmoveClock;
        copy.fullmoveNumber = fullmoveNumber;
        copy.moveHistory = new List<Move>(moveHistory);
        return copy;
    }

    public string ToUci(Move move)
    {
        string uci = SquareToString(move.From) + SquareToString(move.To);
        if (move.Promotion.HasValue)
        {
            uci += "nbrq"[move.Promotion.Value - 1];
        }
        return uci;
    }

    public Move? ParseUci(string uci)
    {
        if (uci.Length < 4) return null;
        int from = StringToSquare(uci.Substring(0, 2));
        int to = StringToSquare(uci.Substring(2, 2));
        int? promo = null;
        if (uci.Length == 5)
        {
            promo = "nbrq".IndexOf(uci[4]) + 1;
        }
        return new Move(from, to, promo);
    }

    private static string SquareToString(int square)
    {
        char file = (char)('a' + (square % 8));
        char rank = (char)('8' - (square / 8));
        return "" + file + rank;
    }

    private static int StringToSquare(string s)
    {
        return (s[0] - 'a') + (8 - (s[1] - '0')) * 8;
    }
}

public struct Move
{
    public int From { get; }
    public int To { get; }
    public int? Promotion { get; }

    public Move(int from, int to, int? promotion = null)
    {
        From = from;
        To = to;
        Promotion = promotion;
    }

    public bool IsCastling => (From == 4 || From == 60) && (To == 6 || To == 2 || To == 62 || To == 58);
}

public static class BitOperations
{
    public static int ExtractLeastSignificantBit(ref ulong mask)
    {
        int index = (int)(mask & ~(mask - 1));
        int bitIndex = (index == 0) ? 0 : (int)Math.Log2(index);
        mask &= mask - 1;
        return bitIndex;
    }
}