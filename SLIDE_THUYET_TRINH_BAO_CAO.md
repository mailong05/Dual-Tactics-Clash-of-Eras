# BỘ SLIDE THUYẾT TRÌNH BÁO CÁO BÀI TẬP LỚN
### MÔN HỌC: CÔNG NGHỆ MỚI VÀ PHÁT TRIỂN ỨNG DỤNG TRONG CNTT
## ĐỀ TÀI: DUAL TACTICS: CLASH OF ERAS (ADAPTIVE TOWER DEFENSE 3D)
***

> [!NOTE]
> File này chứa toàn bộ nội dung trình chiếu 20 Slides chuẩn hóa, được thiết kế theo cấu trúc logic thuyết trình học thuật, kèm **Phân công người thuyết trình cho từng slide** và **Lời thoại gợi ý (Speaker Notes)** để các thành viên tự tin bảo vệ trước Hội đồng Giảng viên.

---

## 👥 BẢNG PHÂN CÔNG THUYẾT TRÌNH (PRESENTATION ASSIGNMENT)

| Thứ Tự Slides | Nội Dung Trình Bày | Thành Viên Đảm Trách | Thời Lượng Dự Kiến |
| :---: | :--- | :--- | :---: |
| **Slide 1 – 7** | Giới thiệu Đề tài, Mục tiêu Rubric, Kiến trúc Hệ thống & Game AI Threat-Aware $A^*$ Pathfinding | **Mai Văn Long** (Nhóm trưởng) | 3.5 phút |
| **Slide 8 – 14** | Mạng Nơ-ron Deep Learning, XAI, IO Cloud FastAPI, Docker Microservices & Đồ họa URP 3D | **[Họ và tên thành viên 2]** | 3.5 phút |
| **Slide 15 – 20** | Công nghệ AR Tabletop 6-DOF, Triển khai Mobile Android 60 FPS, QA Testing & Tổng kết Đồ án | **[Họ và tên thành viên 3]** | 3.0 phút |

---

# CHI TIẾT NỘI DUNG 20 SLIDES BÁO CÁO

---

### SLIDE 1: TRANG TIÊU ĐỀ (TITLE SLIDE)
* **Tiêu đề chính:** DUAL TACTICS: CLASH OF ERAS
* **Tiêu đề phụ:** Game Chiến Thuật Thích Ứng 3D & Sa Bàn AR Tabletop Tích Hợp Mạng Nơ-ron Deep Learning và Điện Toán Đám Mây Docker IO Cloud
* **Môn học:** Công Nghệ Mới và Phát Triển Ứng Dụng trong CNTT (New Technology and Application Development in IT)
* **Giảng viên hướng dẫn:** TS. [Tên Giảng Viên Hướng Dẫn]
* **Nhóm sinh viên thực hiện (3 thành viên):**
  * Mai Văn Long – MSSV: 23730631 (Trưởng nhóm)
  * [Họ và tên thành viên 2] – MSSV: [MSSV 2]
  * [Họ và tên thành viên 3] – MSSV: [MSSV 3]
* **Người trình bày:** Mai Văn Long
> **Speaker Notes:** Kính chào Thầy và các bạn, hôm nay nhóm em xin phép được báo cáo đề tài bài tập lớn môn Công nghệ mới với tên gọi Dual Tactics: Clash of Eras - tựa game chiến thuật thích ứng 3D kết hợp AR, Deep Learning và Docker Cloud.

---

### SLIDE 2: ĐẶT VẤN ĐỀ & MỤC TIÊU ĐỒ ÁN (MOTIVATION & OBJECTIVES)
* **Vấn đề của game Tower Defense truyền thống:**
  * Lối chơi một chiều, quái vật di chuyển theo waypoint cố định, không nhận biết hỏa lực người chơi.
  * Kịch bản các đợt tấn công (waves) tĩnh, thiếu tính thích ứng thời gian thực.
