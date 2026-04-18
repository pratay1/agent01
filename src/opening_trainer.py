import random
import chess
import numpy as np
from typing import Dict, List, Tuple
from src.board_encoder import board_to_tensor
from src.move_encoder import move_to_index


OPENING_MOVES = [
    "e2e4", "d2d4", "c2c4", "g1f3", "g2g3", "b2b3", "f2f4"
]

COMMON_OPENINGS = {
    "e2e4 e7e5": "Open Game",
    "e2e4 c7c5": "Sicilian Defense",
    "e2e4 e7e6": "French Defense",
    "e2e4 c7c6": "Caro-Kann Defense",
    "e2e4 d7d6": "Pirc/Modern Defense",
    "d2d4 d7d5": "Closed Game",
    "d2d4 g8f6": "Indian Defense",
    "d2d4 f7f5": "Dutch Defense",
}


class OpeningTrainer:
    def __init__(self):
        self.opening_frequency: Dict[str, int] = {opening: 1 for opening in OPENING_MOVES}
        self.opening_history: List[str] = []
        self.total_opening_games = 0

    def sample_first_move(self) -> str:
        total = sum(self.opening_frequency.values())
        weights = [freq / total for freq in self.opening_frequency.values()]
        selected = random.choices(list(self.opening_frequency.keys()), weights=weights, k=1)[0]
        return selected

    def update_frequency(self, opening_key: str):
        self.opening_frequency[opening_key] = self.opening_frequency.get(opening_key, 0) + 1
        self.opening_history.append(opening_key)
        if len(self.opening_history) > 100:
            self.opening_history.pop(0)
        self.total_opening_games += 1

    def get_opening_key(self, board: chess.Board) -> str:
        moves = list(board.move_stack)
        if len(moves) >= 1:
            first_move = moves[0].uci()[:2] + moves[0].uci()[2:4]
            return first_move
        return ""

    def get_opening_name(self, board: chess.Board) -> str:
        moves = list(board.move_stack)
        if len(moves) >= 2:
            key = moves[0].uci() + " " + moves[1].uci()
            return COMMON_OPENINGS.get(key, "")
        elif len(moves) >= 1:
            first_move = moves[0].uci()
            opening_names = {
                "e2e4": "King's Pawn",
                "d2d4": "Queen's Pawn",
                "c2c4": "English Opening",
                "g1f3": "Reti Opening",
                "g2g3": "King's Fianchetto",
                "b2b3": "Nimzo-Larsen",
                "f2f4": "Bird's Opening",
            }
            return opening_names.get(first_move, "")
        return ""

    def check_opening_balance(self) -> Tuple[bool, str]:
        if self.total_opening_games < 10:
            return True, ""
        
        max_freq = max(self.opening_frequency.values())
        total = sum(self.opening_frequency.values())
        max_percentage = max_freq / total
        
        if max_percentage > 0.35:
            overrepresented = max(self.opening_frequency.items(), key=lambda x: x[1])[0]
            return False, f"Opening {overrepresented} exceeds 35% frequency ({max_percentage:.1%})"
        
        return True, ""

    def get_top_openings(self, n: int = 8) -> List[Tuple[str, int]]:
        sorted_openings = sorted(self.opening_frequency.items(), key=lambda x: x[1], reverse=True)
        return sorted_openings[:n]