"""
IO Cloud & Deep Learning AI Backend Server
Adaptive Prehistoric Tower Defense 3D
Supports persistent SQLite storage in backend/data/io_cloud.db across container restarts.
Includes:
- Cross-Device Player Account Registration & PIN Login (Prehistoric Chief Profiles).
- Persistent Multi-Device Cloud Save & Load.
- Real-Time Battle Telemetry Pipeline for Deep Learning AI Director.
- Global High-Score Leaderboard.
- Modern Responsive Web Dashboard (/dashboard, /web).
- Optional Adafruit IO IoT Bridge.
"""

import hashlib
import json
import os
import sqlite3
import time
import uuid
from typing import Dict, Any, List, Optional
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import HTMLResponse
from pydantic import BaseModel, Field
import requests
import sys

_backend_dir = os.path.dirname(os.path.abspath(__file__))
if _backend_dir not in sys.path:
    sys.path.insert(0, _backend_dir)

from model import ai_director
from dashboard import get_dashboard_html

app = FastAPI(
    title="Dual Tactics: Clash of Eras - IO Cloud & AI Director Service",
    description="Containerized Deep Learning & IO Cloud telemetry backend for Adaptive Prehistoric Tower Defense 3D",
    version="1.2.0"
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


def _hash_pin(pin: str) -> str:
    return hashlib.sha256(pin.strip().encode("utf-8")).hexdigest()


def init_db() -> None:
    """Initializes persistent SQLite tables and seeds default leaderboard and demo account if empty."""
    with _get_conn() as conn:
        conn.execute(
            """
            CREATE TABLE IF NOT EXISTS accounts (
                username TEXT PRIMARY KEY,
                pin_hash TEXT NOT NULL,
                display_name TEXT NOT NULL,
                created_at REAL NOT NULL,
                last_login REAL NOT NULL
            )
            """
        )
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

        # Seed default Chief account if none exists
        acc_count = conn.execute("SELECT COUNT(*) FROM accounts").fetchone()[0]
        if acc_count == 0:
            now = time.time()
            demo_pin = _hash_pin("1234")
            conn.execute(
                """
                INSERT OR IGNORE INTO accounts (username, pin_hash, display_name, created_at, last_login)
                VALUES (?, ?, ?, ?, ?)
                """,
                ("chief_demo", demo_pin, "Tù Trưởng Demo", now, now)
            )
            demo_save = {
                "player_id": "chief_demo",
                "player_name": "Tù Trưởng Demo",
                "unlocked_towers": ["Watchtower", "Barricade", "SpikeTrap", "Ballista"],
                "unlocked_dinos": ["Velociraptor", "Pterodactyl", "Ankylosaurus", "TRex"],
                "coins": 750,
                "highest_wave": 10,
                "selected_mode": "TowerDefense",
                "last_saved": now
            }
            conn.execute(
                """
                INSERT OR IGNORE INTO cloud_saves (player_id, payload_json, last_saved)
                VALUES (?, ?, ?)
                """,
                ("chief_demo", json.dumps(demo_save), now)
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

    # Chế độ Thủ Thành (Tower Defense) độc lập:
    defense_wave: int = 1
    defense_highest_wave: int = 1
    defense_gold: int = 1200
    defense_has_run: bool = True

    # Chế độ Công Thành (Dino Assault) độc lập:
    assault_level: int = 1
    assault_highest_level: int = 1
    assault_meat: int = 500
    assault_has_run: bool = True

    # Dữ liệu các khe lưu (Auto-Save + Khe 1..5)
    slots_json: str = ""


class ScoreSubmission(BaseModel):
    player_id: str
    player_name: str
    score: int
    wave_reached: int
    mode: str = "TowerDefense"


class AccountAuthRequest(BaseModel):
    username: str
    pin: str
    display_name: str = ""


class AdafruitPushPayload(BaseModel):
    aio_username: str
    aio_key: str
    feed_key: str = "wave"
    value: float


# ================== ENDPOINTS ==================

@app.get("/", response_class=HTMLResponse)
@app.get("/dashboard", response_class=HTMLResponse)
@app.get("/web", response_class=HTMLResponse)
def web_dashboard():
    """Serves modern interactive real-time HTML5 dashboard for lecturers & developers"""
    return HTMLResponse(content=get_dashboard_html())


@app.get("/api")
def root_api():
    return {
        "service": "IO Cloud & Deep Learning AI Backend",
        "game": "Dual Tactics: Clash of Eras",
        "status": "online",
        "docker_container": True,
        "persistence": "sqlite",
        "dashboard_url": "/dashboard",
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


# ================== ACCOUNT AUTHENTICATION ==================

@app.post("/api/v1/auth/register")
def register_account(req: AccountAuthRequest):
    """
    Registers a new Prehistoric Chief cloud profile with a PIN code.
    Allows seamlessly synchronizing game progress across PC, Mobile, and web.
    """
    init_db()
    username = req.username.strip().lower()
    if len(username) < 3 or len(username) > 32:
        return {"status": "error", "message": "Tên tài khoản Tù Trưởng phải từ 3 đến 32 ký tự."}
    if len(req.pin.strip()) < 4:
        return {"status": "error", "message": "Mã PIN bảo vệ phải có ít nhất 4 ký tự/chữ số."}

    display_name = req.display_name.strip() or req.username.strip()
    pin_hash = _hash_pin(req.pin)
    now = time.time()

    with _get_conn() as conn:
        existing = conn.execute("SELECT username FROM accounts WHERE username = ?", (username,)).fetchone()
        if existing is not None:
            return {
                "status": "error",
                "message": f"Tài khoản [{username}] đã tồn tại. Vui lòng bấm 'Đăng Nhập' hoặc chọn tên khác."
            }

        conn.execute(
            """
            INSERT INTO accounts (username, pin_hash, display_name, created_at, last_login)
            VALUES (?, ?, ?, ?, ?)
            """,
            (username, pin_hash, display_name, now, now)
        )

        # Seed default cloud save row if none exists
        save_exists = conn.execute("SELECT player_id FROM cloud_saves WHERE player_id = ?", (username,)).fetchone()
        if save_exists is None:
            default_data = {
                "player_id": username,
                "player_name": display_name,
                "unlocked_towers": ["Watchtower", "Barricade", "SpikeTrap", "Ballista"],
                "unlocked_dinos": ["Velociraptor", "Pterodactyl", "Ankylosaurus", "TRex"],
                "coins": 1200,
                "highest_wave": 1,
                "selected_mode": "TowerDefense",
                "defense_wave": 1,
                "defense_highest_wave": 1,
                "defense_gold": 1200,
                "defense_has_run": True,
                "assault_level": 1,
                "assault_highest_level": 1,
                "assault_meat": 500,
                "assault_has_run": True,
                "slots_json": "",
                "last_saved": now
            }
            conn.execute(
                "INSERT INTO cloud_saves (player_id, payload_json, last_saved) VALUES (?, ?, ?)",
                (username, json.dumps(default_data), now)
            )

        conn.commit()

    return {
        "status": "success",
        "message": f"Chúc mừng! Đã tạo thành công tài khoản Tù Trưởng [{username}]!",
        "username": username,
        "display_name": display_name
    }


@app.post("/api/v1/auth/login")
def login_account(req: AccountAuthRequest):
    """
    Logs in to an existing Prehistoric Chief account using username and PIN.
    Returns the player's saved cloud progress for instant synchronization.
    """
    init_db()
    username = req.username.strip().lower()
    pin_hash = _hash_pin(req.pin)
    now = time.time()

    with _get_conn() as conn:
        acc = conn.execute(
            "SELECT username, pin_hash, display_name FROM accounts WHERE username = ?",
            (username,)
        ).fetchone()

        if acc is None:
            return {
                "status": "error",
                "message": f"Không tìm thấy tài khoản [{username}]. Vui lòng bấm 'Tạo Tài Khoản Mới'!"
            }

        if acc["pin_hash"] != pin_hash:
            return {
                "status": "error",
                "message": "Mã PIN bảo vệ không chính xác. Vui lòng kiểm tra lại!"
            }

        conn.execute("UPDATE accounts SET last_login = ? WHERE username = ?", (now, username))

        # Fetch saved cloud progress
        save_row = conn.execute(
            "SELECT payload_json FROM cloud_saves WHERE player_id = ?",
            (username,)
        ).fetchone()

        save_data = json.loads(save_row["payload_json"]) if save_row else None
        conn.commit()

    return {
        "status": "success",
        "message": f"Đăng nhập thành công! Chào mừng Tù Trưởng {acc['display_name']}.",
        "username": username,
        "display_name": acc["display_name"],
        "save_data": save_data
    }


@app.get("/api/v1/cloud/accounts")
def list_cloud_accounts():
    """Lists registered cloud accounts and latest synced progression for the Web Dashboard"""
    init_db()
    with _get_conn() as conn:
        total = conn.execute("SELECT COUNT(*) FROM accounts").fetchone()[0]
        rows = conn.execute(
            """
            SELECT a.username, a.display_name, a.created_at, a.last_login, s.payload_json
            FROM accounts a
            LEFT JOIN cloud_saves s ON a.username = s.player_id
            ORDER BY a.last_login DESC
            LIMIT 50
            """
        ).fetchall()

    accounts = []
    for r in rows:
        save_info = json.loads(r["payload_json"]) if r["payload_json"] else {}
        accounts.append({
            "username": r["username"],
            "display_name": r["display_name"],
            "created_at": r["created_at"],
            "last_login": r["last_login"],
            "highest_wave": save_info.get("highest_wave", 1),
            "coins": save_info.get("coins", 500),
            "selected_mode": save_info.get("selected_mode", "TowerDefense"),
            "last_saved": save_info.get("last_saved", r["last_login"])
        })

    return {"count": total, "accounts": accounts}


# ================== AI INFERENCE & TELEMETRY ==================

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


# ================== ADAFRUIT IO IOT BRIDGE ==================

@app.post("/api/v1/cloud/adafruit/push")
def push_to_adafruit(payload: AdafruitPushPayload):
    """
    Optional IoT telemetry bridge to Adafruit IO (https://io.adafruit.com).
    Publishes real-time game telemetry (e.g. current wave, highest score) to user's Adafruit feed.
    """
    url = f"https://io.adafruit.com/api/v2/{payload.aio_username}/feeds/{payload.feed_key}/data"
    headers = {
        "X-AIO-Key": payload.aio_key,
        "Content-Type": "application/json"
    }
    try:
        resp = requests.post(url, json={"value": payload.value}, headers=headers, timeout=5.0)
        return {
            "status": "success" if resp.status_code < 300 else "error",
            "adafruit_status": resp.status_code,
            "response": resp.json() if resp.status_code < 300 else resp.text
        }
    except Exception as e:
        return {"status": "error", "message": str(e)}
