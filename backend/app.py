"""
IO Cloud & Deep Learning AI Backend Server
Adaptive Prehistoric Tower Defense 3D
Supports persistent SQLite storage in backend/data/io_cloud.db across container restarts.
"""

import json
import os
import sqlite3
import time
import uuid
from typing import Dict, Any, List
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

from model import ai_director

app = FastAPI(
    title="Dual Tactics: Clash of Eras - IO Cloud & AI Director Service",
    description="Containerized Deep Learning & IO Cloud telemetry backend for Adaptive Prehistoric Tower Defense 3D",
    version="1.1.0"
)

# Enable CORS for Unity clients (Editor, WebGL, Android, Windows)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

DEFAULT_DB_PATH = os.path.join(os.path.dirname(__file__), "data", "io_cloud.db")

SEED_LEADERBOARD = [
    ("player_chief_jurassic", "T-Rex Hunter", 14500, 18, "TowerDefense", 1700000001.0),
    ("player_stone_strategist", "Caveman Prime", 12800, 15, "TowerDefense", 1700000002.0),
    ("player_dino_overlord", "Raptor Queen", 11200, 14, "DinoAssault", 1700000003.0),
    ("player_totem_master", "Shaman Thunder", 9850, 12, "TowerDefense", 1700000004.0),
]


def _get_db_path() -> str:
    return os.environ.get("CLOUD_DB_PATH", DEFAULT_DB_PATH)


def _get_conn() -> sqlite3.Connection:
    db_path = _get_db_path()
    os.makedirs(os.path.dirname(db_path), exist_ok=True)
    conn = sqlite3.connect(db_path, check_same_thread=False)
    conn.row_factory = sqlite3.Row
    return conn


def init_db() -> None:
    """Initializes persistent SQLite tables and seeds default leaderboard if empty."""
    with _get_conn() as conn:
        conn.execute(
            """
            CREATE TABLE IF NOT EXISTS cloud_saves (
                player_id TEXT PRIMARY KEY,
                payload_json TEXT NOT NULL,
                last_saved REAL NOT NULL
            )
            """
        )
        conn.execute(
            """
            CREATE TABLE IF NOT EXISTS telemetry_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                event_id TEXT NOT NULL,
                player_id TEXT NOT NULL,
                round_num INTEGER NOT NULL,
                game_mode TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                logged_at REAL NOT NULL
            )
            """
        )
        conn.execute(
            """
            CREATE TABLE IF NOT EXISTS leaderboard (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                player_id TEXT NOT NULL,
                player_name TEXT NOT NULL,
                score INTEGER NOT NULL,
                wave_reached INTEGER NOT NULL,
                mode TEXT NOT NULL,
                submitted_at REAL NOT NULL
            )
            """
        )
        count = conn.execute("SELECT COUNT(*) FROM leaderboard").fetchone()[0]
        if count == 0:
            conn.executemany(
                """
                INSERT INTO leaderboard (player_id, player_name, score, wave_reached, mode, submitted_at)
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                SEED_LEADERBOARD,
            )
        conn.commit()


init_db()


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
        "persistence": "sqlite",
        "docs_url": "/docs"
    }


@app.get("/health")
def health_check():
    """Liveness probe for Docker container and IO Cloud monitoring"""
    init_db()
    return {
        "status": "healthy",
        "timestamp": time.time(),
        "model_loaded": True,
        "checkpoint_loaded": getattr(ai_director, "checkpoint_loaded", False),
        "device": ai_director.device,
        "storage": "sqlite"
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
    """Ingests battle telemetry into persistent IO Cloud SQLite analytics pipeline"""
    init_db()
    entry = telemetry.model_dump()
    logged_at = time.time()
    event_id = str(uuid.uuid4())
    entry["logged_at"] = logged_at
    entry["event_id"] = event_id

    with _get_conn() as conn:
        conn.execute(
            """
            INSERT INTO telemetry_logs (event_id, player_id, round_num, game_mode, payload_json, logged_at)
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            (event_id, entry["player_id"], entry["round"], entry["game_mode"], json.dumps(entry), logged_at),
        )
        # Keep bounded at most 2000 rows in persistent DB
        conn.execute(
            """
            DELETE FROM telemetry_logs
            WHERE id NOT IN (SELECT id FROM telemetry_logs ORDER BY id DESC LIMIT 2000)
            """
        )
        total_records = conn.execute("SELECT COUNT(*) FROM telemetry_logs").fetchone()[0]
        conn.commit()

    return {
        "status": "logged",
        "event_id": event_id,
        "total_records": total_records
    }