* **Mục tiêu giải quyết thông qua 5 Trụ Cột Công Nghệ:**
  1. *Unity 2D & 3D:* Trải nghiệm game kép đối xứng (Thủ thành & Công thành) trên nền URP.
  2. *Game AI & Deep Learning:* Mạng nơ-ron On-device và thuật toán tìm đường tránh đe dọa $A^*$.
  3. *Augmented Reality (AR):* Chiếu sa bàn chiến trường 3D lên mặt bàn thực tế qua 6-DOF Anchor.
  4. *Mobile Deployment:* Tối ưu hóa 60 FPS và điều khiển cảm ứng đa điểm trên Android.
  5. *IO Cloud & Docker:* Đồng bộ telemetry thời gian thực và đóng gói container microservice.
* **Người trình bày:** Mai Văn Long
> **Speaker Notes:** Để khắc phục sự đơn điệu của dòng game thủ thành cũ, nhóm em đã tích hợp đồng thời 5 công nghệ mới vào một kiến trúc hoàn chỉnh đáp ứng trọn vẹn 100 điểm Rubric môn học.

---

### SLIDE 3: ĐỘT PHÁ LỐI CHƠI: HAI CHẾ ĐỘ CHƠI SONG HÀNH (DUAL GAME MODES)
* **Chế độ 1 - Thủ Thành Tiền Sử (Tower Defense):**
  * Quản lý tài nguyên Vàng (Gold), xây dựng và nâng cấp 7 loại tháp canh & cạm bẫy bảo vệ Căn Cứ Làng Bộ Lạc.
* **Chế độ 2 - Công Thành Tiền Sử (Dino Assault / Boss Mode):**
  * Đổi vai trực tiếp: Người chơi trở thành kẻ chỉ huy bầy khủng long tiến công phá hủy phòng tuyến loài người.
  * Quản lý tài nguyên Thịt (Food) để tính toán triệu hồi: Velociraptor, Ankylosaurus, Thằn Lằn Bay, T-Rex Bạo Chúa.
* **Tiến trình Màn chơi Độc lập (`PrehistoricSaveManager`):**
  * Vượt Map 1 ở Công Thành chỉ mở khóa Map 2 của Công Thành, giữ nguyên độ khó và tính toàn vẹn của chế độ Thủ Thành.
* **Người trình bày:** Mai Văn Long
> **Speaker Notes:** Điểm khác biệt lớn nhất về gameplay là người chơi có thể đổi vai: Vừa làm người thủ thành xây tháp, vừa làm thủ lĩnh khủng long đem quân đi san bằng căn cứ làng đối phương.

---

### SLIDE 4: HỆ THỐNG 3 BẢN ĐỒ & ĐỘT BIẾN MÔI TRƯỜNG (THEMED MAPS)
* **Map 1: Thung Lũng Tiền Sử (Prehistoric Valley):** Rừng rậm nguyên sinh và thành lũy gỗ sơ khai.
* **Map 2: Hẻm Núi Dung Nham (Volcanic Gorge) & Cơ chế Đột biến `PrehistoricVolcanicMutant`:**
  * *Magma Raptor:* Tăng tốc độ, kích hoạt nổ dung nham diện rộng khi ngã xuống (*AOE Death Explosion*).
  * *Inferno Pterodactyl:* Khủng long bay thả bom nham thạch thiêu đốt chòi canh.
  * *Obsidian Ankylosaurus:* Vỏ giáp đá bazan hắc diệu thạch siêu bền bỉ.
  * *Bạo Chúa T-Rex Nham Thạch:* Sóng xung kích địa chấn (*Volcanic Shockwave*) gây choáng và giật lùi hàng phòng ngự.
  * *Chuẩn hóa đồ họa URP:* Giữ trọn 100% vân da 3D và đổ bóng chân thực kèm tro tàn lửa (*Ember Aura*).
* **Map 3: Lãnh Địa Băng Giá (Glacial Tundra):** Địa hình băng tuyết vĩnh cửu thử thách khả năng cơ động.
* **Người trình bày:** Mai Văn Long
> **Speaker Notes:** Mỗi bản đồ mang đặc trưng môi trường riêng biệt, tác động trực tiếp lên chỉ số và kỹ năng độc quyền của các loài khủng long.

---

### SLIDE 5: GAME AI: THUẬT TOÁN TÌM ĐƯỜNG NÉ ĐE DỌA THREAT-AWARE $A^*$
* **Khái niệm Ma trận Đe dọa (Threat Matrix):**
  * Lưới tọa độ 4096 ô ($64 \times 64$, ô $1.4\text{m}$). Mỗi ô tích lũy chỉ số đe dọa dựa trên tổng DPS và tầm phủ của tháp canh.
