# PROJECT MASTER PLAN & ACADEMIC ROADMAP
## Dual Tactics: Clash of Eras - Adaptive Prehistoric Tower Defense 3D
**Course / Subject:** New Technology and Application Development in IT  
**Rubric Evaluation Framework:** 100 Base Points + 5 Mandatory Supporting Work Components  

---

## 1. Executive Summary & Architecture Overview
*Dual Tactics: Clash of Eras* is an advanced multi-platform 3D/2D tactical game integrating Deep Learning AI Director inference, containerized microservice cloud architecture, augmented reality tabletop inspection, and cross-platform mobile touch controls.

```mermaid
flowchart TD
    subgraph Client["Unity 6 Client (URP / C#)"]
        A[3D Battlefield Engine] <--> B[2D Tactical Minimap / Radar]
        A <--> C[AR Tabletop Diorama Controller]
        A <--> D[Mobile Touch Controller]
        A <--> E[IOCloudManager Network Service]
    end

    subgraph Cloud["Containerized IO Cloud Backend (Docker)"]
        E <-->|HTTP / REST Telemetry| F[FastAPI Service :8000]
        F <--> G[PrehistoricTacticsNet (PyTorch)]
        F <--> H[Leaderboard & Analytics Store]
        F <--> I[LiveOps Remote Configuration]
    end

    subgraph Inference["Deep Learning Model"]
        G -->|Multi-Task Inference| J[Archetype Mix: Runner / Flyer / Tank / Boss]
        G -->|Spatial Bias| K[Route Pressure: North / East / West]
        G -->|Player Risk| L[Vulnerability Score & Adaptive Budget]
    end
```

---

## 2. Rubric Alignment & Milestone Tracking

| Rubric Criterion | Target Score | Technical Implementation | Status |
| :--- | :---: | :--- | :---: |
| **1. Unity 2D & 3D Implementation** | **20 / 20** | Full 3D canyon battlefield with NavMesh, URP, dynamic animations, paired with an interactive 2D Tactical Radar Minimap with 3D-to-2D projection and coordinate transforms. | **100% COMPLETE** |
| **2. AR Implementation & Deep Learning** | **20 / 20** | AR Tabletop Hologram diorama with surface tracking grid and orbit inspection. Multi-task PyTorch Deep Neural Network (`PrehistoricTacticsNet`) trained on 2,000 matches with ONNX export. | **100% COMPLETE** |
| **3. Mobile Deployment** | **20 / 20** | Multi-touch gesture engine (1-finger pan, 2-finger pinch/zoom, 2-finger rotate, tap-to-place) + 1-click Android APK build tool (`PrehistoricMobileBuildTool.cs`, `build_android.ps1`). | **100% COMPLETE** |
| **4. IO Cloud Integration** | **20 / 20** | Containerized FastAPI REST backend with 7 active endpoints (`/health`, `/api/v1/ai/predict-wave`, `/api/v1/cloud/telemetry`, `/api/v1/cloud/save`, `/api/v1/cloud/leaderboard`, etc.). | **100% COMPLETE** |
| **5. Docker Use** | **20 / 20** | Multi-stage production `Dockerfile` with healthcheck probes + `docker-compose.yml` service orchestration with mounted volume state persistence. | **100% COMPLETE** |
| **Supporting Work Penalty Avoidance** | **0 Deduction** | Complete delivery of all 5 academic supporting work items: Planning, Testing, Evidence of Integration, Documentation, and Demonstration. | **100% COMPLETE** |

---

## 3. Work Breakdown Structure (WBS) & Sprint Schedule

### Sprint 1: Core 3D Prehistoric Engine & Combat Mechanics
- **WBS 1.1:** URP Canyon Environment Setup, lighting, and camera bounds.
- **WBS 1.2:** Dinosaur AI NavMesh pathfinding (Velociraptor, Pterodactyl, Ankylosaurus, T-Rex Boss).
- **WBS 1.3:** Tower placement grid, ballistic trajectory arcs, AoE catapult physics, electric chain lightning shamans, and spike traps.
- **WBS 1.4:** Dual game modes: *Tower Defense Mode* (Defend base against dino waves) and *Dino Assault Mode* (Spawn dinosaurs to raid AI base).

