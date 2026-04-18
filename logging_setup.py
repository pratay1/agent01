import logging
import os
from datetime import datetime

def setup_logging(log_dir=None):
    """Setup comprehensive logging for the application"""
    if log_dir is None:
        log_dir = os.path.join(os.path.dirname(__file__), "logs")
    
    # Create logs directory if it doesn't exist
    os.makedirs(log_dir, exist_ok=True)
    
    # Create log filename with timestamp
    log_filename = os.path.join(log_dir, f"chessai_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log")
    
    # Configure logging
    logging.basicConfig(
        level=logging.DEBUG,
        format='%(asctime)s - %(name)s - %(levelname)s - %(funcName)s:%(lineno)d - %(message)s',
        handlers=[
            logging.FileHandler(log_filename, encoding='utf-8'),
            logging.StreamHandler()  # Also output to console
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