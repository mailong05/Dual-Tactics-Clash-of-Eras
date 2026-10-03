# PROJECT SPECIFICATION & ROADMAP: ADAPTIVE TOWER DEFENSE 3D

## 1. Project Overview & Core Concept
- **Project Name:** Adaptive Tower Defense 3D
- **Target Engine:** Unity 6 (Universal Render Pipeline - URP)
- **Target Audience / Purpose:** Student graduation project optimized for low-spec laptops (stable 60+ FPS, lightweight CPU/GPU footprint).
- **Core Unique Selling Point (USP):** An **AI Director** that analyzes player tower placement and previous wave performance (damage taken per route, destruction rates) to dynamically adjust future waves (routing, unit mix) in real-time.

---

## 2. Expanded Scope & Features
1. **Maps (3 Distinct Environments):**
   - *Map 1 (The Canyon):* Basic two parallel routes, open space, ideal for core testing.
   - *Map 2 (The Crossroads):* Intersecting pathways allowing cross-fire and dual-route management.
   - *Map 3 (The Labyrinth):* Winding paths with natural obstacles limiting long-range tower lines-of-sight.
2. **Towers (4 Strategic Types):**
   - *Gatling Tower:* Fast fire rate, low single-target damage (counters swarms/fast runners).
   - *Cannon Tower:* Slow fire rate, Area of Effect (AoE) damage (counters grouped clusters).
   - *Laser/Sniper Tower:* Extremely high single-target damage, long range (counters Bosses/Tanks).
   - *Slow/Freeze Tower:* Low damage, applies slow debuff within radius.
3. **Enemies (4 Archetypes):**
   - *Runner:* Low health, high speed.
   - *Tank:* High health, slow speed.
   - *Shielded:* Absorbs the first instance of heavy burst damage.
   - *Boss:* High health pool, minor resistance to slow effects, appears in advanced waves.

---

## 3. C# Script Architecture (Folder & Class Structure)
Instruct your AI agent to structure the code strictly within these folders:

```text
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs       // Controls game states (Play, Pause, Win, Lose, Wave transition)
│   │   └── ResourceManager.cs   // Manages player currency/gold/energy
│   ├── Grid/
│   │   ├── GridManager.cs       // Manages buildable grid slots / nodes
│   │   └── Node.cs              // Individual cell data (occupied status, terrain type)
│   ├── Towers/
│   │   ├── TowerBase.cs         // Base class for all towers (range check, target locking, firing)
│   │   ├── GatlingTower.cs      // Inherits TowerBase
│   │   ├── CannonTower.cs       // Inherits TowerBase
│   │   └── Projectile.cs        // Handles bullet motion, collision, and damage application
│   ├── Enemies/
│   │   ├── EnemyBase.cs         // Base class for enemies (health, speed, damage reception, NavMesh movement)
│   │   ├── EnemyHealth.cs       // Manages HP bar and status debuffs (Slow, Armor)
│   │   └── EnemySpawner.cs      // Spawns units based on AI Director instructions
│   ├── AI_Director/
│   │   ├── AIDirector.cs        // Core AI: Gathers metrics, scores utility, decides next wave configuration
│   │   └── WaveData.cs          // ScriptableObject defining enemy composition per wave
│   └── UI/
│       ├── UIManager.cs         // Handles HUD (currency, base health, tower selection menu)
│       └── AIDebugPanel.cs      // Visualizes AI Director thinking / routing heatmaps for demoing
```

---

## 4. Step-by-Step Implementation Roadmap for AI Agent

### Phase 1: Core Foundation & Grid
- **Tasks:** Set up the main scene, write `GameManager` (currencies, base HP). Implement raycasting/mouse clicking on buildable tiles via `GridManager`.
- **Deliverable:** Player can click a grid slot and spawn a basic placeholder tower cylinder.

### Phase 2: Navigation & Enemies
- **Tasks:** Bake Unity NavMesh on Map 1. Implement `EnemyBase` allowing enemies to move autonomously from Spawn points to the Base using `NavMeshAgent`. Add basic `EnemyHealth`.
- **Deliverable:** Enemies spawn and march toward the base, deducting player life upon arrival.

### Phase 3: Combat Loop & Towers
- **Tasks:** Write `TowerBase` using `Physics.OverlapSphere` to detect enemies in range. Implement projectile movement and damage scripts. Populate the 4 tower types.
- **Deliverable:** Towers automatically target, rotate toward, and destroy incoming enemies.

### Phase 4: Maps & Multi-Enemies Expansion
- **Tasks:** Configure Maps 2 and 3 with multi-route setups. Create ScriptableObject profiles for `WaveData` and code the distinct enemy archetypes (Runner, Tank, Shielded).
- **Deliverable:** Game supports multiple selectable maps and varied enemy waves.

### Phase 5: AI Director Integration (The Core Feature)
- **Tasks:** Implement `AIDirector.cs`. 
  - Track wave metrics: `damageDealtPerRoute` and `towerTypeDistribution`.
  - Use simple utility scoring to evaluate player blind spots.
  - Dynamically alter subsequent wave spawn ratios and route allocations.
  - Build `AIDebugPanel` UI to display decision states during live presentation.
- **Deliverable:** If the player over-defends Route A, the AI shifts subsequent waves to Route B or spawns hard-counter units.

### Phase 6: Polish, UI/UX & Optimization
- **Tasks:** Design clean UI menus, add basic particle effects (muzzle flash, explosions), integrate lightweight sound effects (SFX), and profile for low-spec laptop performance.