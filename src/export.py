import torch
import os
import logging

logger = logging.getLogger("ChessAI.export")


def export_to_onnx(network: torch.nn.Module, output_path: str, checkpoint_path: str):
    try:
        logger.info(f"Starting ONNX export: network={type(network).__name__}, "
                   f"output_path={output_path}, checkpoint_path={checkpoint_path}")
        
        # Validate inputs
        if not os.path.exists(checkpoint_path):
            error_msg = f"Checkpoint file not found: {checkpoint_path}"
            logger.error(error_msg)
            raise FileNotFoundError(error_msg)
            
        logger.debug(f"Loading checkpoint from {checkpoint_path}")
        checkpoint = torch.load(checkpoint_path, map_location='cpu')
        logger.debug("Checkpoint loaded successfully")
        
        logger.debug("Loading network state dict")
        network.load_state_dict(checkpoint['network_state_dict'])
        logger.debug("Network state dict loaded successfully")
        
        logger.debug("Setting network to evaluation mode")
        network.eval()
        logger.debug("Network set to evaluation mode")

        logger.debug("Creating dummy input for ONNX export")
        dummy_input = torch.zeros(1, 19, 8, 8, dtype=torch.float32)
        logger.debug(f"Dummy input created with shape: {dummy_input.shape}")

        logger.debug("Running torch.onnx.export")
        torch.onnx.export(
            network,
            dummy_input,
            output_path,
            export_params=True,
            opset_version=17,
            input_names=["board_input"],
            output_names=["policy_logits", "value"],
            dynamic_axes={"board_input": {0: "batch_size"}}
        )
        logger.info(f"ONNX export completed successfully: {output_path}")

        logger.debug(f"Checking file size of exported model: {output_path}")
        if not os.path.exists(output_path):
            error_msg = f"Exported ONNX file not found: {output_path}"
            logger.error(error_msg)
            raise FileNotFoundError(error_msg)
            
        file_size = os.path.getsize(output_path)
        logger.info(f"ONNX export successful: {output_path}")
        logger.info(f"File size: {file_size / 1024 / 1024:.2f} MB")
        logger.info(f"Input: board_input (shape: [batch_size, 19, 8, 8])")
        logger.info(f"Outputs: policy_logits (shape: [batch_size, 4672]), value (shape: [batch_size])")
        
        print(f"ONNX export successful: {output_path}")
        print(f"File size: {file_size / 1024 / 1024:.2f} MB")
        print(f"Input: board_input (shape: [batch_size, 19, 8, 8])")
        print(f"Outputs: policy_logits (shape: [batch_size, 4672]), value (shape: [batch_size])")

        return output_path
    except FileNotFoundError as e:
        logger.error(f"File not found error during ONNX export: {e}")
        raise
    except Exception as e:
        logger.error(f"Fatal error during ONNX export: {e}", exc_info=True)
        raise