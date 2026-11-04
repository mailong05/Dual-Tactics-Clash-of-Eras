"""
IO Cloud & Deep Learning AI Backend Server
Adaptive Prehistoric Tower Defense 3D
"""

import time
import uuid
from typing import Dict, Any, List, Optional
from fastapi import FastAPI, HTTPException, status
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

from model import ai_director

app = FastAPI(
    title="Dual Tactics: Clash of Eras - IO Cloud & AI Director Service",
    description="Containerized Deep Learning & IO Cloud telemetry backend for Adaptive Prehistoric Tower Defense 3D",
    version="1.0.0"
)

# Enable CORS for Unity clients (Editor, WebGL, Android, Windows)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# In-memory Cloud Database (Persists during container lifecycle)
CLOUD_SAVES: Dict[str, Dict[str, Any]] = {}
TELEMETRY_LOGS: List[Dict[str, Any]] = []
LEADERBOARD: List[Dict[str, Any]] = [
    {"player_id": "player_chief_jurassic", "player_name": "T-Rex Hunter", "score": 14500, "wave_reached": 18, "mode": "TowerDefense"},
    {"player_id": "player_stone_strategist", "player_name": "Caveman Prime", "score": 12800, "wave_reached": 15, "mode": "TowerDefense"},
    {"player_id": "player_dino_overlord", "player_name": "Raptor Queen", "score": 11200, "wave_reached": 14, "mode": "DinoAssault"},
    {"player_id": "player_totem_master", "player_name": "Shaman Thunder", "score": 9850, "wave_reached": 12, "mode": "TowerDefense"}
]

# Pydantic Schemas
class GameTelemetry(BaseModel):
    player_id: str = "guest_player"
    round: int = Field(default=1, ge=1)
    gold: int = Field(default=100, ge=0)
    base_hp: int = Field(default=100, ge=0)
    game_mode: str = "TowerDefense"
    watchtowers: int = 0
    ballistas: int = 0
    catapults: int = 0
    shamans: int = 0
    tarpits: int = 0
    spiketraps: int = 0
    barricades: int = 0
    avg_survival_sec: float = 8.0
    defense_density: float = 1.0


class CloudSavePayload(BaseModel):
    player_id: str
    player_name: str = "Prehistoric Player"
    unlocked_towers: List[str] = ["Watchtower", "Barricade", "SpikeTrap"]
    unlocked_dinos: List[str] = ["Velociraptor", "Pterodactyl"]
    coins: int = 500
    highest_wave: int = 1
    selected_mode: str = "TowerDefense"


class ScoreSubmission(BaseModel):
    player_id: str
    player_name: str
    score: int
    wave_reached: int
    mode: str = "TowerDefense"


# ================== ENDPOINTS ==================

@app.get("/")
def root():
    return {
        "service": "IO Cloud & Deep Learning AI Backend",
        "game": "Dual Tactics: Clash of Eras",
        "status": "online",
        "docker_container": True,
        "docs_url": "/docs"
    }


@app.get("/health")
def health_check():
    """Liveness probe for Docker container and IO Cloud monitoring"""
    return {
        "status": "healthy",
        "timestamp": time.time(),
        "model_loaded": True,
        "checkpoint_loaded": getattr(ai_director, "checkpoint_loaded", False),
        "device": ai_director.device
    }


@app.post("/api/v1/ai/predict-wave")
def predict_adaptive_wave(telemetry: GameTelemetry):
    """
    Core Deep Learning Endpoint:
    Receives current player game state, processes it through PrehistoricTacticsNet neural network,
    and returns dynamically adapted dinosaur composition and route pressure weights.
    """
    data = telemetry.model_dump()
    prediction = ai_director.predict(data)
    prediction["request_timestamp"] = time.time()
    return prediction


@app.post("/api/v1/cloud/telemetry")
def record_telemetry(telemetry: GameTelemetry):
    """Ingests battle telemetry into IO Cloud analytics pipeline"""
    entry = telemetry.model_dump()
    entry["logged_at"] = time.time()
    entry["event_id"] = str(uuid.uuid4())
    TELEMETRY_LOGS.append(entry)

    # Keep memory bounded
    if len(TELEMETRY_LOGS) > 1000:
        TELEMETRY_LOGS.pop(0)

    return {
        "status": "logged",
        "event_id": entry["event_id"],
        "total_records": len(TELEMETRY_LOGS)
    }


@app.post("/api/v1/cloud/save")
def save_player_state(payload: CloudSavePayload):
    """Saves player game progress to IO Cloud"""
    data = payload.model_dump()
    data["last_saved"] = time.time()
    CLOUD_SAVES[payload.player_id] = data
    return {"status": "saved", "player_id": payload.player_id, "timestamp": data["last_saved"]}


@app.get("/api/v1/cloud/load/{player_id}")
def load_player_state(player_id: str):
    """Loads player game progress from IO Cloud"""
    if player_id in CLOUD_SAVES:
        return CLOUD_SAVES[player_id]

    # Return default initialized save
    default_save = CloudSavePayload(player_id=player_id).model_dump()
    default_save["last_saved"] = time.time()
    return default_save


@app.get("/api/v1/cloud/leaderboard")
def get_leaderboard(limit: int = 10):
    """Returns top scores across all modes"""
    sorted_board = sorted(LEADERBOARD, key=lambda x: x["score"], reverse=True)
    return {"leaderboard": sorted_board[:limit], "count": len(sorted_board)}


@app.post("/api/v1/cloud/leaderboard/submit")
def submit_score(submission: ScoreSubmission):
    """Submits a new highscore to global cloud leaderboard"""
    entry = submission.model_dump()
    entry["submitted_at"] = time.time()
    LEADERBOARD.append(entry)
    LEADERBOARD.sort(key=lambda x: x["score"], reverse=True)
    return {"status": "recorded", "rank": LEADERBOARD.index(entry) + 1}


@app.get("/api/v1/cloud/config")
def get_remote_config():
    """Live-Ops remote configuration values synced to Unity client"""
    return {
        "event_name": "Jurassic Awakening (Season 1)",
        "gold_multiplier": 1.0,
        "dino_speed_multiplier": 1.0,
        "boss_health_multiplier": 1.0,
        "server_message": "Welcome to Dual Tactics: Clash of Eras! Powered by IO Cloud & Deep Learning AI."
    }