@app.get("/api/v1/cloud/telemetry/summary")
def get_telemetry_summary(limit: int = 20):
    """Returns persisted battle telemetry logs and summary count for demonstration & debugging"""
    init_db()
    with _get_conn() as conn:
        total = conn.execute("SELECT COUNT(*) FROM telemetry_logs").fetchone()[0]
        rows = conn.execute(
            "SELECT payload_json FROM telemetry_logs ORDER BY id DESC LIMIT ?",
            (max(1, min(limit, 100)),),
        ).fetchall()
    recent = [json.loads(r["payload_json"]) for r in rows]
    return {"total_records": total, "recent_logs": recent}


@app.post("/api/v1/cloud/save")
def save_player_state(payload: CloudSavePayload):
    """Saves player game progress persistently to IO Cloud SQLite"""
    init_db()
    data = payload.model_dump()
    last_saved = time.time()
    data["last_saved"] = last_saved

    with _get_conn() as conn:
        conn.execute(
            """
            INSERT INTO cloud_saves (player_id, payload_json, last_saved)
            VALUES (?, ?, ?)
            ON CONFLICT(player_id) DO UPDATE SET
                payload_json = excluded.payload_json,
                last_saved = excluded.last_saved
            """,
            (payload.player_id, json.dumps(data), last_saved),
        )
        conn.commit()

    return {"status": "saved", "player_id": payload.player_id, "timestamp": last_saved}


@app.get("/api/v1/cloud/load/{player_id}")
def load_player_state(player_id: str):
    """Loads player game progress from persistent IO Cloud SQLite"""
    init_db()
    with _get_conn() as conn:
        row = conn.execute(
            "SELECT payload_json FROM cloud_saves WHERE player_id = ?",
            (player_id,),
        ).fetchone()

    if row is not None:
        return json.loads(row["payload_json"])

    default_save = CloudSavePayload(player_id=player_id).model_dump()
    default_save["last_saved"] = time.time()
    return default_save


@app.get("/api/v1/cloud/leaderboard")
def get_leaderboard(limit: int = 10):
    """Returns top scores across all modes from persistent SQLite"""
    init_db()
    with _get_conn() as conn:
        total = conn.execute("SELECT COUNT(*) FROM leaderboard").fetchone()[0]
        rows = conn.execute(
            """
            SELECT player_id, player_name, score, wave_reached, mode, submitted_at
            FROM leaderboard
            ORDER BY score DESC, id DESC
            LIMIT ?
            """,
            (max(1, min(limit, 100)),),
        ).fetchall()

    board = [dict(r) for r in rows]
    return {"leaderboard": board, "count": total}


@app.post("/api/v1/cloud/leaderboard/submit")
def submit_score(submission: ScoreSubmission):
    """Submits a new highscore to persistent global cloud leaderboard"""
    init_db()
    submitted_at = time.time()
    with _get_conn() as conn:
        cur = conn.execute(
            """
            INSERT INTO leaderboard (player_id, player_name, score, wave_reached, mode, submitted_at)
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            (
                submission.player_id,
                submission.player_name,
                submission.score,
                submission.wave_reached,
                submission.mode,
                submitted_at,
            ),
        )
        row_id = cur.lastrowid
        # Compute 1-based rank matching ORDER BY score DESC, id DESC
        rank = conn.execute(
            """
            SELECT COUNT(*) + 1 FROM leaderboard
            WHERE score > ? OR (score = ? AND id > ?)
            """,
            (submission.score, submission.score, row_id),
        ).fetchone()[0]
        conn.commit()

    return {"status": "recorded", "rank": rank}


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
