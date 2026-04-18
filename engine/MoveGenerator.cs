using System;
using System.Collections.Generic;
using System.Linq;

public class MoveGenerator
{
    public static List<string> GenerateLegalMoves(BoardState board)
    {
        var moves = board.GenerateLegalMoves();
        return moves.Select(m => board.ToUci(m)).ToList();
    }

    public static bool IsMoveLegal(BoardState board, string uci)
    {
        var move = board.ParseUci(uci);
        if (move == null) return false;
        
        var legalMoves = board.GenerateLegalMoves();
        return legalMoves.Any(m => m.From == move.Value.From && m.To == move.Value.To && m.Promotion == move.Value.Promotion);
    }
}