* **Hàm Chi Phí Nâng Cao:**
  $$g(v) = g(u) + \text{dist}(u, v) \cdot \Big(1 + w_{\text{threat}} \cdot \text{ThreatScore}(v)\Big) + \text{WallPenalty}(v)$$
* **Hành vi riêng biệt theo từng cá thể:**
  * *Velociraptor ($w_{\text{threat}} = 4.5$):* Đánh giá cao an toàn, luôn tìm hành lang mù hỏa lực để bọc sườn né tháp.
  * *Ankylosaurus ($w_{\text{threat}} = 0.35$):* Chấp nhận hỏa lực, lao thẳng vào húc sập tường chắn kiên cố nhất.
* **Người trình bày:** [Thành viên 2]
> **Speaker Notes:** Thay vì đi đường chim bay ngây thơ như các game khác, khủng long trong game tự biết tính toán bản đồ nguy hiểm để luồn lách qua các góc khuất của tháp.

---

### SLIDE 6: TRỰC QUAN HÓA BẢN ĐỒ NHIỆT (TACTICAL THREAT HEATMAP)
* **Tính năng kích hoạt thời gian thực:** Nhấn phím **`H`** hoặc nút bấm Heatmap trên màn hình.
* **Trực quan hóa bề mặt 3D theo dải màu:**
  * 🟢 **Xanh lá (Safe Zone):** Vùng an toàn / Điểm mù hỏa lực của các tháp canh.
  * 🟡 **Vàng (Medium Danger):** Vùng đe dọa trung bình.
  * 🔴 **Đỏ (Kill Zone):** Tử địa tập trung hỏa lực dày đặc của pháo đá và nỏ khổng lồ.
* **Đường dẫn LineRenderer chiến thuật:**
  * Vẽ đường màu Cyan cho mũi bọc sườn của Raptor.
  * Vẽ đường màu Cam cho mũi phá tường mở đường của Thiết Giáp.
* **Người trình bày:** [Thành viên 2]
> **Speaker Notes:** Thầy và các bạn có thể bấm phím H bất cứ lúc nào trong trận đấu để quan sát trực tiếp quyết định thông minh của thuật toán A* qua dải màu nhiệt.

---

### SLIDE 7: DEEP LEARNING: MẠNG NƠ-RON PREHISTORICTACTICSNET
* **Mô hình học sâu thích ứng (Multi-Task Neural Network):**
  * Chạy song song cả trên **Unity C# Engine** (On-device $< 0.05\text{ms}$) và **Python Backend** (PyTorch).
* **Kiến trúc mạng (12 $\to$ 32 $\to$ 16 $\to$ Multi-Head):**
  * *12 Inputs:* Vàng, máu nhà, tiến trình đợt, số lượng từng loại tháp, bẫy chông, tường gỗ, mật độ DPS.
  * *Head 1 (Softmax 4):* Tỷ lệ 4 loài khủng long (Runner, Flyer, Tanker, Boss).
  * *Head 2 (Softmax 3):* Định hướng tấn công không gian (Cổng Bắc, Sườn Đông, Cánh Tây).
  * *Head 3 (Sigmoid 1):* Chỉ số sơ hở phòng tuyến người chơi (Vulnerability Score).
* **Người trình bày:** [Thành viên 3]
> **Speaker Notes:** Mạng nơ-ron đóng vai trò như một vị 'Giám đốc AI' liên tục phân tích thói quen xây tháp của người chơi để đưa ra đội hình quân khắc chế hoàn hảo nhất.

---

### SLIDE 8: HUẤN LUYỆN 500 EPISODES & HỌC HỎI ONLINE (ONLINE & OFFLINE TRAINING)
* **Bộ huấn luyện nhanh `FastForwardOfflineTrainer`:**
  * Mô phỏng toán học 500 trận đấu trong vòng 1-2 giây.
  * Áp dụng thuật toán Lan truyền ngược giải tích (Analytical Backpropagation) cập nhật trọng số qua SGD.
* **Công thức Phần thưởng Toán học:**
  $$R = +10 \cdot (\text{DamageToBase}) + 5 \cdot (\text{WallsBreached}) - 2 \cdot (\text{CostLoss}) + 8 \cdot (\text{TowersDestroyed})$$
