# 🐳 DOCKER USE & IO CLOUD INTEGRATION GUIDE
## Dual Tactics: Clash of Eras - Adaptive Prehistoric Tower Defense 3D

This project includes a **Dockerized Deep Learning AI Director** and **IO Cloud Integration Service** built with **FastAPI** and **PyTorch**.

---

### 1. 🚀 Quickstart: Running with Docker

To build and run the IO Cloud and Deep Learning container:

```bash
# 1. Start the container in background
docker compose up -d --build

# 2. View live logs
docker compose logs -f

# 3. Verify health status
curl http://localhost:8000/health
```

The service will be live at:
- **API Base:** `http://localhost:8000`
- **Interactive Swagger Docs:** `http://localhost:8000/docs`
- **OpenAPI JSON Specification:** `http://localhost:8000/openapi.json`

To stop the container:
```bash
docker compose down
```

---

### 2. 🧠 Deep Learning Model Architecture (`backend/model.py`)

- **Neural Network:** `PrehistoricTacticsNet`
- **Input Dimension:** 12 normalized game telemetry features:
  1. `gold`: Player's current economic power
  2. `base_hp`: Remaining player castle durability
  3. `round`: Current match stage
  4. `watchtowers`: Count of arrow towers
  5. `ballistas`: Count of spear ballistas
  6. `catapults`: Count of AoE catapults
  7. `shamans`: Count of lightning totems
  8. `tarpits`: Count of slowdown tar pools
  9. `spiketraps`: Count of sharp wooden floor spikes
  10. `barricades`: Count of wooden roadblocks
  11. `avg_survival_sec`: Average enemy lifespan before death
  12. `defense_density`: Spatial defense cluster concentration
- **Multi-task Output Heads:**
  1. **Archetype Mix Softmax (4 outputs):** Velociraptor (Runner), Pterodactyl (Flyer), Ankylosaurus (Tank), T-Rex (Boss).
  2. **Route Bias Softmax (3 outputs):** North Canyon, East Flank, West Ridge pressure distribution.
  3. **Vulnerability Index Sigmoid (1 output):** 0.0 to 1.0 indicating weakness in player's layout.

---

### 3. ☁️ IO Cloud Endpoints (`backend/app.py`)

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/health` | Liveness check for Docker & cloud container monitoring |
| `POST` | `/api/v1/ai/predict-wave` | Runs Deep Learning inference to recommend counter-wave compositions |
| `POST` | `/api/v1/cloud/telemetry` | Ingests real-time match telemetry to cloud storage |
| `POST` | `/api/v1/cloud/save` | Persists player progression (unlocked towers, coins) |
| `GET` | `/api/v1/cloud/load/{id}` | Retrieves player save state from cloud |
| `GET` | `/api/v1/cloud/leaderboard` | Returns global rankings and top scores |
| `POST` | `/api/v1/cloud/leaderboard/submit` | Submits a new match score to the global cloud board |
| `GET` | `/api/v1/cloud/config` | Live-Ops remote configuration (event banners, multipliers) |
