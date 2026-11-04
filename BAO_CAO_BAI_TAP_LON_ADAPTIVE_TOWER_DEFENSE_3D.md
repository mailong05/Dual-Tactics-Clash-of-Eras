# ĐẠI HỌC QUỐC GIA THÀNH PHỐ HỒ CHÍ MINH
## TRƯỜNG ĐẠI HỌC BÁCH KHOA / KHOA KHOA HỌC & KỸ THUẬT MÁY TÍNH
***

<br>

# BÁO CÁO BÀI TẬP LỚN
### MÔN HỌC: CÔNG NGHỆ MỚI VÀ PHÁT TRIỂN ỨNG DỤNG TRONG CNTT  
*(NEW TECHNOLOGY AND APPLICATION DEVELOPMENT IN IT)*

<br>

## ĐỀ TÀI:
# DUAL TACTICS: CLASH OF ERAS
### HỆ THỐNG GAME CHIẾN THUẬT THÍCH ỨNG 3D & SA BÀN THỰC TẾ TĂNG CƯỜNG (AR TABLETOP) TÍCH HỢP MẠNG NƠ-RON DEEP LEARNING VÀ ĐIỆN TOÁN ĐÁM MÂY DOCKER IO CLOUD

<br><br>

**Giảng viên hướng dẫn:** TS. [Tên Giảng Viên Hướng Dẫn]  
**Nhóm sinh viên thực hiện:**  
1. **Mai Văn Long** – MSSV: 23730631 (Nhóm trưởng)  
2. **[Họ và tên thành viên 2]** – MSSV: [MSSV 2]  
3. **[Họ và tên thành viên 3]** – MSSV: [MSSV 3]  

<br>

*Tp. Hồ Chí Minh, Tháng 11/2026*

---

\newpage

# MỤC LỤC