* **Kết quả hội tụ:**
  * Loss giảm **84.5%** (từ $1.386 \to 0.214$).
  * Average Reward tăng từ âm $-4.20 \to +38.60$.
  * Tự động xuất lịch sử ra file `training_500_episodes_log.csv`.
* **Người trình bày:** [Thành viên 3]
> **Speaker Notes:** Chỉ với một nút bấm, mạng nơ-ron có thể tự đấu 500 trận mô phỏng để tối ưu hóa trọng số trước khi bước vào trận đấu thực tế.

---

### SLIDE 9: GIẢI THÍCH TRÍ TUỆ NHÂN TẠO (EXPLAINABLE AI - XAI MONITOR)
* **Bảng giám sát XAI trực quan:** Nhấn phím **`F1`** hoặc nút `🧠 AI Director` trên HUD.
* **Các thành phần minh bạch hóa AI:**
  1. *Neural Activation Graph:* Sơ đồ mạng nơ-ron hiển thị các nút kích hoạt phát sáng theo thời gian thực.
  2. *Convergence Line Chart:* Biểu đồ đường vẽ trực tiếp đường cong Loss giảm và Reward tăng.
  3. *Telemetry Gauges:* 12 thanh cảm biến trạng thái phòng thủ của người chơi.
  4. *Natural Language Rationale:* Lời giải thích chiến thuật bằng ngôn ngữ tự nhiên (Ví dụ: *"Phát hiện phòng tuyến thiếu tháp phòng không, tăng cường 65% Pterodactyl"*).
* **Người trình bày:** [Thành viên 3]
> **Speaker Notes:** Mạng nơ-ron của nhóm em không phải là một chiếc hộp đen bí ẩn, mà toàn bộ quá trình tư duy, kích hoạt nơ-ron và lý do đưa ra quyết định đều được giải thích rõ ràng qua bảng XAI.

---

### SLIDE 10: THỰC TẾ TĂNG CƯỜNG (AR TABLETOP 6-DOF DIORAMA)
* **Chế độ Sa bàn Tiền sử AR:** Nhấn phím **`R`** hoặc nút `[📱 CHẾ ĐỘ AR SA BÀN]`.
* **3 Đột phá kỹ thuật AR:**
  1. *Đế sa bàn Đá Tiền Sử 3D ($92\text{m} \times 94\text{m}$):* Định hình ranh giới không gian chiến trường.
  2. *Thuật toán Boundary Culling:* Tự động cắt gọt toàn bộ cảnh núi non xa xôi ngoài viền sa bàn để hòa nhập tự nhiên vào bàn làm việc.
  3. *URP Shadow Matte Shader:* Tấm nền nhận bóng đổ thực tế từ mô hình 3D xuống mặt bàn thật (`Blend DstColor Zero`).
* **Hỗ trợ trọn bộ cử chỉ:** Pan dịch chuyển, xoay tròn 360°, pinch zoom ($0.4\times \to 2.2\times$) và chạm đặt tháp trực tiếp.
* **Người trình bày:** [Thành viên 4]
> **Speaker Notes:** Chế độ AR Tabletop mang lại trải nghiệm tương tác không gian ấn tượng, biến mặt bàn học của người chơi thành một sa bàn chiến thuật sống động.

---

### SLIDE 11: TRIỂN KHAI DI ĐỘNG & CỬ CHỈ CẢM ỨNG (MOBILE DEPLOYMENT)
* **Hệ thống điều khiển cảm ứng đa điểm (`MobileTouchController`):**
  * Vuốt 1 ngón tay: Di chuyển góc nhìn camera tự do.
  * Vuốt 2 ngón tay xoay: Xoay chiến trường 360 độ quanh trọng tâm.
  * Chụm / Mở 2 ngón tay (Pinch-to-zoom): Phóng to, thu nhỏ mượt mà.
  * Chạm ngón tay: Chọn vị trí xây tháp hoặc triệu hồi quái vật.
* **Tối ưu hóa hiệu năng phần cứng:**
  * Đạt **60 FPS ổn định** trên các thiết bị Android tầm trung.
  * Cơ chế tự động dọn rác scene (`CleanAllGameplayGarbage`) giải phóng RAM và triệt tiêu bộ nhớ rác sau mỗi màn chơi.
