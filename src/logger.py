import logging
import os
import traceback
from datetime import datetime
from typing import Optional

class TrainingLogger:
    def __init__(self, log_dir: str = "logs"):
        self.log_dir = log_dir
        self.setup_loggers()
        
    def setup_loggers(self):
        # Create logs directory if it doesn't exist
        os.makedirs(self.log_dir, exist_ok=True)
        
        # Setup main application logger
        self.main_logger = logging.getLogger('chess_ai_main')
        self.main_logger.setLevel(logging.DEBUG)
        
        # Setup error logger
        self.error_logger = logging.getLogger('chess_ai_error')
        self.error_logger.setLevel(logging.ERROR)
        
        # Setup training logger
        self.training_logger = logging.getLogger('chess_ai_training')
        self.training_logger.setLevel(logging.INFO)
        
        # Create formatters
        detailed_formatter = logging.Formatter(
            '%(asctime)s - %(name)s - %(levelname)s - %(filename)s:%(lineno)d - %(message)s'
        )
        
        simple_formatter = logging.Formatter(
            '%(asctime)s - %(levelname)s - %(message)s'
        )
        
        # File handlers
        main_handler = logging.FileHandler(
            os.path.join(self.log_dir, 'main.log'), 
            encoding='utf-8'
        )
        main_handler.setFormatter(detailed_formatter)
        self.main_logger.addHandler(main_handler)
        
        error_handler = logging.FileHandler(
            os.path.join(self.log_dir, 'error.log'), 
            encoding='utf-8'
        )
        error_handler.setFormatter(detailed_formatter)
        self.error_logger.addHandler(error_handler)
        
        training_handler = logging.FileHandler(
            os.path.join(self.log_dir, 'training.log'), 
            encoding='utf-8'
        )
        training_handler.setFormatter(simple_formatter)
        self.training_logger.addHandler(training_handler)
        
        # Console handler for errors
        console_handler = logging.StreamHandler()
        console_handler.setLevel(logging.WARNING)
        console_handler.setFormatter(simple_formatter)
        self.main_logger.addHandler(console_handler)
        
    def log_info(self, message: str, component: str = "main"):
        if component == "training":
            self.training_logger.info(message)
        else:
            self.main_logger.info(message)
            
    def log_error(self, message: str, exc_info=None, component: str = "main"):
        self.error_logger.error(message, exc_info=exc_info)
        self.main_logger.error(message, exc_info=exc_info)
        
    def log_warning(self, message: str, component: str = "main"):
        self.main_logger.warning(message)
        
    def log_debug(self, message: str, component: str = "main"):
        self.main_logger.debug(message)
        
    def log_training_step(self, epoch: int, losses: dict, additional_info: dict = None):
        msg = f"Epoch {epoch} - Policy Loss: {losses.get('policy_loss', 0):.6f}, "
        msg += f"Value Loss: {losses.get('value_loss', 0):.6f}, "
        msg += f"Combined Loss: {losses.get('combined_loss', 0):.6f}"
        
        if additional_info:
            for key, value in additional_info.items():
                msg += f", {key}: {value}"
                
        self.training_logger.info(msg)
        
    def log_exception(self, e: Exception, context: str = ""):
        error_msg = f"Exception in {context}: {str(e)}\n"
        error_msg += f"Traceback: {traceback.format_exc()}"
        self.error_logger.error(error_msg)
        self.main_logger.error(error_msg)

# Global logger instance
logger = TrainingLogger()

def get_logger():
    return logger