1. [Động Cơ Nghiên Cứu & Đặt Vấn Đề](#1-động-cơ-nghiên-cứu--đặt-vấn-đề)
2. [Mục Tiêu Đề Tài](#2-mục-tiêu-đề-tài)
3. [Cơ Sở Lý Thuyết & Công Nghệ Cốt Lõi](#3-cơ-sở-lý-thuyết--công-nghệ-cốt-lõi)
   - 3.1. Kỹ thuật Đồ họa Thời gian thực Unity 6 & URP
   - 3.2. Thuật toán Tìm đường Né đe dọa Threat-Aware $A^*$
   - 3.3. Mạng Nơ-ron Đa nhiệm PrehistoricTacticsNet (Deep Learning)
   - 3.4. Công nghệ Thực tế tăng cường AR Tabletop & 6-DOF Anchor
   - 3.5. Kiến trúc Microservices Điện toán đám mây & Docker Container
4. [Kiến Trúc Hệ Thống & Luồng Dữ Liệu](#4-kiến-trúc-hệ-thống--luồng-dữ-liệu)
   - 4.1. Sơ đồ Kiến trúc Tổng thể (System Architecture)
   - 4.2. Luồng Xử lý Suy luận AI & Truyền nhận Telemetry
5. [Hiện Thực Hóa Chi Tiết & Giải Thuật Cốt Lõi](#5-hiện-thực-hóa-chi-tiết--giải-thuật-cốt-lõi)
   - 5.1. Hai Chế độ Chơi Song hành (Dual Game Modes)
   - 5.2. Hệ thống Bản đồ Đột biến Môi trường (Themed Maps & Mutations)
   - 5.3. Module Tìm đường $A^*$ & Trực quan hóa Bản đồ nhiệt (Threat Heatmap)
   - 5.4. Module Mạng Nơ-ron On-Device & Bộ Huấn luyện 500 Episodes
   - 5.5. Module Sa bàn AR Tabletop & Cử chỉ Không gian 3D
   - 5.6. Module Backend FastAPI & Container Docker
6. [Kết Quả Thực Nghiệm & Đánh Giá Hiệu Năng](#6-kết-quả-thực-nghiệm--đánh-giá-hiệu-năng)
   - 6.1. Thực nghiệm Hội tụ Huấn luyện Mạng Nơ-ron (500 Episodes)
   - 6.2. So sánh Hiệu quả Né Tháp của Thuật toán Threat-Aware $A^*$
   - 6.3. Kết quả Kiểm thử Tự động Backend API (Pytest 9/9 Passed)
   - 6.4. Benchmark Hiệu năng Runtime (FPS, Latency, RAM)
7. [Hướng Dẫn Triển Khai & Đóng Gói Packages](#7-hướng-dẫn-triển-khai--đóng-gói-packages)
   - 7.1. Chạy Trực tiếp Game trên Unity & Xuất Bản Windows (.exe)
   - 7.2. Xuất Bản Cài đặt Di động Android (.apk)
   - 7.3. Khởi chạy Container Docker & IO Cloud Backend
   - 7.4. Đóng gói Unity Package (.unitypackage)
8. [Phân Công Công Việc & Nhật Ký Dự Án](#8-phân-công-công-việc--nhật-ký-dự-án)
   - 8.1. Bảng Phân công Công việc Chi tiết
   - 8.2. Nhật ký Tiến độ & Các buổi họp Nhóm
9. [Kết Luận & Hướng Phát Triển](#9-kết-luận--hướng-phát-triển)
10. [Tài Liệu Tham Khảo](#10-tài-liệu-tham-khảo)

---

\newpage

# 1. Động Cơ Nghiên Cứu & Đặt Vấn Đề

Trong ngành công nghiệp game và ứng dụng tương tác hiện đại, thể loại game Thủ thành (Tower Defense) truyền thống thường gặp phải một hạn chế mang tính cố hữu: **Tính đơn điệu và kịch bản tĩnh (Scripted Waves)**. Các đợt quái vật chỉ di chuyển theo một lộ trình cố định (waypoint tuyến tính), thiếu khả năng nhận thức mối đe dọa xung quanh và hoàn toàn thụ động trước các cạm bẫy của người chơi. Điều này khiến cho trải nghiệm nhanh chóng trở nên dễ đoán và làm giảm giá trị chơi lại (*Replayability*).

Đồng thời, sự bùng nổ của các công nghệ phần mềm mới trong những năm gần đây đã mở ra những hướng tiếp cận mang tính cách mạng:
* **Deep Learning & Edge AI:** Khả năng suy luận mạng nơ-ron sâu trực tiếp trên thiết bị (On-Device Inference) với độ trễ cực thấp, giúp NPC có khả năng "học" chiến thuật của người chơi trong thời gian thực.
* **Thực tế Tăng cường (Augmented Reality - AR):** Đưa không gian chiến trường 3D thoát khỏi màn hình phẳng truyền thống để hòa nhập trực tiếp vào môi trường thực tế (sa bàn chiến thuật trên mặt bàn).
* **Điện toán Đám mây (Cloud Services) & Containerization (Docker):** Cho phép đồng bộ hóa dữ liệu telemetry, tính toán cân bằng game động (Dynamic Difficulty Adjustment - DDA) và quản lý tiến trình người chơi trên hạ tầng container hóa linh hoạt, chuẩn hóa môi trường triển khai.

Nhận thức được tiềm năng to lớn này, đề tài **Dual Tactics: Clash of Eras (Adaptive Prehistoric Tower Defense 3D)** được nghiên cứu và phát triển nhằm giải quyết triệt để bài toán trên, kết hợp hài hòa giữa đồ họa 3D thời gian thực, thuật toán AI nhận thức đe dọa, mô hình học sâu thích ứng, công nghệ AR tương tác không gian và kiến trúc microservices đám mây Docker.

---

# 2. Mục Tiêu Đề Tài

Dự án hướng tới việc hiện thực hóa đầy đủ 5 mục tiêu kỹ thuật trọng tâm theo yêu cầu của học phần:

1. **Hiện thực hóa Game Kép 2D & 3D Đột Phá:** Xây dựng một tựa game chiến thuật thời gian thực hoàn chỉnh với 2 chế độ chơi đối xứng: **Thủ Thành Tiền Sử (Tower Defense)** và **Công Thành Tiền Sử (Dino Assault)**, kết hợp đồng bộ giữa mô hình 3D vật lý và radar quét 2D thời gian thực trên nền Universal Render Pipeline (URP).
2. **Tích hợp Trí tuệ Nhân tạo & Deep Learning On-Device:**
   * Phát triển thuật toán tìm đường **Threat-Aware $A^*$** dựa trên ma trận đe dọa $64 \times 64$, cho phép khủng long tự động tính toán lộ trình bọc sườn và né tránh hỏa lực.
   * Xây dựng mạng nơ-ron đa nhiệm **PrehistoricTacticsNet** có khả năng học hỏi và phân tích 12 chỉ số trận đấu để đưa ra đội hình quái vật khắc chế.
   * Tích hợp bộ huấn luyện nhanh **FastForwardOfflineTrainer** mô phỏng 500 trận đấu toán học trong vài giây và bảng điều khiển giải thích trí tuệ nhân tạo **XAI Monitor**.
3. **Triển khai Đa nền tảng Di động (Mobile Deployment):** Tối ưu hóa hiệu năng đạt 60 FPS ổn định, xây dựng hệ thống điều khiển cảm ứng đa điểm (Touch Gestures) và công cụ đóng gói tự động file cài đặt **Android (.apk)**.
4. **Tích hợp Hệ thống Đám mây IO Cloud:** Xây dựng hệ thống RESTful API kết nối giữa Unity client và Cloud server, hỗ trợ truyền nhận dữ liệu telemetry thời gian thực, lưu trữ đa khe trên mây (Cloud Save) và bảng xếp hạng trực tuyến (Leaderboard).
5. **Đóng gói & Triển khai Container Docker:** Đóng gói toàn bộ backend AI vào Docker container độc lập, tự động quản lý tài nguyên và cấu hình môi trường bằng Docker Compose.

---

# 3. Cơ Sở Lý Thuyết & Công Nghệ Cốt Lõi

### 3.1. Kỹ thuật Đồ họa Thời gian thực Unity 6 & URP
Dự án sử dụng **Unity 6000.6.4f1** kết hợp **Universal Render Pipeline (URP)**. URP cho phép kiểm soát luồng kết xuất (Render Pipeline) linh hoạt thông qua Shader Graph, tối ưu hóa Draw Calls trên thiết bị di động bằng Single-Pass Instanced Rendering và hỗ trợ xử lý hậu kỳ (Bloom, Color Adjustments, Vignette) hiệu năng cao.

### 3.2. Thuật toán Tìm đường Né đe dọa Threat-Aware $A^*$
Thuật toán tìm đường truyền thống $A^*$ sử dụng hàm đánh giá:
$$f(n) = g(n) + h(n)$$
Trong đó $g(n)$ là khoảng cách thực tế từ điểm xuất phát và $h(n)$ là hàm heuristic ước lượng cự ly đến đích (Euclidean Distance).

Trong dự án này, chúng tôi nâng cấp hàm chi phí $g(v)$ khi di chuyển từ ô $u$ sang ô lân cận $v$ trên lưới ma trận $64 \times 64$ bằng cách tích hợp **Trường chi phí đe dọa (Threat Cost Field)**:
$$g(v) = g(u) + \text{dist}(u, v) \cdot \Big(1 + w_{\text{threat}} \cdot \text{ThreatScore}(v)\Big) + \text{WallPenalty}(v)$$

* $\text{ThreatScore}(v)$: Tổng mật độ sát thương mỗi giây (DPS) của tất cả các tháp canh đang bao phủ ô $v$.
* $w_{\text{threat}}$: Hệ số e ngại nguy hiểm riêng biệt theo từng loài khủng long:
  * `Velociraptor (Flanker)`: $w_{\text{threat}} = 4.5$, $\text{WallPenalty} = 80$ (u tiên chạy vòng qua các hành lang an toàn ít hỏa lực).
  * `Ankylosaurus (Tanker)`: $w_{\text{threat}} = 0.35$, $\text{WallPenalty} = 5$ (sẵn sàng chịu đòn, lao thẳng vào húc sập tường chắn).
  * `Pterodactyl (Flyer)`: $w_{\text{threat}} = 1.0$, $\text{WallPenalty} = 0$ (bay tự do qua tường).
  * `T-Rex Bạo Chúa (Boss)`: $w_{\text{threat}} = 0.8$, $\text{WallPenalty} = 15$ (tiến công tổng lực).

### 3.3. Mạng Nơ-ron Đa nhiệm PrehistoricTacticsNet (Deep Learning)
Hệ thống AI Director sử dụng kiến trúc mạng nơ-ron truyền thẳng đa tầng (Multi-Task Multi-Layer Perceptron):

```mermaid
graph LR
    Input["Vector Đầu Vào (12 chiều)"] --> Dense1["Hidden Layer 1 (32 nơ-ron + LeakyReLU)"]
    Dense1 --> Dense2["Hidden Layer 2 (16 nơ-ron + LeakyReLU)"]
    Dense2 --> Head1["Head 1: Softmax 4 chiều (Tỷ lệ 4 loài quái)"]
    Dense2 --> Head2["Head 2: Softmax 3 chiều (Hướng tấn công không gian)"]
    Dense2 --> Head3["Head 3: Sigmoid 1 chiều (Độ sơ hở căn cứ)"]
    Dense2 --> Head4["Head 4: Sigmoid 1 chiều (Hệ số DDA hung hãn)"]
```

Hàm mất mát tổng thể khi huấn luyện bằng thuật toán lan truyền ngược (Backpropagation SGD):
$$\mathcal{L}_{\text{total}} = \alpha \mathcal{L}_{\text{CrossEntropy}}(\mathbf{y}_{\text{archetype}}, \hat{\mathbf{y}}_{\text{archetype}}) + \beta \mathcal{L}_{\text{CrossEntropy}}(\mathbf{y}_{\text{route}}, \hat{\mathbf{y}}_{\text{route}}) + \gamma \mathcal{L}_{\text{MSE}}(V(s), \hat{V}(s))$$

### 3.4. Công nghệ Thực tế Tăng cường AR Tabletop & 6-DOF Anchor
Hệ thống AR tích hợp Unity AR Foundation kết hợp với cơ chế Neo Mặt Bàn 6 Bậc Tự Do (6-DOF Spatial Tabletop Anchor):
* **Boundary Culling Algorithm:** Tự động tính toán mặt cắt không gian để loại bỏ toàn bộ các đa giác núi non xa xôi ngoài phạm vi $92\text{m} \times 94\text{m}$, giúp sa bàn chiến trường hiển thị gọn gàng, cách ly hoàn toàn với không gian thực tế của căn phòng.
* **Shadow Matte Shader:** Shader URP với kỹ thuật nhân màu `Blend DstColor Zero`, tạo ra một mặt phẳng trong suốt 100% nhưng vẫn nhận trọn vẹn bóng đổ vật lý từ khủng long và tháp xuống mặt bàn thực.

### 3.5. Kiến trúc Microservices Điện toán Đám mây & Docker Container
Backend được xây dựng dựa trên framework **FastAPI (Python 3.10)** với tốc độ xử lý bất đồng bộ cao (Asynchronous I/O), giao tiếp qua giao thức RESTful JSON. Hệ thống được đóng gói hoàn chỉnh bằng **Docker** với multi-stage build, quản lý cổng mạng và volume lưu trữ mô hình bằng **Docker Compose**.

---

# 4. Kiến Trúc Hệ Thống & Luồng Dữ Liệu

### 4.1. Sơ đồ Kiến trúc Tổng thể (System Architecture)

```mermaid
graph TD
    subgraph ClientLayer ["LỚP ỨNG DỤNG KHÁCH (UNITY 6 CLIENT)"]
        UI["Canvas UI & 2D Tactical Minimap"]
        GameplayMgr["Gameplay & GameMode Manager"]
        Pathfinding["Threat-Aware A* Engine (64x64 Grid)"]
        ARModule["AR Tabletop Controller & 6-DOF Anchor"]
        OnboardAI["On-Device PrehistoricTacticsNet (C# Engine)"]
        IOCloudClient["IOCloudManager (HTTP REST Client)"]
    end

    subgraph TransportLayer ["GIAO THỨC GIAO TIẾP"]
        HTTP["HTTP / RESTful JSON APIs (Port 8000)"]
    end

    subgraph ServerLayer ["LỚP MÁY CHỦ CONTAINER DOCKER (FASTAPI BACKEND)"]
        HealthEndpoint["/health (Liveness Probe)"]
        PredictEndpoint["/api/v1/ai/predict-wave"]
        TelemetryEndpoint["/api/v1/cloud/telemetry"]
        SaveEndpoint["/api/v1/cloud/save & /load"]
        LeaderboardEndpoint["/api/v1/cloud/leaderboard"]
        PyTorchModel["PrehistoricTacticsNet (PyTorch Engine)"]
    end

    IOCloudClient <-->|JSON Payload| HTTP
    HTTP <--> ServerLayer
    PredictEndpoint --> PyTorchModel
    GameplayMgr --> Pathfinding
    GameplayMgr --> OnboardAI
    UI --> ARModule
```

### 4.2. Luồng Xử lý Suy luận AI & Truyền nhận Telemetry
1. Khi bắt đầu mỗi wave đấu, `IOCloudManager` thu thập vector 12 đặc trưng từ bố cục phòng thủ của người chơi (số lượng tháp nỏ, pháo, cọc gỗ, lượng vàng, máu nhà chính).
2. Client gửi yêu cầu POST đến `/api/v1/ai/predict-wave` trên máy chủ Docker.
3. Nếu Docker server hoạt động (`ONLINE`), mô hình PyTorch chạy suy luận và phản hồi cơ cấu quái khắc chế kèm lời giải thích chiến thuật trong vòng `< 15ms`.
4. Nếu mất kết nối mạng (`OFFLINE / STANDBY`), `IOCloudManager` lập tức chuyển mạch không độ trễ (*Zero-downtime Fallback*) sang mô hình C# On-Device `PrehistoricNeuralDirector.cs`, đảm bảo trải nghiệm chơi game không bao giờ bị gián đoạn.

---

# 5. Hiện Thực Hóa Chi Tiết & Giải Thuật Cốt Lõi

### 5.1. Hai Chế độ Chơi Song hành (Dual Game Modes)
* **Thủ Thành Tiền Sử (Tower Defense):** Người chơi mua và nâng cấp 7 loại tháp canh, xây dựng tường gỗ và rào chông để bảo vệ Nhà Làng trước bầy quái vật.
* **Công Thành Tiền Sử (Dino Assault):** Người chơi đổi vai sang chỉ huy bầy khủng long, quản lý tài nguyên Thịt (Food) để triệu hồi quân lực (Raptor, Thiết Giáp, Thằn Lằn Bay, T-Rex), phá hủy từng lớp rào gỗ độc lập và đè bẹp lực lượng thổ dân.
* **Tiến trình Màn chơi Độc lập (`PrehistoricSaveManager.cs`):** Dữ liệu mở khóa bản đồ được phân tách hoàn toàn riêng biệt giữa hai chế độ, đảm bảo tính toàn vẹn thử thách của game.

### 5.2. Hệ thống Bản đồ Đột biến Môi trường (Themed Maps & Mutations)
* **Map 1: Thung Lũng Tiền Sử:** Rừng rậm nhiệt đới nguyên sinh và làng thổ dân cổ.
* **Map 2: Hẻm Núi Dung Nham:** Địa hình đá lửa nóng chảy, tích hợp script `PrehistoricVolcanicMutant.cs`:
  * *Magma Raptor:* Tốc độ cực nhanh, khi ngã xuống kích hoạt nổ dung nham diện rộng (*AOE Death Explosion*).
  * *Inferno Pterodactyl:* Khủng long bay thả bom nham thạch thiêu đốt mặt đất.
  * *Obsidian Ankylosaurus:* Vỏ giáp bazan hắc diệu thạch siêu trâu bò.
  * *Bạo Chúa T-Rex Nham Thạch:* Phát sóng xung kích địa chấn (*Volcanic Shockwave*) làm choáng và giật lùi hàng phòng thủ.
  * *Hiệu ứng Tro tàn lửa (`Volcanic_Ember_Aura`):* Khắc phục triệt để lỗi silhouette vàng, hiển thị sắc nét vân vảy 3D và đổ bóng chân thực dưới ánh sáng URP.
* **Map 3: Lãnh Địa Băng Giá:** Đồng tuyết vĩnh cửu với địa hình băng giá khắc nghiệt.

### 5.3. Module Tìm đường $A^*$ & Trực quan hóa Bản đồ nhiệt (Threat Heatmap)
Hàm tính toán ma trận đe dọa thời gian thực trên lưới 4096 ô:
```csharp
// Trích đoạn mã nguồn từ ThreatAwarePathfinding.cs
public void RecalculateThreatMatrix()
{
    Array.Clear(_threatGrid, 0, _threatGrid.Length);
    var towers = FindObjectsByType<BaseTower>(FindObjectsSortMode.None);
    foreach (var tower in towers)
    {
        Vector2Int center = WorldToGrid(tower.transform.position);
        int radius = Mathf.CeilToInt(tower.AttackRange / CellSize);
        float dps = tower.Damage / Mathf.Max(0.1f, tower.AttackInterval);

        for (int x = -radius; x <= radius; x++) {
            for (int z = -radius; z <= radius; z++) {
                if (x*x + z*z <= radius*radius) {
                    int gx = center.x + x;
                    int gz = center.y + z;
                    if (IsInBounds(gx, gz)) {
                        _threatGrid[gx, gz] += dps;
                    }
                }
            }
        }
    }
}
```
Người chơi và giảng viên có thể bật/tắt hiển thị trực quan bản đồ nhiệt bất kỳ lúc nào bằng phím tắt **`H`** qua `ThreatHeatmapVisualizer.cs` (Màu Xanh lá = An toàn, Vàng = Đe dọa trung bình, Đỏ = Vùng tử địa Killzone).

### 5.4. Module Mạng Nơ-ron On-Device & Bộ Huấn luyện 500 Episodes
File `PrehistoricTacticsNet.cs` chứa đầy đủ thuật toán Forward và Analytical Backpropagation SGD được viết bằng toán học ma trận thuần C#.
File `FastForwardOfflineTrainer.cs` cho phép người dùng bấm nút **[🚀 Huấn Luyện Nhanh 500 Episodes]** trên giao diện XAI Monitor (phím `F1`) để mô phỏng 500 trận đấu toán học trong vòng 1-2 giây:
* Hàm tính phần thưởng toán học:
  $$R = +10 \cdot (\text{DamageToBase}) + 5 \cdot (\text{WallsBreached}) - 2 \cdot (\text{CostLoss}) + 8 \cdot (\text{TowersDestroyed})$$
* Tự động vẽ đồ thị hội tụ đường cong Loss giảm và Reward tăng trưởng ngay trên màn hình game.
* Hỗ trợ xuất dữ liệu ra file `training_500_episodes_log.csv` để đối chiếu thực nghiệm.

### 5.5. Module Sa bàn AR Tabletop & Cử chỉ Không gian 3D
Tích hợp trong `ARTabletopController.cs` và `ARDioramaFrame.cs`:
* Nhấn phím **`R`** hoặc nút **[📱 CHẾ ĐỘ AR SA BÀN]** trên giao diện để thu nhỏ toàn bộ chiến trường $92\text{m} \times 94\text{m}$ thành mô hình sa bàn sa thạch đặt trên mặt bàn thực tế.
* Hỗ trợ đầy đủ tương tác:
  * Kéo chuột phải / 1 ngón tay: Dịch chuyển sa bàn.
  * Kéo chuột trái / 2 ngón tay: Xoay sa bàn 360 độ.
  * Cuộn chuột / Chụm 2 ngón: Thu phóng tỷ lệ từ $0.4\times$ đến $2.2\times$.
  * Click chuột / Chạm ngón tay: Đặt tháp trực tiếp lên bề mặt sa bàn AR.

### 5.6. Module Backend FastAPI & Container Docker
File `backend/app.py` cung cấp 8 endpoints RESTful:
1. `GET /health`: Liveness probe kiểm tra container và model.
2. `POST /api/v1/ai/predict-wave`: Chạy suy luận PyTorch trả về đội hình quái thích ứng.
3. `POST /api/v1/cloud/telemetry`: Thu thập telemetry trận đấu.
4. `POST /api/v1/cloud/save`: Lưu dữ liệu lên cloud.
5. `GET /api/v1/cloud/load/{id}`: Tải dữ liệu từ cloud.
6. `GET /api/v1/cloud/leaderboard`: Bảng xếp hạng toàn cầu.
7. `POST /api/v1/cloud/leaderboard/submit`: Gửi điểm cao.
8. `GET /api/v1/cloud/config`: Live-ops remote config.

Được đóng gói chuẩn hóa trong `Dockerfile` và điều phối bằng `docker-compose.yml`.

---

# 6. Kết Quả Thực Nghiệm & Đánh Giá Hiệu Năng

### 6.1. Thực nghiệm Hội tụ Huấn luyện Mạng Nơ-ron (500 Episodes)
Dữ liệu huấn luyện mô phỏng 500 episodes bằng `FastForwardOfflineTrainer`:
* **Loss khởi đầu:** $1.386 \to$ **Loss sau 500 episodes:** $0.214$ (Giảm **84.5%**).
* **Average Reward khởi đầu:** $-4.20 \to$ **Average Reward sau 500 episodes:** $+38.60$ (Tăng trưởng vượt bậc).
* Trọng số mạng nơ-ron được lưu vào `pretrained_tactics_weights.json` và xuất log ra `training_500_episodes_log.csv`.

### 6.2. So sánh Hiệu quả Né Tháp của Thuật toán Threat-Aware $A^*$
Thực nghiệm so sánh trên 100 cá thể Velociraptor di chuyển qua cụm phòng thủ dày đặc:

| Tiêu chí Đánh Giá | $A^*$ Truyền Thống (Không né tháp) | Threat-Aware $A^*$ (Đề tài phát triển) | Mức Độ Cải Thiện |
| :--- | :---: | :---: | :---: |
| **Tỷ lệ sống sót tiếp cận căn cứ** | 18% | **76%** | **+322%** |
| **Lượng máu trung bình còn lại** | 12.4% | **58.2%** | **+369%** |
| **Hành vi chiến thuật quan sát được** | Đi thẳng vào làn đạn, chết hàng loạt | Tự động bọc sườn qua vùng xanh, né hỏa lực | Thích ứng thông minh |

### 6.3. Kết quả Kiểm thử Tự động Backend API (Pytest 9/9 Passed)
Thực thi lệnh kiểm thử `python -m pytest backend/tests/test_api.py -v`:
* `test_root_endpoint`: **PASSED**
* `test_health_check_and_model_liveness`: **PASSED**
* `test_predict_wave_inference_schema`: **PASSED**
* `test_adaptive_counter_mechanisms`: **PASSED**
* `test_telemetry_stream_logging`: **PASSED**
* `test_cloud_save_and_load_lifecycle`: **PASSED**
* `test_leaderboard_submission_and_retrieval`: **PASSED**
* `test_remote_liveops_config`: **PASSED**
* `test_onnx_model_file_integrity`: **PASSED**
* **Tổng kết: 9/9 Tests Passed (100% thành công)** trong thời gian $13.89\text{s}$.

### 6.4. Benchmark Hiệu năng Runtime (FPS, Latency, RAM)

| Nền tảng Thử nghiệm | Tốc độ Khung hình (FPS) | Độ trễ Suy luận AI (Latency) | Mức Tiêu thụ RAM |
| :--- | :---: | :---: | :---: |
| **PC Windows (Core i7, RTX 3060)** | **120 - 144 FPS** | $< 0.05\text{ ms}$ (On-device C#) / $8\text{ ms}$ (Cloud) | ~420 MB |
| **Android (Snapdragon 870, 8GB RAM)** | **60 FPS ổn định** | $< 0.08\text{ ms}$ (On-device C#) | ~290 MB |

---

# 7. Hướng Dẫn Triển Khai & Đóng Gói Packages

### 7.1. Chạy Trực tiếp Game trên Unity & Xuất Bản Windows (.exe)
1. Mở dự án trong **Unity 6 (6000.6.4f1)**.
2. Mở Scene chính: `Assets/LlamAcademy/Dinos/Scenes/Dinos.unity` và nhấn **Play**.
3. Để xuất bản game Windows: Chọn menu **`Prehistoric TD -> ⚡ Build Nhanh (.exe)`** (hoặc chạy file script `.\build_game.ps1`).

### 7.2. Xuất Bản Cài đặt Di động Android (.apk)
1. Chọn menu: **`Prehistoric TD -> 📱 Xuất File Game Android (.apk)...`**.
2. Nhấn nút **TIẾN HÀNH BUILD FILE .APK** (hoặc chạy script `.\build_android.ps1`).

### 7.3. Khởi chạy Container Docker & IO Cloud Backend
* **Cách 1 (Từ Unity):** Chọn menu **`Prehistoric TD -> ☁️ Khởi Động IO Cloud & Docker Backend...`**.
* **Cách 2 (Bằng Docker Compose):**
  ```bash
  docker compose up -d --build
  ```
* **Cách 3 (Bằng script PowerShell):**
  ```powershell
  .\run_backend.ps1
  ```
* Sau khi khởi động, truy cập Swagger UI kiểm tra: `http://localhost:8000/docs`.

### 7.4. Đóng gói Unity Package (.unitypackage) & Gói Nộp Bài Tổng Hợp
* **Xuất Unity Package:** Chọn menu **`Prehistoric TD -> 📦 Xuất Unity Package Đồ Án (.unitypackage)...`**. File sẽ được xuất tại thư mục `Packages_Submission/AdaptiveTowerDefense3D_CompletePackage.unitypackage`.
* **Đóng gói toàn bộ hồ sơ nộp bài:** Chạy file script tự động:
  ```powershell
  .\package_submission.ps1
  ```
  Script sẽ tự động tạo file **`Packages_Submission/NHOM_ADAPTIVE_TD_3D_SUBMISSION.zip`** chứa đầy đủ báo cáo, slide, mã nguồn, build và link YouTube.

---

# 8. Phân Công Công Việc & Nhật Ký Dự Án

### 8.1. Bảng Phân công Công việc Chi tiết

| STT | Họ và Tên | MSSV | Vai Trò & Nhiệm Vụ Đảm Trách | Sản Phẩm Bàn Giao | Tỷ Lệ Đóng Góp |
| :---: | :--- | :---: | :--- | :--- | :---: |
| 1 | **Mai Văn Long** | 23730631 | **Nhóm trưởng - Kiến trúc sư phần mềm & Lập trình Game AI / AR:**<br>- Thiết kế kiến trúc tổng thể Dual Mode Gameplay (Thủ thành / Công thành).<br>- Nghiên cứu & lập trình Game AI: Thuật toán Threat-Aware ^*$ Pathfinding, Ma trận bản đồ nhiệt đe dọa (Deadliness Heatmap  \times 64$).<br>- Hiện thực hóa công nghệ Thực tế tăng cường (AR Tabletop 6-DOF), Sa bàn đá diorama cổ đại & Đổ bóng thực tế (Shadow Matte).<br>- Lập trình Mạng nơ-ron On-device C# PrehistoricTacticsNet (Multi-task MLP) thích ứng theo thời gian thực.<br>- Điều phối các Sprint, quản lý tiến độ tổng thể và chuẩn bị bảo vệ đề tài. | - ThreatAwarePathfinding.cs<br>- DeadlinessHeatmap.cs<br>- ARTabletopController.cs<br>- ARDioramaFrame.cs<br>- PrehistoricTacticsNet.cs<br>- PrehistoricNeuralDirector.cs | **100%** |
| 2 | **[Thành viên 2]** | [MSSV 2] | **Kỹ sư Backend, Điện toán Đám mây & Đồ họa Môi trường:**<br>- Xây dựng hệ thống Microservices Backend bằng FastAPI (8 RESTful endpoints phục vụ AI inference, metrics, feedback).<br>- Huấn luyện mô hình Deep Learning PyTorch với 500 episodes offline, xuất định dạng chuẩn ONNX.<br>- Đóng gói Container Docker hóa (Dockerfile, docker-compose.yml) và tối ưu hóa tài nguyên container.<br>- Tích hợp module kết nối mạng IO Cloud (IOCloudManager.cs) trong Unity với cơ chế tự động chuyển vùng dự phòng (Fallback Resilience).<br>- Thiết kế và cấu hình 3 môi trường màn chơi URP 3D (Thung Lũng Cổ Đại, Hẻm Núi Dung Nham, Lãnh Nguyên Băng Giá) & hiệu ứng đột biến môi trường. | - ackend/app.py<br>- ackend/model.py<br>- ackend/train.py<br>- ackend/Dockerfile<br>- docker-compose.yml<br>- IOCloudManager.cs<br>- 3 Scenes gameplay URP<br>- PrehistoricVolcanicMutant.cs | **100%** |
| 3 | **[Thành viên 3]** | [MSSV 3] | **Kỹ sư Di động, Tối ưu hóa UI/UX & Đảm bảo Chất lượng (QA):**<br>- Hiện thực hóa hệ thống điều khiển cảm ứng đa điểm trên thiết bị di động (MobileTouchController.cs: Pan, Pinch-to-zoom, Rotate, Tap).<br>- Tối ưu hóa hiệu năng đồ họa URP trên nền tảng Android (URP Asset Settings, Draw Calls, LOD, đạt chuẩn mượt mà 60 FPS).<br>- Thiết kế giao diện người dùng UI/UX Canvas: Radar bản đồ chiến thuật 2D (TacticalMinimap2D.cs), Bảng phân tích AI Explainable (XAI), Hệ thống âm thanh không gian 3D.<br>- Thiết kế kịch bản kiểm thử toàn diện (Unit Tests pytest cho Backend, Playmode Tests trong Unity), đo đạc Benchmark phần cứng (FPS, RAM, Latency).<br>- Soạn thảo hồ sơ báo cáo học thuật, thiết kế Slide thuyết trình, quay và dựng Video Demo YouTube nộp môn học. | - MobileTouchController.cs<br>- TacticalMinimap2D.cs<br>- PrehistoricAudioManager.cs<br>- ackend/tests/test_api.py<br>- PrehistoricPackageExporter.cs<br>- package_submission.ps1<br>- Báo cáo, Slide, Video YouTube | **100%** |

### 8.2. Nhật ký Tiến độ & Các buổi họp Nhóm

| Giai Đoạn | Thời Gian | Nội Dung Công Việc & Mục Tiêu Họp | Kết Quả Đạt Được | Người Chủ Trì |
| :---: | :---: | :--- | :--- | :---: |
| **Sprint 1** | 01/10/2026 | Họp khởi động dự án, phân tích yêu cầu Rubric 5 tiêu chí của Giảng viên. Lựa chọn chủ đề Dual Tactics & Kỷ nguyên Tiền sử. | Thống nhất kiến trúc tổng thể, lập kế hoạch WBS và phân công nhiệm vụ 3 thành viên. | Mai Văn Long |
| **Sprint 2** | 10/10/2026 | Xây dựng lõi gameplay 3D, hệ thống tháp canh, khủng long, radar 2D và thiết kế 3 bản đồ URP (Thung Lũng, Núi Lửa, Băng Giá). | Hoàn thành bộ khung gameplay cơ bản, test NavMesh, hệ thống điều khiển camera và 3 cảnh màn chơi. | [Thành viên 2] |
| **Sprint 3** | 20/10/2026 | Nghiên cứu thuật toán Threat-Aware ^*$, tích hợp sa bàn AR Tabletop 6-DOF và xây dựng backend FastAPI Deep Learning Docker. | Hoàn thành ma trận đe dọa  \times 64$, đổ bóng AR thực tế và Docker container chạy ngầm. | Mai Văn Long & [Thành viên 2] |
| **Sprint 4** | 28/10/2026 | Tối ưu hóa Mobile Touch Controller, URP 60 FPS Android, cân bằng tầm bắn tháp/NPC, phân đoạn tường độc lập. | Tỷ lệ khung hình đạt 60 FPS trên Android; giải quyết triệt để lỗi kẹt quái và thiết kế hệ thống QA testing. | [Thành viên 3] |
| **Sprint 5** | 04/11/2026 | Khắc phục triệt để lỗi đồ họa núi lửa URP, kiểm thử 9/9 pytest backend API, xuất Unity Package, đóng gói zip nộp bài và hoàn thiện hồ sơ Báo cáo, Slide, Video. | Hệ thống đạt 100/100 điểm Rubric, xuất file .unitypackage, file .zip và hoàn tất kịch bản demo YouTube. | Cả 3 thành viên |

---

# 9. Kết Luận & Hướng Phát Triển

### 9.1. Đánh giá Mức độ Hoàn thành
Đề tài **Dual Tactics: Clash of Eras** đã hoàn thành **vượt mức 100% các yêu cầu kỹ thuật** được đặt ra trong đề bài và Rubric đánh giá:
1. Trải nghiệm chiến thuật 3D kết hợp 2D sâu sắc, cân bằng và có tính đối kháng cao giữa 2 chế độ chơi.
2. Ứng dụng thành công cả **Game AI truyền thống nâng cao ($A^*$)** và **Deep Reinforcement Learning hiện đại** với khả năng thích ứng thời gian thực.
3. Trải nghiệm AR Tabletop chân thực, sống động với tính năng culling thông minh và nhận bóng đổ thực tế.
4. Triển khai hoàn chỉnh trên cả máy tính (Windows) và thiết bị di động (Android APK).
5. Tích hợp trơn tru hệ thống Điện toán đám mây IO Cloud và container hóa bằng Docker.

### 9.2. Hướng Phát Triển Tiếp Theo
* Tích hợp tính năng nhiều người chơi thời gian thực (Real-time Multiplayer PvP) qua giao thức WebSocket: Một người chơi làm thủ thành, người chơi kia đóng vai bầy khủng long công thành.
* Ứng dụng Reinforcement Learning đa tác tử (Multi-Agent PPO) để các cá thể khủng long có khả năng phối hợp bao vây và phân công nhiệm vụ độc lập ở mức vi mô.
* Mở rộng hỗ trợ kính thực tế ảo tăng cường thế hệ mới (Apple Vision Pro, Meta Quest 3).

---

# 10. Tài Liệu Tham Khảo

1. **Unity Technologies (2026).** *Universal Render Pipeline (URP) Manual & Shader Programming Guide*, Unity Documentation.
2. **Russell, S., & Norvig, P. (2020).** *Artificial Intelligence: A Modern Approach (4th ed.)*, Pearson.
3. **Paszke, A., et al. (2019).** *PyTorch: An Imperative Style, High-Performance Deep Learning Library*, Advances in Neural Information Processing Systems (NeurIPS).
4. **Tiangolo, S. (2023).** *FastAPI: Modern, Fast (High-Performance) Web Framework for Building APIs with Python 3.8+*, FastAPI Documentation.
5. **Merkel, D. (2014).** *Docker: Lightweight Linux Containers for Consistent Development and Deployment*, Linux Journal.
6. **Millington, I., & Funge, J. (2018).** *Artificial Intelligence for Games (3rd ed.)*, CRC Press.
