"""
Comprehensive Automated Integration Test Suite
IO Cloud & Deep Learning AI Backend Server
Tests all 7 FastAPI endpoints, model inference, multi-task outputs, and data persistence.
"""

import os
import sys
import pytest
from fastapi.testclient import TestClient

# Add backend directory to python path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from app import app
from model import ai_director

client = TestClient(app)


def test_root_endpoint():
    """Verify service root metadata and online status"""
    response = client.get("/")
    assert response.status_code == 200
    data = response.json()
    assert data["service"] == "IO Cloud & Deep Learning AI Backend"
    assert data["status"] == "online"
    assert data["docker_container"] is True
    assert data["docs_url"] == "/docs"


def test_health_check_and_model_liveness():
    """Verify container health probe and PyTorch model loading"""
    response = client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "healthy"
    assert data["model_loaded"] is True
    assert data["checkpoint_loaded"] is True
    assert "device" in data
    assert "timestamp" in data


def test_predict_wave_inference_schema():
    """Verify Deep Learning model multi-task prediction structure and probabilities"""
    telemetry = {
        "player_id": "test_player_alpha",
        "round": 4,
        "gold": 250,
        "base_hp": 85,
        "game_mode": "TowerDefense",
        "watchtowers": 3,
        "ballistas": 1,
        "catapults": 1,
        "shamans": 0,
        "tarpits": 1,
        "spiketraps": 2,
        "barricades": 2,
        "avg_survival_sec": 9.2,
        "defense_density": 1.5
    }
    response = client.post("/api/v1/ai/predict-wave", json=telemetry)
    assert response.status_code == 200
    data = response.json()

    assert "model_type" in data
    assert "archetype_weights" in data
    assert "route_bias" in data
    assert "vulnerability_score" in data
    assert "recommended_wave_size" in data
    assert "tactical_rationale" in data

    # Verify probability distributions sum to ~1.0
    archetypes = data["archetype_weights"]
    arch_sum = sum(archetypes.values())
    assert abs(arch_sum - 1.0) < 0.05, f"Archetype weights sum to {arch_sum}, expected ~1.0"
    assert archetypes["Velociraptor_Runner"] >= 0.0
    assert archetypes["Pterodactyl_Flyer"] >= 0.0
    assert archetypes["Ankylosaurus_Tank"] >= 0.0

    routes = data["route_bias"]
    route_sum = sum(routes.values())
    assert abs(route_sum - 1.0) < 0.05, f"Route bias sums to {route_sum}, expected ~1.0"

    assert 0.0 <= data["vulnerability_score"] <= 1.0
    assert data["recommended_wave_size"] >= 6


def test_adaptive_counter_mechanisms():
    """Verify that the AI Director specifically counters player defense choices"""
    # 1. Barricade turtle strategy -> should trigger aerial flyers
    turtle_telemetry = {
        "player_id": "turtle_player",
        "round": 3,
        "gold": 100,
        "base_hp": 100,
        "watchtowers": 0,
        "barricades": 4,
        "spiketraps": 4
    }
    resp_turtle = client.post("/api/v1/ai/predict-wave", json=turtle_telemetry).json()
    assert resp_turtle["archetype_weights"]["Pterodactyl_Flyer"] > 0.25
    assert "aerial Pterodactyl" in resp_turtle["tactical_rationale"] or "bypass" in resp_turtle["tactical_rationale"]

    # 2. Archer tower spam -> should deploy armored tanks (Ankylosaurus)
    archer_telemetry = {
        "player_id": "archer_player",
        "round": 3,
        "gold": 50,
        "base_hp": 100,
        "watchtowers": 5,
        "ballistas": 2,
        "barricades": 0,
        "spiketraps": 0
    }
    resp_archer = client.post("/api/v1/ai/predict-wave", json=archer_telemetry).json()
    assert resp_archer["archetype_weights"]["Ankylosaurus_Tank"] > 0.25
    assert "Ankylosaurus" in resp_archer["tactical_rationale"] or "arrow" in resp_archer["tactical_rationale"]

    # 3. Boss deployment in round 5/8/10
    boss_telemetry = {
        "player_id": "boss_target",
        "round": 8,
        "gold": 300,
        "base_hp": 90
    }
    resp_boss = client.post("/api/v1/ai/predict-wave", json=boss_telemetry).json()
    assert resp_boss["archetype_weights"]["TRex_Boss"] > 0.20
    assert "T-Rex" in resp_boss["tactical_rationale"]


