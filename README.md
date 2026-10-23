# Dual Tactics: Clash of Eras - Adaptive Prehistoric Tower Defense 3D

[![Unity](https://img.shields.io/badge/Unity-6000.6.4f1%20(Unity%206)-blue.svg?logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP-orange.svg)](https://unity.com/srp/universal-render-pipeline)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Android%20%7C%20AR-success.svg)](https://unity.com/)
[![Language](https://img.shields.io/badge/Language-C%23-green.svg?logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)

**Dual Tactics: Clash of Eras** là một tựa game chiến thuật 3D thời gian thực lấy bối cảnh kỷ nguyên tiền sử, được xây dựng trên nền tảng **Unity 6 (Universal Render Pipeline)**. Trò chơi mang đến trải nghiệm đột phá với hệ thống **Chế độ chơi kép (Dual Game Modes)**: Người chơi có thể vừa đóng vai trò người chỉ huy phòng tuyến xây tháp thủ thành, vừa có thể hoán đổi vai trò sang thống lĩnh bầy đàn khủng long tiền sử để công phá các căn cứ làng thổ dân.

---

## 🌟 Tính Năng Nổi Bật

### 1. ⚔️ Hai Chế Độ Chơi Song Hành (Dual Game Modes)
* **Thủ Thành Tiền Sử (Tower Defense):**
  * Xây dựng, bố trí và nâng cấp hệ thống phòng thủ kiên cố bảo vệ Căn Cứ Làng Bộ Lạc.
  * Đối đầu với các đợt tấn công ngày càng hung hãn của bầy quái vật và các Boss khủng long bạo chúa.
* **Công Thành Tiền Sử (Dino Assault / Boss Mode):**
  * Đổi vai trực tiếp: Người chơi trở thành kẻ chỉ huy bầy khủng long tiến công phá hủy căn cứ loài người.
  * Quản lý tài nguyên Thịt (Food) để tính toán triệu hồi quân lực hợp lý theo thời gian thực.
  * Phá vỡ hệ thống tường thành, bẫy chông, chòi canh để san bằng Nhà Làng.

---

### 2. 🦖 Hệ Thống Đơn Vị & Công Trình Đa Dạng
* **Chủng loài khủng long (Dino Units):**
  * **Velociraptor:** Tốc độ di chuyển cực nhanh, di chuyển theo bầy, tấn công cắn xé liên hoàn.
  * **Ankylosaurus (Thiết Giáp):** Giáp dày, lượng máu khổng lồ, chuyên dụng húc đổ cổng thành và hấp thụ sát thương.
  * **Pterodactyl (Thằn Lằn Bay):** Khủng long bay vượt chướng ngại vật địa hình, tấn công chòi canh từ trên cao.
  * **T-Rex Bạo Chúa (Boss Dino):** Kích thước khổng lồ, lượng máu và sát thương hủy diệt mọi công trình phòng thủ.
* **Hệ thống tháp & cạm bẫy phòng ngự (Defenses & Towers):**
  * **Chòi Cung Thủ Gỗ & Tháp Nỏ Khổng Lồ (Ballista):** Tấn công tầm xa đơn mục tiêu với độ chính xác cao.
  * **Máy Bắn Đá Lửa (Catapult):** Bắn đá lửa gây sát thương diện rộng (AoE), phá vỡ các cụm quái vật đông đúc.
  * **Trụ Sấm Sét (Tesla Tower):** Phóng sét dây chuyền gây sát thương điện giật liên hoàn.
  * **Tháp Băng Tiền Sử (Frost Tower):** Làm chậm tốc độ di chuyển của kẻ địch trong vùng ảnh hưởng.
  * **Cạm Bẫy & Công Trình Chắn:** Rào Cọc Gỗ (Plank Defenses), Cổng Thành (Portcullis Barricade), Hố Hắc Ín (Tar Pit), Bẫy Chông Độc (Spike Trap).
  * **Lực lượng Thổ Dân:** Chiến Binh Phóng Giáo và Cung Thủ Làng túc trực tuần tra quanh căn cứ.

---

### 3. 🧠 Trí Tuệ Nhân Tạo & Deep Learning Thích Ứng (AI & Deep Learning Director)
* **Mạng Nơ-ron Đa Nhiệm Chạy On-Device (`PrehistoricNeuralDirector.cs`):**
  * Tích hợp trực tiếp mô hình Deep Learning **PrehistoricTacticsNet** vào Unity C#, suy luận thời gian thực (On-Device Inference) trên CPU/GPU mà không cần phụ thuộc server bên ngoài. Hoạt động 100% offline mượt mà trên cả máy tính và điện thoại Android (độ trễ siêu thấp `< 0.05 ms`).
  * **Kiến trúc mạng (Multi-Task MLP):**
    * **12 Đầu vào (Telemetry Features):** Đo lường vàng, máu căn cứ, tiến trình màn, mật độ tháp đơn mục tiêu, pháo diện rộng, tháp sấm sét, tháp băng, bẫy chông, tường rào, mật độ DPS và chỉ số kỹ năng.
    * **2 Hidden Layers (32 neurons + ReLU):** Trích xuất đặc trưng và liên kết chiến thuật.
    * **4 Đầu ra Đa Nhiệm (Multi-Task Heads):**
      1. *Softmax Archetypes (4 classes):* Tỷ lệ phối hợp quân lực (Runner, Flyer, Tank, Boss).
      2. *Softmax Spatial Route Bias (3 routes):* Định hướng mũi tiến công không gian (Cổng Bắc, Cánh Đông, Sườn Tây).
      3. *Sigmoid Vulnerability:* Đánh giá chỉ số sơ hở của phòng tuyến người chơi.
      4. *Sigmoid Aggression & DDA:* Hệ số thích ứng độ khó thời gian thực.
* **Bảng Giám Sát Trí Tuệ Nhân Tạo XAI (`PrehistoricNeuralMonitorUI.cs`):**
  * Bảng điều khiển Explainable AI (XAI) trực quan hóa hoạt động của mạng nơ-ron bằng phím tắt **`F1`** hoặc nút bấm **`🧠 AI Director`** trên HUD.
  * Hiển thị thanh tiến trình 12 cảm biến trận đấu, đồ thị thanh xác suất Softmax, hướng đánh không gian, hệ số DDA và lời giải thích chiến thuật bằng ngôn ngữ tự nhiên (*Natural Language Tactical Rationale*).
* **Cơ Chế Cân Bằng Độ Khó Động (Dynamic Difficulty Adjustment - DDA):**
  * Tự động đo lường chuỗi thắng, tỷ lệ bảo toàn máu căn cứ và tốc độ diệt quái để phân cấp trình độ người chơi, từ đó điều chỉnh độ hung hãn của bầy quái một cách thông minh và kịch tính.
* **Thuật toán Đột Phá Khe Hẹp (`TacticalBreachMemory`):** Giới hạn tối đa 4 quái vật vượt qua một lối đi cùng lúc, ngăn chặn triệt để tình trạng quái vật dồn cục, kẹt mesh tại các nút thắt cổ chai.
* **Cơ chế Tản Đội Hình Đa Điểm (Flank & Spread Attack):** Khủng long tự động phân tán góc tiếp cận khi bao vây tấn công công trình. Mỗi cá thể cắn đều tính sát thương độc lập trong thời gian thực.
* **Cơ chế Tự Dọn Dẹp Scene Sạch Sẽ (`CleanAllGameplayGarbage`):** Tự động vô hiệu hóa Collider ngay khi quái vật ngã chết để tránh xác làm vật cản tàng hình; tự hủy đạn bay lơ lửng và dọn dẹp các thanh máu mồ côi sau mỗi đợt chơi.

---

### 4. 💾 Hệ Thống Lưu & Tải Game Đa Khe (Multi-Slot Save/Load)
* **3 Khe lưu trữ độc lập** cho mỗi chế độ chơi (Thủ Thành & Công Thành riêng biệt).
* Hỗ trợ lưu tự động (Auto-Save) sau mỗi đợt thắng/thua và lưu thủ công (Manual Save) trực tiếp trong menu Pause.
* Khả năng chọn lưu đè hoặc lưu vào khe trống tương tự các tựa game RPG/Chiến thuật kinh điển.
* Đồng bộ dữ liệu an toàn trên bộ nhớ cục bộ (Local JSON) kèm khả năng đồng bộ đám mây (Cloud Save).

---

### 5. 📱 Đa Nền Tảng & Thực Tế Tăng Cường (AR Mode)
* **PC (Windows):** Điều khiển mượt mà bằng chuột và bàn phím (WASD / Mũi tên / Chuột xoay & zoom camera).
* **Thiết bị di động (Android / iOS):** Giao diện cảm ứng tối ưu, hỗ trợ cử chỉ vuốt pan, xoay và pinch-to-zoom đa điểm.
* **Chế độ AR Tabletop:** Tích hợp Unity AR Foundation, cho phép quét mặt phẳng thực tế và chiếu sa bàn chiến trường 3D ngay trên bàn làm việc của bạn.

---

## 📁 Cấu Trúc Thư Mục Chính

```plaintext
ADAPTIVE TOWER DEFENSE 3D/
├── Assets/
│   ├── LlamAcademy/
│   │   └── Dinos/
│   │       ├── AI/               # TacticalBreachMemory, AdaptiveWaveManager, Swarm Logic
│   │       ├── AR/               # AR Tabletop Controller & AR Surface Trackers
│   │       ├── Audio/            # PrehistoricAudioManager, BGM & Sound FX
│   │       ├── Cloud/            # IOCloudManager & Telemetry Synchronizer
│   │       ├── Config/           # ScriptableObjects cho Tháp và Khủng Long
│   │       ├── Enemy/            # Defender (Chiến binh, Cung thủ thổ dân)
│   │       ├── Player/           # Điều khiển Camera, TowerPlacer, DinoSpawner
│   │       ├── RoundManagement/  # PrehistoricGameplayManager, PrehistoricGameModeManager
│   │       ├── Save/             # PrehistoricSaveManager (Hệ thống Multi-slot Save)
│   │       ├── Scenes/           # Scene chính (Dinos.unity)
│   │       ├── UI/               # Runtime UI, HUD, Modal Thắng/Thua, Giao diện Save/Load
│   │       ├── Unit/             # Các lớp cơ sở Dino, Wall, Towers, Traps, FlyingUnit
│   │       └── Utility/          # HealthBarCanvas, WorldSpace Billboards
│   ├── Resources/                # Prefabs, Animation Controllers, Textures
│   └── Stylized Lava Texture/    # Shader & Texture dòng dung nham tiền sử
├── Packages/                     # Khai báo các Package Unity (URP, Input System, XR/AR)
├── ProjectSettings/              # Cấu hình dự án Unity (Quality, Tags, Layers, Physics)
├── build_game.ps1                # Script PowerShell tự động build bản Windows (.exe)
├── build_android.ps1             # Script PowerShell tự động build bản Android (.apk)
└── README.md                     # Tài liệu giới thiệu và hướng dẫn dự án
```

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
| **Hủy Đặt Tháp** | Chuột phải (`Right Click`) hoặc phím `Esc` | Nhấn nút Hủy trên thanh công cụ |
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
Dự án được phát triển phục vụ mục đích nghiên cứu, học thuật và phát triển công nghệ game chiến thuật thích ứng. Bản quyền thuộc về tác giả và nhóm phát triển.