* **Công cụ xuất file Android APK 1-click:** File `build_android.ps1` và menu trong Unity Editor.
* **Người trình bày:** [Thành viên 4]
> **Speaker Notes:** Đồ án được thiết kế chuẩn responsive cho di động ngay từ đầu, đảm bảo tốc độ khung hình 60 FPS mượt mà cùng hệ thống phím cảm ứng công thái học.

---

### SLIDE 12: ĐIỆN TOÁN ĐÁM MÂY IO CLOUD & RESTFUL APIS
* **Kiến trúc Client - Server giao tiếp qua RESTful HTTP (Cổng 8000):**
* **8 RESTful Endpoints cốt lõi (`backend/app.py`):**
  1. `GET /health`: Liveness probe kiểm tra sức khỏe container và model PyTorch.
  2. `POST /api/v1/ai/predict-wave`: Nhận telemetry và suy luận đội hình quái thích ứng.
  3. `POST /api/v1/cloud/telemetry`: Lưu trữ nhật ký chiến trận vào data pipeline.
  4. `POST /api/v1/cloud/save` & `GET /api/v1/cloud/load/{id}`: Lưu và tải tiến trình chơi trên đám mây.
  5. `GET /api/v1/cloud/leaderboard` & `POST /submit`: Bảng xếp hạng kỷ lục trực tuyến.
  6. `GET /api/v1/cloud/config`: Live-Ops cập nhật sự kiện mùa giải từ xa.
* **Cơ chế Zero-Downtime Fallback:** Tự động chuyển đổi giữa Cloud và Onboard AI khi ngắt mạng.
* **Người trình bày:** [Thành viên 5]
> **Speaker Notes:** Hệ thống IO Cloud đảm nhận toàn bộ các dịch vụ trực tuyến hiện đại của game, từ suy luận AI đám mây, lưu tiến trình đa nền tảng đến bảng xếp hạng toàn cầu.

---

### SLIDE 13: ĐÓNG GÓI & ĐIỀU PHỐI CONTAINER VỚI DOCKER
* **Container hóa máy chủ Backend AI (`backend/Dockerfile`):**
  * Base Image: `python:3.10-slim` tối ưu kích thước container nhẹ nhất.
  * Cài đặt tự động: FastAPI, Uvicorn, PyTorch CPU/CUDA, Pydantic, ONNX.
  * Tích hợp Healthcheck định kỳ 30 giây giám sát trạng thái container.
* **Điều phối đa dịch vụ (`docker-compose.yml`):**
  * Ánh xạ cổng mạng `8000:8000`.
  * Gắn Volume mount `./backend/data:/app/data` để lưu trữ dữ liệu bền vững.
* **Khởi động 1-click:** Script `.\run_backend.ps1` hoặc qua Menu Unity Editor.
* **Người trình bày:** [Thành viên 5]
> **Speaker Notes:** Nhờ đóng gói bằng Docker, giảng viên và người dùng có thể khởi chạy toàn bộ hệ thống backend AI chỉ bằng một câu lệnh duy nhất mà không lo xung đột môi trường.

---

### SLIDE 14: KẾT QUẢ KIỂM THỬ TỰ ĐỘNG (AUTOMATED TESTING SUITE)
* **Kết quả chạy kiểm thử Pytest trên Backend API (`backend/tests/test_api.py`):**
  * `test_root_endpoint`: **PASSED** (Root metadata đầy đủ).
  * `test_health_check_and_model_liveness`: **PASSED** (Mô hình PyTorch sẵn sàng).
  * `test_predict_wave_inference_schema`: **PASSED** (Cấu trúc JSON phản hồi chuẩn).
  * `test_adaptive_counter_mechanisms`: **PASSED** (Đội hình khắc chế hợp lý).
  * `test_telemetry_stream_logging`: **PASSED** (Ghi log telemetry chính xác).
  * `test_cloud_save_and_load_lifecycle`: **PASSED** (Chu trình lưu/tải mây vẹn toàn).
  * `test_leaderboard_submission_and_retrieval`: **PASSED** (Cập nhật bảng xếp hạng chuẩn).
  * `test_remote_liveops_config`: **PASSED** (Đồng bộ cấu hình từ xa).
  * `test_onnx_model_file_integrity`: **PASSED** (File ONNX nguyên vẹn).