def test_telemetry_stream_logging():
    """Verify real-time game telemetry logging into IO Cloud analytics"""
    telemetry = {
        "player_id": "stream_test_user",
        "round": 2,
        "gold": 180,
        "base_hp": 95,
        "game_mode": "TowerDefense"
    }
    response = client.post("/api/v1/cloud/telemetry", json=telemetry)
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "logged"
    assert "event_id" in data
    assert data["total_records"] >= 1


def test_cloud_save_and_load_lifecycle():
    """Verify full cloud save & restore cycle for cross-platform progression"""
    test_player = "player_test_integration_999"
    save_payload = {
        "player_id": test_player,
        "player_name": "Jurassico Master",
        "unlocked_towers": ["Watchtower", "Catapult", "TeslaTower"],
        "unlocked_dinos": ["Velociraptor", "Pterodactyl", "Ankylosaurus"],
        "coins": 1250,
        "highest_wave": 9,
        "selected_mode": "DinoAssault"
    }

    # Save to cloud
    resp_save = client.post("/api/v1/cloud/save", json=save_payload)
    assert resp_save.status_code == 200
    assert resp_save.json()["status"] == "saved"
    assert resp_save.json()["player_id"] == test_player

    # Load back from cloud
    resp_load = client.get(f"/api/v1/cloud/load/{test_player}")
    assert resp_load.status_code == 200
    loaded_data = resp_load.json()
    assert loaded_data["player_id"] == test_player
    assert loaded_data["player_name"] == "Jurassico Master"
    assert loaded_data["coins"] == 1250
    assert loaded_data["highest_wave"] == 9
    assert "TeslaTower" in loaded_data["unlocked_towers"]


def test_leaderboard_submission_and_retrieval():
    """Verify competitive leaderboard ranking and submission"""
    submission = {
        "player_id": "challenger_007",
        "player_name": "Apex Predator",
        "score": 99999,
        "wave_reached": 25,
        "mode": "TowerDefense"
    }
    resp_sub = client.post("/api/v1/cloud/leaderboard/submit", json=submission)
    assert resp_sub.status_code == 200
    assert resp_sub.json()["status"] == "recorded"
    assert resp_sub.json()["rank"] == 1  # Highest score in leaderboard

    # Retrieve top 5
    resp_board = client.get("/api/v1/cloud/leaderboard?limit=5")
    assert resp_board.status_code == 200
    board = resp_board.json()
    assert board["count"] >= 1
    top_player = board["leaderboard"][0]
    assert top_player["player_name"] == "Apex Predator"
    assert top_player["score"] == 99999


def test_remote_liveops_config():
    """Verify live-ops remote configuration sync to game client"""
    response = client.get("/api/v1/cloud/config")
    assert response.status_code == 200
    data = response.json()
    assert "event_name" in data
    assert "gold_multiplier" in data
    assert "dino_speed_multiplier" in data
    assert "boss_health_multiplier" in data
    assert data["gold_multiplier"] > 0


def test_onnx_model_file_integrity():
    """Verify that ONNX export model exists and passes structural validation"""
    import onnx
    onnx_path = os.path.join(os.path.dirname(__file__), "..", "data", "tactics_model.onnx")
    assert os.path.exists(onnx_path), f"ONNX model missing at {onnx_path}"

    model_proto = onnx.load(onnx_path)
    onnx.checker.check_model(model_proto)
    assert len(model_proto.graph.input) == 1
    assert len(model_proto.graph.output) == 3