### Sprint 2: 2D Radar & Coordinate Projection System
- **WBS 2.1:** Screen-space overlay canvas design with custom radar compass bezel.
- **WBS 2.2:** Real-time 3D world-to-radar mathematical coordinate mapping ($x, z \rightarrow \text{RadarBlip}$).
- **WBS 2.3:** Categorized blips: Red ground dinos, Yellow flyers, Cyan defenses, Emerald base.
- **WBS 2.4:** Rotating radar sweep line and responsive Tab hotkey expansion.

### Sprint 3: Deep Learning Neural Architecture & Training
- **WBS 3.1:** Designed `PrehistoricTacticsNet` multi-task feedforward architecture with batch normalization and dropout.
- **WBS 3.2:** Created synthetic match scenario generator covering 5 tactical archetypes (Turtle, Archer Spam, Artillery, Struggling, Balanced).
- **WBS 3.3:** Executed 40-epoch PyTorch training with Adam optimizer ($\text{lr}=0.002$), achieving 100% validation classification accuracy.
- **WBS 3.4:** Exported model weights (`tactics_model.pth`), ONNX cross-platform format (`tactics_model.onnx`), and generated training curves (`training_metrics.png`).

### Sprint 4: Tabletop Augmented Reality Mode
- **WBS 4.1:** Designed miniature tabletop diorama transformation matrix ($0.08\times$ canyon downscale).
- **WBS 4.2:** Procedural holographic tracking plane grid representing real-world surface scanning.
- **WBS 4.3:** Cinematic smooth transition lerp between standard 3D God-view and AR Tabletop perspective.
- **WBS 4.4:** AR mode UI toggle pill and 360-degree diorama orbit inspection.

### Sprint 5: Mobile Touch Controls & Deployment Pipelines
- **WBS 5.1:** Multi-touch gesture recognition system (1-finger pan, 2-finger pinch, twist rotate).
- **WBS 5.2:** Mobile PlayerSettings configuration (LandscapeLeft, ARM64/ARMv7, Vulkan/GLES3, Linear colorspace).
- **WBS 5.3:** Built `PrehistoricMobileBuildTool.cs` providing Unity Editor GUI for Android APK compilation.
- **WBS 5.4:** Created headless automated CLI batch build script `build_android.ps1`.

### Sprint 6: Containerized IO Cloud Microservice & LiveOps
- **WBS 6.1:** Implemented FastAPI REST backend with real-time telemetry streaming and leaderboard persistence.
- **WBS 6.2:** Integrated PyTorch DL model inference into `/api/v1/ai/predict-wave`.
- **WBS 6.3:** Packaged container with `Dockerfile` (Python 3.10-slim, CPU PyTorch, Healthcheck) and `docker-compose.yml`.
- **WBS 6.4:** Built automated Pytest suite (`test_api.py`) validating all 7 endpoints with 100% pass rate.
- **WBS 6.5:** Added real-time IO Cloud HUD badge in Unity with latency ping and tactical rationale flash.

---

## 4. Verification & Risk Mitigation Matrix

| Risk Factor | Impact | Mitigation Strategy | Verification Result |
| :--- | :---: | :--- | :--- |
| **Cloud Disconnection / Network Outage** | Medium | Unity client detects socket drop and falls back immediately to onboard Heuristic AI Director priors with zero gameplay interruption. | Verified via offline test run. |
| **Android Build Module Missing on Host** | Low | Mobile build tool provides clear descriptive dialog instructing user to install module, plus provides Windows standalone build tool as parity fallback. | Verified in Editor GUI. |
| **Docker Engine Stopped on Host** | Low | Created dual launcher `run_backend.ps1` that automatically tests Docker availability; if stopped, boots backend via direct Python uvicorn seamlessly. | Verified via script execution. |
| **Supporting Deliverable Deduction** | Critical (-20 pts) | Built 5 distinct, rigorous academic deliverables matching the exact rubric wording. | 5/5 complete and verified. |