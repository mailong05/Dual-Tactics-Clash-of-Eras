"""
Deep Learning Training Pipeline for PrehistoricTacticsNet
Adaptive Tower Defense AI Director
Generates synthetic battle scenarios, trains the multi-task neural network,
evaluates performance, saves model weights (.pth), exports ONNX format (.onnx),
and renders publication-grade training metric curves (.png).
"""

import os
import json
import time
import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import TensorDataset, DataLoader
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

from model import PrehistoricTacticsNet


def generate_synthetic_match_data(num_samples: int = 2000, seed: int = 42):
    """
    Generates balanced, realistic tower defense telemetry and corresponding
    expert counter-strategy labels across diverse player strategies:
    - Trap & Wall Turtle: heavy barricades and spike traps -> Counter with Flyers
    - Archer Tower Spam: heavy single-target arrows -> Counter with Armored Tanks
    - Artillery Catapults: heavy AoE splash -> Counter with Fast Swarm Runners
    - Balanced Defense: mixed setups -> Multi-prong flanking routes
    """
    np.random.seed(seed)
    features = []
    target_archetypes = []
    target_routes = []
    target_vuln = []

    for _ in range(num_samples):
        # Scenario archetype randomly assigned
        playstyle = np.random.choice(["turtle", "archer_spam", "artillery", "balanced", "struggling"])
        round_num = np.random.randint(1, 25)

        if playstyle == "turtle":
            barricades = np.random.randint(3, 7)
            spiketraps = np.random.randint(3, 8)
            tarpits = np.random.randint(2, 6)
            watchtowers = np.random.randint(1, 4)
            catapults = np.random.randint(0, 2)
            shamans = np.random.randint(0, 2)
            ballistas = np.random.randint(0, 2)
            gold = np.random.randint(50, 400)
            base_hp = np.random.randint(70, 100)
            survival = np.random.uniform(9.0, 18.0)
            # Optimal AI counter: Air assault bypasses walls
            arch_dist = [0.15, 0.60, 0.15, 0.10 if round_num >= 5 else 0.0]
            route_dist = [0.2, 0.4, 0.4]
            vuln = 0.35

        elif playstyle == "archer_spam":
            watchtowers = np.random.randint(5, 11)
            ballistas = np.random.randint(2, 6)
            barricades = np.random.randint(0, 2)
            spiketraps = np.random.randint(0, 2)
            tarpits = np.random.randint(0, 2)
            catapults = np.random.randint(0, 2)
            shamans = np.random.randint(0, 2)
            gold = np.random.randint(100, 600)
            base_hp = np.random.randint(60, 100)
            survival = np.random.uniform(7.0, 15.0)
            # Optimal AI counter: Heavy armored tanks absorb single-target arrows
            arch_dist = [0.15, 0.15, 0.60, 0.10 if round_num >= 5 else 0.0]
            route_dist = [0.45, 0.25, 0.30]
            vuln = 0.40

        elif playstyle == "artillery":
            catapults = np.random.randint(2, 5)
            shamans = np.random.randint(2, 5)
            watchtowers = np.random.randint(1, 4)
            ballistas = np.random.randint(0, 3)
            barricades = np.random.randint(1, 3)
            spiketraps = np.random.randint(1, 3)
            tarpits = np.random.randint(1, 3)
            gold = np.random.randint(150, 700)
            base_hp = np.random.randint(65, 100)
            survival = np.random.uniform(8.0, 16.0)
            # Optimal AI counter: Fast swarm runners dodge slow projectile arcs
            arch_dist = [0.55, 0.20, 0.15, 0.10 if round_num >= 5 else 0.0]
            route_dist = [0.30, 0.45, 0.25]
            vuln = 0.45

        elif playstyle == "struggling":
            watchtowers = np.random.randint(0, 3)
            ballistas = 0
            catapults = 0
            shamans = 0
            tarpits = 0
            spiketraps = np.random.randint(0, 2)
            barricades = np.random.randint(0, 2)
            gold = np.random.randint(0, 120)
            base_hp = np.random.randint(15, 55)
            survival = np.random.uniform(3.0, 8.0)
            # Vulnerable: aggressive push
            arch_dist = [0.40, 0.30, 0.20, 0.10 if round_num >= 5 else 0.0]
            route_dist = [0.50, 0.25, 0.25]
            vuln = 0.85

        else: # balanced
            watchtowers = np.random.randint(2, 5)
            ballistas = np.random.randint(1, 3)
            catapults = np.random.randint(1, 2)
            shamans = np.random.randint(1, 2)
            tarpits = np.random.randint(1, 3)
            spiketraps = np.random.randint(1, 3)
            barricades = np.random.randint(1, 3)
            gold = np.random.randint(200, 500)
            base_hp = np.random.randint(75, 100)
            survival = np.random.uniform(10.0, 18.0)
            arch_dist = [0.30, 0.25, 0.35, 0.10 if round_num >= 5 else 0.0]
            route_dist = [0.34, 0.33, 0.33]
            vuln = 0.25

        # Normalize archetype distribution
        arch_dist = np.array(arch_dist, dtype=np.float32)
        arch_dist /= arch_dist.sum()

        route_dist = np.array(route_dist, dtype=np.float32)
        route_dist /= route_dist.sum()

        density = (watchtowers + ballistas + catapults + shamans + barricades + spiketraps + tarpits) / 10.0

        # Construct 12 normalized inputs
        x = [
            min(gold / 1000.0, 1.0),
            min(base_hp / 100.0, 1.0),
            min(round_num / 30.0, 1.0),
            min(watchtowers / 10.0, 1.0),
            min(ballistas / 6.0, 1.0),
            min(catapults / 4.0, 1.0),
            min(shamans / 4.0, 1.0),
            min(tarpits / 8.0, 1.0),
            min(spiketraps / 8.0, 1.0),
            min(barricades / 6.0, 1.0),
            min(survival / 20.0, 1.0),
            min(density / 5.0, 1.0),
        ]

        features.append(x)
        target_archetypes.append(arch_dist)
        target_routes.append(route_dist)
        target_vuln.append([vuln])

    return (
        np.array(features, dtype=np.float32),
        np.array(target_archetypes, dtype=np.float32),
        np.array(target_routes, dtype=np.float32),
        np.array(target_vuln, dtype=np.float32)
    )