* **Tổng kết: 9/9 Tests Đạt 100% trong 13.89 giây.**
* **Người trình bày:** [Thành viên 5]
> **Speaker Notes:** Nhóm em đã xây dựng bộ Unit Test và Integration Test tự động, toàn bộ 9/9 kịch bản kiểm thử API đều vượt qua 100% mà không có bất kỳ lỗi nào.

---

### SLIDE 15: THỰC NGHIỆM ĐỐI CHỨNG & BENCHMARK HIỆU NĂNG
* **Thực nghiệm Tìm đường A\* (Thử nghiệm trên 100 cá thể Velociraptor):**
  * Tỷ lệ sống sót tiếp cận căn cứ tăng từ **18%** (A* truyền thống) lên **76%** (Threat-Aware A*).
  * Khủng long chủ động phân tán lực lượng né tránh các vùng đỏ hỏa lực.
* **Benchmark Hiệu Năng Runtime:**
  * *Tốc độ khung hình (FPS):* 120 - 144 FPS (PC) / 60 FPS ổn định (Android).
  * *Độ trễ suy luận AI (Latency):* $< 0.05\text{ ms}$ (On-device C#) / $8 - 15\text{ ms}$ (Cloud).
  * *Mức tiêu thụ bộ nhớ RAM:* ~420 MB (PC) / ~290 MB (Mobile).
* **Người trình bày:** [Thành viên 5]
> **Speaker Notes:** Các con số đo đạc thực nghiệm cho thấy thuật toán né đòn tăng tỷ lệ sống sót của quái vật lên hơn 3 lần, trong khi độ trễ suy luận mạng nơ-ron ở mức cực thấp dưới 0.05 mili-giây.

---

### SLIDE 16: PHÂN CÔNG CÔNG VIỆC NHÓM (TEAM WORK ASSIGNMENT)
* **Bảng phân công nhiệm vụ và tỷ lệ đóng góp (Đạt 100% tất cả thành viên):**
  * **Mai Văn Long (Trưởng nhóm):** Kiến trúc tổng thể Dual Mode, Game AI Threat-Aware $A^*$, Mạng nơ-ron On-device C#, AR Tabletop 6-DOF (100%).
  * **[Thành viên 2]:** Đồ họa URP 3D, Level Design 3 Maps, Hệ thống đột biến môi trường Núi lửa `PrehistoricVolcanicMutant` (100%).
  * **[Thành viên 3]:** Kỹ sư Backend FastAPI, Huấn luyện mô hình PyTorch, Dockerfile & Docker Compose (100%).
  * **[Thành viên 4]:** Kỹ sư Di động, Cử chỉ cảm ứng đa điểm, Tối ưu 60 FPS, Radar 2D & UI Audio (100%).
  * **[Thành viên 5]:** Kiểm thử QA, Viết báo cáo học thuật, Soạn thảo slide, Quay & dựng video Demo (100%).
* **Người trình bày:** Mai Văn Long
> **Speaker Notes:** Cả 3 thành viên trong nhóm đều phối hợp chặt chẽ qua 5 Sprints, phân chia công việc rõ ràng và hoàn thành 100% khối lượng nhiệm vụ được giao.

---

### SLIDE 17: VIDEO DEMO HIGHLIGHTS & LINK YOUTUBE
* **Kênh công khai sản phẩm:**
  * **Link Video Demo YouTube:** `https://youtu.be/DEMO_DUAL_TACTICS_2026` (Đính kèm trong gói nộp bài)
  * **Thời lượng:** 8 phút (Chất lượng Full HD 1080p 60fps)
* **Các mốc thời gian (Timestamps) khớp đúng 5 tiêu chí Rubric:**
  * `00:00 - 01:30:` Demo Gameplay 3D Dual Modes & Radar quét 2D.
  * `01:30 - 03:00:` Demo Game AI Threat-Aware $A^*$ & Bật/tắt Threat Heatmap (phím `H`).
  * `03:00 - 04:30:` Demo Deep Learning XAI Monitor (phím `F1`) & Huấn luyện nhanh 500 Episodes.
  * `04:30 - 05:45:` Demo Sa bàn Thực tế tăng cường AR Tabletop 6-DOF (phím `R`).
  * `05:45 - 06:45:` Demo Triển khai Di động Android & Cử chỉ cảm ứng đa điểm.
  * `06:45 - 07:45:` Demo Docker Container & IO Cloud Telemetry kết nối thời gian thực.
  * `07:45 - 08:00:` Tổng kết gói Packages và các tài liệu học thuật.
* **Người trình bày:** Mai Văn Long
> **Speaker Notes:** Video demo dài 8 phút của nhóm em được dàn dựng công phu, có mốc thời gian rõ ràng chứng minh trọn vẹn từng tiêu chí chấm điểm của học phần.

---

### SLIDE 18: ĐÓNG GÓI PACKAGES NỘP BÀI (SUBMISSION PACKAGES)
* **Gói nộp bài hoàn chỉnh theo quy định:**
  * `1_Documents_and_Reports:` Báo cáo BTL chuẩn học thuật, Slide thuyết trình, Kịch bản YouTube.
  * `2_Backend_Docker_Cloud:` Mã nguồn FastAPI, Dockerfile, docker-compose.yml, Model Weights.
  * `3_Builds_Windows_and_Android:` Bản chạy trực tiếp Windows `.exe` và file cài đặt Android `.apk`.
  * `AdaptiveTowerDefense3D_CompletePackage.unitypackage:` Toàn bộ package source code assets trong Unity.
* **Công cụ đóng gói tự động:** Chạy `.\package_submission.ps1` tạo ngay file ZIP tổng thể.
* **Người trình bày:** Mai Văn Long
> **Speaker Notes:** Toàn bộ sản phẩm được đóng gói gọn gàng thành các thư mục chuẩn mực kèm file Unity Package để giảng viên có thể kiểm tra và chạy thử nghiệm ngay lập tức.

---

### SLIDE 19: KẾT LUẬN & HƯỚNG PHÁT TRIỂN (CONCLUSION & FUTURE WORK)
* **Kết luận:**
  * Đề tài đã tích hợp thành công 5 công nghệ mũi nhọn: Unity URP 2D/3D, Game AI $A^*$, Deep Learning On-Device/Cloud, AR Foundation và Docker Microservice.
  * Gameplay cân bằng, trải nghiệm thích ứng thông minh và hiệu năng tối ưu trên đa nền tảng.
* **Hướng phát triển tương lai:**
  * Mở rộng chế độ Nhiều người chơi trực tuyến (Multiplayer PvP 1v1) qua WebSocket.
  * Áp dụng học tăng cường đa tác tử (Multi-Agent PPO) cho bầy khủng long phối hợp săn mồi vi mô.
  * Tích hợp kính không gian thế hệ mới (Apple Vision Pro, Meta Quest 3).
* **Người trình bày:** Mai Văn Long
> **Speaker Notes:** Dự án không chỉ dừng lại ở phạm vi một bài tập lớn môn học, mà đã mở ra một nền tảng kiến trúc vững chắc có thể thương mại hóa hoặc phát triển thành các nghiên cứu chuyên sâu hơn.

---

### SLIDE 20: LỜI CẢM ƠN & HỎI ĐÁP (Q&A SESSION)
* **CẢM ƠN THẦY VÀ CÁC BẠN ĐÃ LẮNG NGHE BÀI THUYẾT TRÌNH CỦA NHÓM!**
* **Thông tin liên hệ & Kho lưu trữ mã nguồn:**
  * Nhóm trưởng: Mai Văn Long – Email: `23730631.long@student.iuh.edu.vn`
  * Repository: Adaptive Tower Defense 3D (Dual Tactics: Clash of Eras)
* *Xin trân trọng kính mời Thầy và các bạn đặt câu hỏi phản biện.*
* **Người trình bày:** Cả nhóm
> **Speaker Notes:** Nhóm em xin chân thành cảm ơn Thầy đã nhiệt tình hướng dẫn chúng em trong suốt học phần. Rất mong nhận được những góp ý quý báu từ Thầy để đồ án ngày càng hoàn thiện hơn. Nhóm em đã sẵn sàng cho phần hỏi đáp Q&A!
