import torch
import os


def export_to_onnx(network: torch.nn.Module, output_path: str, checkpoint_path: str):
    checkpoint = torch.load(checkpoint_path, map_location='cpu')
    network.load_state_dict(checkpoint['network_state_dict'])
    network.eval()

    dummy_input = torch.zeros(1, 19, 8, 8, dtype=torch.float32)

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

    file_size = os.path.getsize(output_path)
    print(f"ONNX export successful: {output_path}")
    print(f"File size: {file_size / 1024 / 1024:.2f} MB")
    print(f"Input: board_input (shape: [batch_size, 19, 8, 8])")
    print(f"Outputs: policy_logits (shape: [batch_size, 4672]), value (shape: [batch_size])")

    return output_path