def train_model():
    data_dir = os.path.join(os.path.dirname(__file__), "data")
    os.makedirs(data_dir, exist_ok=True)

    print("==================================================================")
    print("   PREHISTORIC TACTICS NET - DEEP LEARNING MODEL TRAINING PIPELINE")
    print("==================================================================")

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"[*] Training Device: {device} (PyTorch {torch.__version__})")

    # 1. Generate Dataset
    print("[*] Generating 2,000 synthetic match scenarios across 5 tactical playstyles...")
    X, Y_arch, Y_route, Y_vuln = generate_synthetic_match_data(num_samples=2000)

    # Train/Val Split (80/20)
    split = int(0.8 * len(X))
    train_x, val_x = torch.tensor(X[:split]), torch.tensor(X[split:])
    train_y_arch, val_y_arch = torch.tensor(Y_arch[:split]), torch.tensor(Y_arch[split:])
    train_y_route, val_y_route = torch.tensor(Y_route[:split]), torch.tensor(Y_route[split:])
    train_y_vuln, val_y_vuln = torch.tensor(Y_vuln[:split]), torch.tensor(Y_vuln[split:])

    train_dataset = TensorDataset(train_x, train_y_arch, train_y_route, train_y_vuln)
    val_dataset = TensorDataset(val_x, val_y_arch, val_y_route, val_y_vuln)

    train_loader = DataLoader(train_dataset, batch_size=32, shuffle=True)
    val_loader = DataLoader(val_dataset, batch_size=64, shuffle=False)

    print(f"[*] Train set: {len(train_dataset)} samples | Val set: {len(val_dataset)} samples")

    # 2. Instantiate Model
    model = PrehistoricTacticsNet(input_dim=12, hidden_dim=64).to(device)
    optimizer = optim.Adam(model.parameters(), lr=0.002, weight_decay=1e-4)
    scheduler = optim.lr_scheduler.ReduceLROnPlateau(optimizer, mode='min', factor=0.5, patience=5)

    criterion_kl = nn.KLDivLoss(reduction='batchmean')
    criterion_mse = nn.MSELoss()

    epochs = 40
    history = {"train_loss": [], "val_loss": [], "archetype_acc": [], "val_archetype_acc": []}

    print(f"[*] Commencing training across {epochs} epochs...")
    start_time = time.time()

    for epoch in range(1, epochs + 1):
        model.train()
        running_loss = 0.0
        correct_arch = 0
        total_samples = 0

        for bx, by_arch, by_route, by_vuln in train_loader:
            bx = bx.to(device)
            by_arch = by_arch.to(device)
            by_route = by_route.to(device)
            by_vuln = by_vuln.to(device)

            optimizer.zero_grad()
            p_arch, p_route, p_vuln = model(bx)

            # Losses
            loss_arch = criterion_kl(torch.log(p_arch + 1e-8), by_arch)
            loss_route = criterion_kl(torch.log(p_route + 1e-8), by_route)
            loss_vuln = criterion_mse(p_vuln, by_vuln)

            total_loss = loss_arch + 0.5 * loss_route + loss_vuln
            total_loss.backward()
            optimizer.step()

            running_loss += total_loss.item() * bx.size(0)
            pred_class = torch.argmax(p_arch, dim=1)
            true_class = torch.argmax(by_arch, dim=1)
            correct_arch += (pred_class == true_class).sum().item()
            total_samples += bx.size(0)

        train_epoch_loss = running_loss / total_samples
        train_arch_acc = (correct_arch / total_samples) * 100.0

        # Validation
        model.eval()
        val_running_loss = 0.0
        val_correct_arch = 0
        val_samples = 0

        with torch.no_grad():
            for bx, by_arch, by_route, by_vuln in val_loader:
                bx = bx.to(device)
                by_arch = by_arch.to(device)
                by_route = by_route.to(device)
                by_vuln = by_vuln.to(device)

                p_arch, p_route, p_vuln = model(bx)

                loss_arch = criterion_kl(torch.log(p_arch + 1e-8), by_arch)
                loss_route = criterion_kl(torch.log(p_route + 1e-8), by_route)
                loss_vuln = criterion_mse(p_vuln, by_vuln)

                val_total_loss = loss_arch + 0.5 * loss_route + loss_vuln
                val_running_loss += val_total_loss.item() * bx.size(0)

                pred_class = torch.argmax(p_arch, dim=1)
                true_class = torch.argmax(by_arch, dim=1)
                val_correct_arch += (pred_class == true_class).sum().item()
                val_samples += bx.size(0)

        val_epoch_loss = val_running_loss / val_samples
        val_arch_acc = (val_correct_arch / val_samples) * 100.0
        scheduler.step(val_epoch_loss)

        history["train_loss"].append(train_epoch_loss)
        history["val_loss"].append(val_epoch_loss)
        history["archetype_acc"].append(train_arch_acc)
        history["val_archetype_acc"].append(val_arch_acc)

        if epoch % 5 == 0 or epoch == 1 or epoch == epochs:
            print(f"Epoch [{epoch:02d}/{epochs}] - Loss: {train_epoch_loss:.4f} (Val: {val_epoch_loss:.4f}) | Arch Acc: {train_arch_acc:.1f}% (Val: {val_arch_acc:.1f}%)")

    elapsed = time.time() - start_time
    print(f"[*] Training completed in {elapsed:.2f} seconds!")

    # 3. Save PyTorch Model Checkpoint
    pth_path = os.path.join(data_dir, "tactics_model.pth")
    torch.save(model.state_dict(), pth_path)
    print(f"[+] Saved PyTorch Model Checkpoint: {pth_path} ({os.path.getsize(pth_path)} bytes)")

    # 4. Export ONNX Model for Cross-Platform / Mobile / Unity Sentis inference
    onnx_path = os.path.join(data_dir, "tactics_model.onnx")
    try:
        model.eval()
        dummy_input = torch.randn(1, 12, dtype=torch.float32).to(device)
        torch.onnx.export(
            model,
            dummy_input,
            onnx_path,
            export_params=True,
            opset_version=14,
            do_constant_folding=True,
            input_names=['game_telemetry_features'],
            output_names=['archetype_distribution', 'route_bias', 'vulnerability_index'],
            dynamic_axes={
                'game_telemetry_features': {0: 'batch_size'},
                'archetype_distribution': {0: 'batch_size'},
                'route_bias': {0: 'batch_size'},
                'vulnerability_index': {0: 'batch_size'}
            }
        )
        print(f"[+] Exported ONNX Neural Network Model: {onnx_path} ({os.path.getsize(onnx_path)} bytes)")
    except Exception as e:
        print(f"[-] ONNX Export note: {e}")

    # 5. Plot Publication-Quality Training Curves
    plt.figure(figsize=(12, 5))

    # Loss subplot
    plt.subplot(1, 2, 1)
    plt.plot(range(1, epochs + 1), history["train_loss"], label="Train Loss (Multi-Task)", color="#1f77b4", linewidth=2)
    plt.plot(range(1, epochs + 1), history["val_loss"], label="Val Loss", color="#ff7f0e", linestyle="--", linewidth=2)
    plt.title("PrehistoricTacticsNet - Loss Convergence", fontsize=12, fontweight='bold')
    plt.xlabel("Epoch")
    plt.ylabel("Multi-Task Loss")
    plt.grid(True, alpha=0.3)
    plt.legend()

    # Accuracy subplot
    plt.subplot(1, 2, 2)
    plt.plot(range(1, epochs + 1), history["archetype_acc"], label="Train Archetype Acc", color="#2ca02c", linewidth=2)
    plt.plot(range(1, epochs + 1), history["val_archetype_acc"], label="Val Archetype Acc", color="#d62728", linestyle="--", linewidth=2)
    plt.title("PrehistoricTacticsNet - Counter-Strategy Accuracy", fontsize=12, fontweight='bold')
    plt.xlabel("Epoch")
    plt.ylabel("Accuracy (%)")
    plt.grid(True, alpha=0.3)
    plt.legend()

    plt.tight_layout()
    chart_path = os.path.join(data_dir, "training_metrics.png")
    plt.savefig(chart_path, dpi=200)
    plt.close()
    print(f"[+] Rendered Publication Training Curves: {chart_path}")

    # 6. Save Training Summary JSON
    summary = {
        "model_name": "PrehistoricTacticsNet",
        "parameters": sum(p.numel() for p in model.parameters()),
        "epochs": epochs,
        "batch_size": 32,
        "training_samples": len(train_dataset),
        "validation_samples": len(val_dataset),
        "training_time_seconds": round(elapsed, 2),
        "device": str(device),
        "final_train_loss": round(history["train_loss"][-1], 4),
        "final_val_loss": round(history["val_loss"][-1], 4),
        "final_val_accuracy_percent": round(history["val_archetype_acc"][-1], 2),
        "pth_checkpoint": pth_path,
        "onnx_export": onnx_path if os.path.exists(onnx_path) else None,
        "chart_path": chart_path
    }
    summary_path = os.path.join(data_dir, "training_summary.json")
    with open(summary_path, "w") as f:
        json.dump(summary, f, indent=2)
    print(f"[+] Saved Training Summary: {summary_path}")

    print("==================================================================")
    print(f"[*] DEEP LEARNING MODEL SUITE READY! Validation Accuracy: {summary['final_val_accuracy_percent']}%")
    print("==================================================================")
    return summary


if __name__ == "__main__":
    train_model()
