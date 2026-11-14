# Dual Tactics: Clash of Eras - Adaptive Prehistoric Tower Defense 3D

[![Unity](https://img.shields.io/badge/Unity-6000.6.4f1%20(Unity%206)-blue.svg?logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP-orange.svg)](https://unity.com/srp/universal-render-pipeline)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Android%20%7C%20AR-success.svg)](https://unity.com/)
[![Language](https://img.shields.io/badge/Language-C%23-green.svg?logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)

**Dual Tactics: Clash of Eras** là tựa game chiến thuật 3D thời gian thực lấy bối cảnh kỷ nguyên tiền sử, được xây dựng trên nền tảng **Unity 6 (Universal Render Pipeline)**. Trò chơi mang đến trải nghiệm đột phá với hệ thống **Chế độ chơi kép (Dual Game Modes)**: Người chơi có thể vừa đóng vai trò người chỉ huy phòng tuyến xây tháp thủ thành, vừa có thể hoán đổi vai trò sang thống lĩnh bầy đàn khủng long tiền sử để công phá các căn cứ làng thổ dân.

---

## 🌟 Tính Năng Nổi Bật

### 1. ⚔️ Hai Chế Độ Chơi Song Hành (Dual Game Modes)
* **Thủ Thành Tiền Sử (Tower Defense):**
  * Xây dựng, bố trí và nâng cấp hệ thống phòng thủ kiên cố bảo vệ Căn Cứ Làng Bộ Lạc.
  * Đối đầu với các đợt tấn công ngày càng hung hãn của bầy quái vật và các Boss khủng long bạo chúa với AI thích ứng.
* **Công Thành Tiền Sử (Dino Assault / Boss Mode):**
  * Đổi vai trực tiếp: Người chơi trở thành kẻ chỉ huy bầy khủng long tiến công phá hủy căn cứ loài người.
  * Quản lý tài nguyên Thịt (Food) để tính toán triệu hồi quân lực hợp lý theo thời gian thực (Velociraptor, Khủng Long Thiết Giáp, Thằn Lằn Bay, T-Rex Bạo Chúa).
  * Phá vỡ hệ thống rào cọc, tường gỗ độc lập, cạm bẫy và tiêu diệt thổ dân để san bằng Nhà Làng.
* **Tiến Trình Màn Chơi Độc Lập (Isolated Campaign Progression):**
  * Tiến trình mở khóa map được phân tách hoàn toàn độc lập giữa chế độ Thủ Thành và Công Thành. Khi hoàn thành Map 1 ở chế độ Công Thành để mở khóa Map 2, Map 2 của chế độ Thủ Thành vẫn được bảo toàn trạng thái khóa, đảm bảo tính toàn vẹn thử thách của từng chế độ.

---

### 2. 🗺️ Hệ Thống Bản Đồ Chiến Trường & Đột Biến Môi Trường (Themed Maps & Mutations)
* **Map 1: Thung Lũng Tiền Sử (Prehistoric Valley):**
  * Rừng rậm nhiệt đới nguyên sinh, thảm thực vật rậm rạp và hệ thống thành lũy gỗ sơ khai của bộ lạc cổ.
* **Map 2: Hẻm Núi Dung Nham (Volcanic Gorge):**
  * Địa hình hẻm núi dung nham nóng chảy rực lửa, áp dụng cơ chế đột biến sinh học độc quyền (`PrehistoricVolcanicMutant`):
    * **🌋 Magma Velociraptor:** Tốc độ di chuyển tăng vượt trội; khi ngã xuống kích hoạt phát nổ dung nham diện rộng (*AOE Lava Death Explosion*) gây sát thương cho tháp và quân phòng thủ xung quanh.
    * **🔥 Inferno Pterodactyl:** Thần lằn bay hỏa ngục thả bom dung nham nổ tung (*Magma Bomb Drop*) thiêu đốt phòng tuyến khi bay tuần tiễu bên trên.
    * **🛡️ Obsidian Ankylosaurus:** Vỏ giáp bazan hắc diệu thạch siêu bền vững, chống chịu lượng sát thương cực đại và phá hủy rào chắn.
    * **👑🌋 Bạo Chúa T-Rex Nham Thạch:** Thể hình khổng lồ, phóng sóng xung kích địa chấn (*Volcanic Shockwave*) làm choáng và đẩy lùi toàn bộ phòng tuyến người chơi.
    * **Tro tàn lửa lơ lửng (`Volcanic_Ember_Aura`):** Hiệu ứng hạt tro tàn bốc lên từ thân thể khủng long; vật liệu URP chuẩn hóa hiển thị sắc nét từng đường vân vảy, răng nanh và đổ bóng 3D chân thực (không bị cháy sáng hay ám màu silhouette).
* **Map 3: Lãnh Địa Băng Giá (Glacial Tundra):**
  * Đồng tuyết vĩnh cửu với gió tuyết khắc nghiệt và thử thách phòng thủ trên nền địa hình băng giá.

---

### 3. 🧠 Trí Tuệ Nhân Tạo & Deep Learning Thích Ứng (AI & Deep Learning Director)
* **Mạng Nơ-ron Đa Nhiệm Chạy On-Device (`PrehistoricNeuralDirector.cs`):**
  * Tích hợp trực tiếp mô hình Deep Learning **PrehistoricTacticsNet** vào Unity C#, suy luận thời gian thực (On-Device Inference) trên CPU/GPU không phụ thuộc server bên ngoài. Hoạt động 100% offline mượt mà trên PC và Android (`< 0.05 ms`).
  * **12 Đầu vào (Telemetry Features):** Đo lường vàng, máu căn cứ, tiến trình màn, mật độ tháp đơn mục tiêu, pháo diện rộng, tháp sấm sét, tháp băng, bẫy chông, tường rào, mật độ DPS và chỉ số kỹ năng.
  * **4 Đầu ra Đa Nhiệm (Multi-Task Heads):**
    1. *Softmax Archetypes (4 classes):* Tỷ lệ phối hợp quân lực (Runner, Flyer, Tank, Boss).
    2. *Softmax Spatial Route Bias (3 routes):* Định hướng mũi tiến công không gian (Cổng Bắc, Cánh Đông, Sườn Tây).
    3. *Sigmoid Vulnerability:* Đánh giá chỉ số sơ hở của phòng tuyến người chơi.
    4. *Sigmoid Aggression & DDA:* Hệ số thích ứng độ khó thời gian thực.
* **Thuật Toán Tìm Đường Né Đe Dọa A\* (`ThreatAwarePathfinding.cs`):**
  * Khủng long tự động tính toán trường chi phí đe dọa (*Threat Cost Field*) từ tầm bắn của tháp và NPC phòng thủ.
  * Đánh giá cân bằng giữa cự ly ngắn nhất và mức độ an toàn để chủ động chọn hành lang an toàn (*Safe Corridors*) hoặc bọc sườn (*Flanking*) né tránh hỏa lực tập trung.
  * Giải thuật điều hướng thông minh loại bỏ hoàn toàn các góc kẹt địa hình chân núi và vách đá.
* **Bản Đồ Nhiệt Nguy Hiểm Thời Gian Thực (`ThreatHeatmapVisualizer.cs`):**
  * Bật/tắt trực tiếp bằng phím tắt **`H`** hoặc nút bấm trên thanh công cụ UI.
  * Trực quan hóa mật độ đe dọa bằng lưới màu động trên địa hình 3D:
    * 🟢 **Xanh lá:** Vùng an toàn / Điểm mù của tháp canh.
    * 🟡 **Vàng:** Vùng đe dọa trung bình.
    * 🔴 **Đỏ:** Khu vực hỏa lực hủy diệt (Kill Zone).
* **Bảng Giám Sát Trí Tuệ Nhân Tạo XAI (`PrehistoricNeuralMonitorUI.cs`):**
  * Phím tắt **`F1`** hoặc nút **`🧠 AI Director`** trên HUD để theo dõi 12 cảm biến trận đấu, xác suất Softmax và lời giải thích chiến thuật bằng ngôn ngữ tự nhiên (*Natural Language Tactical Rationale*).

---

### 4. 🏹 Cân Bằng Chiến Đấu & Cơ Chế Va Chạm Thực Tế
* **Giới Hạn Tầm Đánh Thực Tế (Anti-Snipe Defense Balancing):**
  * Chuẩn hóa bán kính giao chiến của thổ dân bảo vệ làng (Cung thủ: 14.0m, Chiến binh phóng giáo: 9.5m), loại bỏ hoàn toàn hiện tượng tấn công siêu tầm xa xuyên bản đồ.
  * Cân bằng tầm quét mục tiêu của Tháp Nỏ, Tháp Cung, Máy Bắn Đá và Trụ Sấm Sét, mở ra không gian chiến thuật hợp lý cho khủng long luồn lách né tháp.
* **Sát Thương Cận Chiến Chuẩn Xác:**
  * Khủng long bắt buộc phải tiếp cận đúng cự ly mục tiêu ($\le 3.2\text{m}$) mới có thể tung đòn cắn/quật đuôi, xóa bỏ triệt để hiện tượng gây sát thương ảo từ xa.
* **Hệ Thống Phân Đoạn Tường Gỗ Độc Lập:**
  * Hàng rào gỗ và cổng thành được chia tách thành các phân đoạn vật lý độc lập. Mỗi phân đoạn sở hữu lượng máu riêng và thanh máu trực quan; khi một đoạn tường bị tấn công sẽ chỉ trừ máu của đoạn đó, không làm sập dây chuyền các đoạn khác.
* **Tự Động Thu Hồi Rác Scene (`CleanAllGameplayGarbage`):**
  * Vô hiệu hóa Collider ngay khi đơn vị ngã chết để tránh xác cản đường; tự hủy đạn bay lơ lửng và dọn dẹp các thanh máu mồ côi khi kết thúc đợt chơi.

---

### 5. 💾 Hệ Thống Lưu & Tải Game Đa Khe (Multi-Slot Save/Load)
* **3 Khe lưu trữ độc lập** cho mỗi chế độ chơi (Thủ Thành & Công Thành riêng biệt).
* Hỗ trợ lưu tự động (Auto-Save) sau mỗi đợt thắng/thua và lưu thủ công (Manual Save) trực tiếp trong menu Pause.
* Khả năng chọn lưu đè hoặc lưu vào khe trống tương tự các tựa game RPG/Chiến thuật kinh điển.
* Đồng bộ dữ liệu an toàn trên bộ nhớ cục bộ (Local JSON) kèm khả năng mở rộng đồng bộ đám mây (Cloud Save).

---

### 6. 📱 Đa Nền Tảng & Thực Tế Tăng Cường (AR Mode)
* **PC (Windows):** Điều khiển mượt mà bằng chuột và bàn phím (WASD / Mũi tên / Chuột xoay & zoom camera).
* **Thiết bị di động (Android / iOS):** Giao diện cảm ứng tối ưu, hỗ trợ cử chỉ vuốt pan, xoay và pinch-to-zoom đa điểm.
* **Chế độ AR Tabletop (Hybrid ARCore + Desktop Pass-Through):** Tích hợp **Unity AR Foundation 6.6.2 + ARCore 6.6.2** trên Android (quét mặt phẳng 6-DOF, chạm mặt bàn đặt sa bàn Tap-to-Place thật bằng cách co giãn `XROrigin` giữ nguyên NavMesh $1\times$), kết hợp **Camera Video Pass-Through (`WebCamTexture`)** dự phòng trên PC Webcam, Đế Sa Bàn Đá Tiền Sử (`ARDioramaFrame` $68 \times 72\text{m}$) và Shader `ARShadowMatte` chiếu bóng đổ thực tế.
* **Điện Toán Đám Mây IO Cloud (HTTPS Railway + Docker Local):** Máy chủ FastAPI + PyTorch triển khai sẵn HTTPS tại `https://io-cloud-ai-backend-production.up.railway.app` (kèm volume SQLite bền vững) và có thể chạy local bằng Docker Compose trên cổng `8000`.

---

## 📁 Cấu Trúc Thư Mục Chính

```plaintext
ADAPTIVE TOWER DEFENSE 3D/
├── Assets/
│   ├── LlamAcademy/
│   │   └── Dinos/
│   │       ├── AI/               # ThreatAwarePathfinding, PrehistoricNeuralDirector, Heatmap
│   │       ├── AR/               # AR Tabletop Controller & AR Surface Trackers
│   │       ├── Audio/            # PrehistoricAudioManager, BGM & Sound FX
│   │       ├── Cloud/            # IOCloudManager & Telemetry Synchronizer
│   │       ├── Config/           # ScriptableObjects cho Tháp và Khủng Long
│   │       ├── Enemy/            # Defender (Chiến binh, Cung thủ thổ dân)
│   │       ├── Environment/      # PrehistoricVolcanicMutant (Hệ thống đột biến map 2)
│   │       ├── Player/           # Điều khiển Camera, TowerPlacer, DinoSpawner
│   │       ├── RoundManagement/  # PrehistoricGameplayManager, PrehistoricGameModeManager
│   │       ├── Save/             # PrehistoricSaveManager (Multi-slot Save độc lập)
│   │       ├── Scenes/           # Dinos.unity, Dinos_VolcanicGorge.unity, Dinos_GlacialTundra.unity
│   │       ├── UI/               # Runtime UI, HUD, Heatmap Button, XAI Monitor, Save/Load
│   │       ├── Unit/             # Các lớp cơ sở Dino, Wall, Towers, Traps, FlyingUnit
│   │       └── Utility/          # HealthBarCanvas, WorldSpace Billboards
│   ├── Resources/                # Prefabs, Animation Controllers, Textures, Materials
│   └── Stylized Lava Texture/    # Shader & Texture dòng dung nham tiền sử
├── Packages/                     # Khai báo các Package Unity (URP, Input System, XR/AR)
├── ProjectSettings/              # Cấu hình dự án Unity (Quality, Tags, Layers, Physics)
├── build_game.ps1                # Script PowerShell tự động build bản Windows (.exe)
├── build_android.ps1             # Script PowerShell tự động build bản Android (.apk)
└── README.md                     # Tài liệu giới thiệu và hướng dẫn dự án
```

---

## 🛠️ Nhật Ký Cải Tiến Kỹ Thuật Mới Nhất

| Hạng Mục | Nội Dung Cải Tiến & Khắc Phục | Trạng Thái |
| :--- | :--- | :---: |
| **Volcanic Visual Fix** | Xóa bỏ vòng lặp ép HDR Emission toàn thân trong `PrehistoricVolcanicMutant.cs`, khắc phục triệt để lỗi T-Rex và Raptor biến thành khối vàng neon silhouette. Tái tạo màu da cam nham thạch tự nhiên, giữ trọn vẹn 100% texture vân vảy và đổ bóng 3D. | ✅ Hoàn thành |
| **Scene PPtr Integrity** | Sửa dứt điểm lỗi `Broken text PPtr (Local file identifier doesn't exist)` trên cả 3 bản đồ (`Dinos_VolcanicGorge.unity`, `Dinos.unity`, `Dinos_GlacialTundra.unity`). Phục hồi đầy đủ component `Image` của các thanh máu tường rào. | ✅ Hoàn thành |
| **Particle Velocity Mode** | Sửa lỗi `Particle Velocity curves must all be in the same mode` bằng cách đồng bộ 3 trục tọa độ của `velocityOverLifetime` sang chế độ `TwoConstants`. | ✅ Hoàn thành |
| **Threat-Aware Pathfinding** | Tích hợp thuật toán né tháp A* theo bản đồ nhiệt đe dọa (`ThreatAwarePathfinding`), xử lý mượt mà các góc kẹt vách núi sườn Đông. | ✅ Hoàn thành |
| **Realtime Threat Heatmap** | Tích hợp công cụ trực quan hóa bản đồ nhiệt đe dọa thời gian thực trên địa hình (`ThreatHeatmapVisualizer`) bật/tắt bằng phím `H`. | ✅ Hoàn thành |
| **Combat Reach & Anti-Snipe** | Giới hạn tầm bắn của NPC thổ dân (14m Cung thủ, 9.5m Phóng giáo) và tháp canh; ràng buộc cự ly tấn công cận chiến của khủng long $\le 3.2\text{m}$. | ✅ Hoàn thành |
| **Independent Progression** | Phân tách độc lập tiến trình mở khóa map giữa 2 chế độ Thủ Thành và Công Thành qua `PrehistoricSaveManager`. | ✅ Hoàn thành |
| **Front Wall Health Bars** | Tách riêng các phân đoạn tường rào gỗ trước thành, hỗ trợ hiển thị thanh máu riêng biệt cho từng khối rào khi bị tấn công. | ✅ Hoàn thành |

---

## 🚀 Hướng Dẫn Cài Đặt & Chạy Dự Án

### Yêu Cầu Hệ Thống
* **Unity Version:** Unity `6000.6.4f1` (Unity 6) trở lên.
* **Render Pipeline:** Universal Render Pipeline (URP).
* **Target Platforms:** Windows Standalone (x64) hoặc Android (API Level 24+, ARM64 / IL2CPP).

### Mở Dự Án Bằng Unity Editor
1. Khởi động **Unity Hub**.
2. Chọn **Add** > **Add project from disk** và trỏ đến thư mục `ADAPTIVE TOWER DEFENSE 3D`.
3. Đảm bảo chọn phiên bản **Unity 6000.6.4f1**.
4. Mở Scene chính tại đường dẫn:
   ```
   Assets/LlamAcademy/Dinos/Scenes/Dinos.unity
   ```
5. Nhấn nút **Play** (`Ctrl + P`) trên thanh công cụ Unity để bắt đầu trải nghiệm!

---

## 🎮 Thao Tác Điều Khiển

| Thao Tác | Trên Máy Tính (PC) | Trên Điện Thoại (Mobile) |
| :--- | :--- | :--- |
| **Di chuyển Camera** | Phím `W, A, S, D` hoặc kéo chuột phải | Vuốt 1 ngón tay trên màn hình |
| **Xoay Camera** | Giữ chuột giữa / chuột phải và di chuyển | Vuốt 2 ngón tay xoay tròn |
| **Thu Phóng Camera** | Lăn con trỏ chuột (`Scroll Wheel`) | Dùng 2 ngón tay chụm / mở (`Pinch-to-zoom`) |
| **Chọn & Đặt Tháp / Quái** | Chuột trái (`Left Click`) | Chạm ngón tay vào ô giao diện / vị trí đất |
| **Phím Nhanh Thả Quái (Assault)** | Phím số `[1]` Raptor, `[2]` Bay, `[3]` Thiết Giáp, `[4]` T-Rex | Chạm vào thẻ quái trên thanh triệu hồi |
| **Bản Đồ Nhiệt Nguy Hiểm (Heatmap)** | Phím tắt **`H`** hoặc nút bấm Heatmap trên UI | Chạm biểu tượng Heatmap trên thanh điều khiển |
| **Bảng Giám Sát AI Deep Learning** | Phím tắt **`F1`** hoặc nút `🧠 AI Director` | Chạm biểu tượng AI Director trên HUD |
| **Menu Tạm Dừng / Lưu Game** | Nhấn phím `Esc` hoặc bấm biểu tượng Menu góc trái | Bấm biểu tượng Menu góc trên bên trái |

---

## 🔨 Hướng Dẫn Đóng Gói (Build)

### Build Bản Windows (.EXE)
Bạn có thể build nhanh thông qua script PowerShell có sẵn:
```powershell
.\build_game.ps1
```
*File thực thi sẽ được xuất tại thư mục:* `Builds/Windows/AdaptiveTowerDefense3D.exe`.

### Build Bản Android (.APK)
Chạy script PowerShell hỗ trợ đóng gói tự động cho thiết bị Android:
```powershell
.\build_android.ps1
```
*Lưu ý: Yêu cầu máy đã cấu hình đầy đủ Android SDK & NDK trong Unity Preferences.*

---

## 📜 Giấy Phép (License)
Dự án được phát triển phục vụ mục đích nghiên cứu, học thuật và phát triển công nghệ game chiến thuật thích ứng. Ý tưởng được lấy từ nhiều nguồn. Dự án chỉ để tham khảo và thử sức. Thân!
