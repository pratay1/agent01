import logging
import os
from datetime import datetime

def setup_logging(log_dir=None, level=logging.INFO):
    """Setup comprehensive logging for the application"""
    if log_dir is None:
        log_dir = os.path.join(os.path.dirname(__file__), "logs")
    
    # Create logs directory if it doesn't exist
    os.makedirs(log_dir, exist_ok=True)
    
    # Create log filename with timestamp
    log_filename = os.path.join(log_dir, f"chessai_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log")
    
    # Configure logging with specified level
    logging.basicConfig(
        level=level,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        handlers=[
            logging.FileHandler(log_filename, encoding='utf-8')
        ]
    )
    
    # Create logger
    logger = logging.getLogger("ChessAI")
    logger.info(f"Logging initialized. Log file: {log_filename}")
    return logger

def get_logger(name=None):
    """Get a logger instance"""
    if name:
        return logging.getLogger(f"ChessAI.{name}")
    return logging.getLogger("ChessAI")