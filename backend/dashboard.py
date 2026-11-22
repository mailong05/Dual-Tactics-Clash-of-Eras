"""
Modern Responsive HTML5 Web Dashboard for IO Cloud & Deep Learning AI Backend
Adaptive Prehistoric Tower Defense 3D
Served directly by FastAPI at /dashboard and /web.
"""

def get_dashboard_html() -> str:
    return """<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>IO Cloud & AI Director | Prehistoric Tower Defense 3D</title>
    <!-- Chart.js CDN for beautiful interactive charts -->
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
    <style>
        :root {
            --bg-main: #0B0F19;
            --bg-card: rgba(17, 24, 39, 0.85);
            --bg-card-border: rgba(56, 189, 248, 0.2);
            --text-primary: #F8FAFC;
            --text-secondary: #94A3B8;
            --accent-cyan: #38BDF8;
            --accent-emerald: #10B981;
            --accent-amber: #F59E0B;
            --accent-rose: #F43F5E;
            --accent-purple: #A855F7;
        }

        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
        }

        body {
            background-color: var(--bg-main);
            color: var(--text-primary);
            padding: 20px;
            min-height: 100vh;
            background-image: 
                radial-gradient(at 10% 20%, rgba(56, 189, 248, 0.08) 0px, transparent 50%),
                radial-gradient(at 90% 80%, rgba(168, 85, 247, 0.08) 0px, transparent 50%);
        }

        .container {
            max-width: 1400px;
            margin: 0 auto;
        }

        /* Top Header */
        header {
            display: flex;
            flex-wrap: wrap;
            justify-content: space-between;
            align-items: center;
            padding: 16px 24px;
            background: var(--bg-card);
            border: 1px solid var(--bg-card-border);
            border-radius: 16px;
            margin-bottom: 24px;
            backdrop-filter: blur(12px);
            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
        }

        .brand-title {
            font-size: 20px;
            font-weight: 800;
            background: linear-gradient(135deg, #38BDF8, #A855F7);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .brand-subtitle {
            font-size: 12px;
            color: var(--text-secondary);
            font-weight: 500;
            margin-top: 4px;
        }

        .header-controls {
            display: flex;
            align-items: center;
            gap: 14px;
            flex-wrap: wrap;
        }

        .status-badge {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 6px 14px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 700;
            background: rgba(16, 185, 129, 0.15);
            color: var(--accent-emerald);
            border: 1px solid rgba(16, 185, 129, 0.3);
        }

        .pulse-dot {
            width: 8px;
            height: 8px;
            background-color: var(--accent-emerald);
            border-radius: 50%;
            animation: pulse 1.8s infinite;
        }

        @keyframes pulse {
            0% { transform: scale(0.95); box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.7); }
            70% { transform: scale(1); box-shadow: 0 0 0 8px rgba(16, 185, 129, 0); }
            100% { transform: scale(0.95); box-shadow: 0 0 0 0 rgba(16, 185, 129, 0); }
        }

        .btn-refresh {
            background: linear-gradient(135deg, #0284C7, #2563EB);
            color: white;
            border: none;
            padding: 8px 16px;
            border-radius: 10px;
            font-size: 13px;
            font-weight: 700;
            cursor: pointer;
            transition: all 0.2s;
            box-shadow: 0 4px 14px rgba(37, 99, 235, 0.3);
        }

        .btn-refresh:hover {
            transform: translateY(-1px);
            box-shadow: 0 6px 20px rgba(37, 99, 235, 0.45);
        }

        /* KPI Cards Grid */
        .kpi-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
            gap: 16px;
            margin-bottom: 24px;
        }

        .kpi-card {
            background: var(--bg-card);
            border: 1px solid var(--bg-card-border);
            border-radius: 14px;
            padding: 18px 20px;
            backdrop-filter: blur(10px);
            transition: transform 0.2s;
        }

        .kpi-card:hover {
            transform: translateY(-2px);
            border-color: rgba(56, 189, 248, 0.5);
        }

        .kpi-label {
            font-size: 12px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            color: var(--text-secondary);
            font-weight: 600;
            margin-bottom: 8px;
        }

        .kpi-value {
            font-size: 28px;
            font-weight: 800;
            color: var(--text-primary);
            display: flex;
            align-items: baseline;
            gap: 8px;
        }

        .kpi-sub {
            font-size: 12px;
            color: var(--accent-cyan);
            font-weight: 600;
        }

        /* Charts Section */
        .charts-row {
            display: grid;
            grid-template-columns: 2fr 1fr;
            gap: 20px;
            margin-bottom: 24px;
        }

        @media (max-width: 992px) {
            .charts-row {
                grid-template-columns: 1fr;
            }
        }

        .card {
            background: var(--bg-card);
            border: 1px solid var(--bg-card-border);
            border-radius: 16px;
            padding: 20px;
            backdrop-filter: blur(10px);
            box-shadow: 0 4px 20px rgba(0, 0, 0, 0.25);
        }

        .card-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 16px;
            padding-bottom: 12px;
            border-bottom: 1px solid rgba(255, 255, 255, 0.08);
        }

        .card-title {
            font-size: 15px;
            font-weight: 700;
            display: flex;
            align-items: center;
            gap: 8px;
            color: #E2E8F0;
        }

        .chart-container {
            position: relative;
            height: 280px;
            width: 100%;
        }

        /* Tables & Lists */
        .grid-split {
            display: grid;
            grid-template-columns: 1.4fr 1fr;
            gap: 20px;
            margin-bottom: 24px;
        }

        @media (max-width: 1100px) {
            .grid-split {
                grid-template-columns: 1fr;
            }
        }

        table {
            width: 100%;
            border-collapse: collapse;
            font-size: 13px;
        }

        th {
            text-align: left;
            padding: 10px 12px;
            color: var(--text-secondary);
            font-weight: 600;
            border-bottom: 1px solid rgba(255, 255, 255, 0.1);
        }

        td {
            padding: 10px 12px;
            border-bottom: 1px solid rgba(255, 255, 255, 0.05);
            color: #CBD5E1;
        }

        tr:hover td {
            background-color: rgba(255, 255, 255, 0.03);
            color: var(--text-primary);
        }

        .badge-mode {
            padding: 3px 8px;
            border-radius: 6px;
            font-size: 11px;
            font-weight: 700;
        }

        .badge-td {
            background: rgba(56, 189, 248, 0.2);
            color: #38BDF8;
        }

        .badge-assault {
            background: rgba(249, 115, 22, 0.2);
            color: #FB923C;
        }

        /* AI Prediction Sandbox */
        .sandbox-box {
            background: linear-gradient(135deg, rgba(30, 41, 59, 0.8), rgba(15, 23, 42, 0.9));
            border: 1px solid rgba(168, 85, 247, 0.35);
            border-radius: 16px;
            padding: 20px;
            margin-bottom: 24px;
        }

        .sandbox-controls {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
            gap: 14px;
            margin-top: 14px;
        }

        .control-group {
            display: flex;
            flex-direction: column;
            gap: 6px;
        }

        .control-group label {
            font-size: 12px;
            color: var(--text-secondary);
            font-weight: 600;
        }

        .control-group input {
            background: rgba(15, 23, 42, 0.8);
            border: 1px solid rgba(255, 255, 255, 0.15);
            border-radius: 8px;
            padding: 8px 12px;
            color: white;
            font-size: 13px;
        }

        .btn-predict {
            background: linear-gradient(135deg, #9333EA, #4F46E5);
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 10px;
            font-weight: 700;
            font-size: 14px;
            cursor: pointer;
            margin-top: 18px;
            transition: all 0.2s;
        }

        .btn-predict:hover {
            transform: translateY(-1px);
            box-shadow: 0 4px 16px rgba(147, 51, 234, 0.5);
        }

        .prediction-result {
            margin-top: 16px;
            background: rgba(15, 23, 42, 0.9);
            border: 1px solid rgba(16, 185, 129, 0.3);
            border-radius: 10px;
            padding: 14px 18px;
            display: none;
        }

        .rationale-text {
            color: #A7F3D0;
            font-size: 13px;
            line-height: 1.5;
            margin-top: 8px;
        }

        /* Progress Bars */
        .prob-bar-container {
            display: flex;
            align-items: center;
            gap: 10px;
            margin-top: 6px;
        }

        .prob-bar-bg {
            flex-grow: 1;
            height: 8px;
            background: rgba(255, 255, 255, 0.1);
            border-radius: 4px;
            overflow: hidden;
        }

        .prob-bar-fill {
            height: 100%;
            border-radius: 4px;
            transition: width 0.4s ease;
        }

        /* Footer */
        footer {
            text-align: center;
            padding: 24px 0 12px;
            color: var(--text-secondary);
            font-size: 12px;
            border-top: 1px solid rgba(255, 255, 255, 0.08);
            margin-top: 24px;
        }
    </style>
</head>
<body>
    <div class="container">
        <!-- Header -->
        <header>
            <div>
                <div class="brand-title">
                    🦕 DUAL TACTICS: CLASH OF ERAS
                </div>
                <div class="brand-subtitle">
                    IO Cloud Telemetry & Deep Learning AI Director Real-Time Dashboard
                </div>
            </div>
            <div class="header-controls">
                <div class="status-badge" id="backendStatusBadge">
                    <span class="pulse-dot"></span>
                    <span id="backendStatusText">CLOUD ONLINE</span>
                </div>
                <button class="btn-refresh" onclick="refreshAllData()">⟳ Làm Mới Dữ Liệu</button>
            </div>
        </header>

        <!-- KPI Grid -->
        <div class="kpi-grid">
            <div class="kpi-card">
                <div class="kpi-label">Tổng Lượt Trận Đã Đấu</div>
                <div class="kpi-value" id="kpiTotalBattles">0</div>
                <div class="kpi-sub">Bản ghi Telemetry lưu trong SQLite</div>
            </div>
            <div class="kpi-card">
                <div class="kpi-label">Tù Trưởng Đã Đăng Ký</div>
                <div class="kpi-value" id="kpiTotalAccounts">0</div>
                <div class="kpi-sub">Tài khoản Cloud đồng bộ đa thiết bị</div>
            </div>
            <div class="kpi-card">
                <div class="kpi-label">Đợt Wave Cao Nhất Đạt Được</div>
                <div class="kpi-value" id="kpiHighestWave">1</div>
                <div class="kpi-sub">Kỷ lục thử thách thủ thành</div>
            </div>
            <div class="kpi-card">
                <div class="kpi-label">AI Director Inference Latency</div>
                <div class="kpi-value" id="kpiLatency">0.05 <span style="font-size: 16px;">ms</span></div>
                <div class="kpi-sub">Mạng Nơ-ron 16→32→16 Multi-Head</div>
            </div>
        </div>

        <!-- Interactive AI Sandbox for Lecturer Demonstration -->
        <div class="sandbox-box">
            <div class="card-title">
                🧠 THỬ NGHIỆM SUY LUẬN MẠNG NƠ-RON AI DIRECTOR (INTERACTIVE SANDBOX)
            </div>
            <div style="font-size: 12px; color: var(--text-secondary); margin-top: 4px;">
                Thay đổi các thông số trận đấu bên dưới và bấm "Chạy Suy Luận" để xem AI tự động phân tích và đưa ra đội hình khủng long phản công theo thời gian thực!
            </div>
            <div class="sandbox-controls">
                <div class="control-group">
                    <label>Đợt (Wave):</label>
                    <input type="number" id="sbWave" value="5" min="1" max="25">
                </div>
                <div class="control-group">
                    <label>Số Vàng Hiện Có:</label>
                    <input type="number" id="sbGold" value="450" min="0" max="5000">
                </div>
                <div class="control-group">
                    <label>Máu Làng (%):</label>
                    <input type="number" id="sbBaseHp" value="85" min="1" max="100">
                </div>
                <div class="control-group">
                    <label>Số Tháp Nỏ (Ballistas):</label>
                    <input type="number" id="sbBallistas" value="2" min="0" max="10">
                </div>
                <div class="control-group">
                    <label>Số Máy Bắn Đá (Catapults):</label>
                    <input type="number" id="sbCatapults" value="1" min="0" max="10">
                </div>
                <div class="control-group">
                    <label>Số Tường Chắn (Barricades):</label>
                    <input type="number" id="sbWalls" value="4" min="0" max="20">
                </div>
            </div>
            <button class="btn-predict" onclick="runAiSandboxPrediction()">⚡ Chạy Suy Luận Mạng Nơ-ron AI</button>

            <div class="prediction-result" id="sandboxResult">
                <div style="font-weight: 700; color: #38BDF8; font-size: 14px;">🎯 KẾT QUẢ SUY LUẬN TỪ MÁY CHỦ:</div>
                <div class="rationale-text" id="sbRationale">Đang suy luận...</div>
                <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 14px; margin-top: 12px;">
                    <div>
                        <div style="font-size: 11px; color: #FB923C; font-weight: 700;">🦖 Velociraptor (Runner Swarm): <span id="sbProb0">25%</span></div>
                        <div class="prob-bar-container"><div class="prob-bar-bg"><div class="prob-bar-fill" id="sbFill0" style="background:#FB923C; width:25%;"></div></div></div>
                    </div>
                    <div>
                        <div style="font-size: 11px; color: #38BDF8; font-weight: 700;">🦅 Pterodactyl (Không Kích Flyer): <span id="sbProb1">25%</span></div>
                        <div class="prob-bar-container"><div class="prob-bar-bg"><div class="prob-bar-fill" id="sbFill1" style="background:#38BDF8; width:25%;"></div></div></div>
                    </div>
                    <div>
                        <div style="font-size: 11px; color: #4ADE80; font-weight: 700;">🛡️ Ankylosaurus (Phá Tường Tank): <span id="sbProb2">25%</span></div>
                        <div class="prob-bar-container"><div class="prob-bar-bg"><div class="prob-bar-fill" id="sbFill2" style="background:#4ADE80; width:25%;"></div></div></div>
                    </div>
                    <div>
                        <div style="font-size: 11px; color: #F43F5E; font-weight: 700;">👑 T-Rex Boss (Sát Thủ Apex): <span id="sbProb3">25%</span></div>
                        <div class="prob-bar-container"><div class="prob-bar-bg"><div class="prob-bar-fill" id="sbFill3" style="background:#F43F5E; width:25%;"></div></div></div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Charts Row -->
        <div class="charts-row">
            <div class="card">
                <div class="card-header">
                    <div class="card-title">📊 MẬT ĐỘ BỐ TRÍ PHÒNG THỦ THEO TỪNG ĐỢT CHIẾN ĐẤU</div>
                    <div style="font-size: 11px; color: var(--text-secondary);">Real-time Telemetry Stream</div>
                </div>
                <div class="chart-container">
                    <canvas id="defenseChart"></canvas>
                </div>
            </div>
            <div class="card">
                <div class="card-header">
                    <div class="card-title">🦖 TỶ TRỌNG ĐỘI HÌNH KHỦNG LONG AI</div>
                    <div style="font-size: 11px; color: var(--text-secondary);">Archetype Softmax</div>
                </div>
                <div class="chart-container">
                    <canvas id="archetypeChart"></canvas>
                </div>
            </div>
        </div>

        <!-- Tables Row -->
        <div class="grid-split">
            <!-- Telemetry Logs Table -->
            <div class="card">
                <div class="card-header">
                    <div class="card-title">📡 NHẬT KÝ CHIẾN TRẬN THỜI GIAN THỰC (BATTLE LOGS)</div>
                    <div style="font-size: 11px; color: var(--text-secondary);" id="recentLogsCount">10 trận gần nhất</div>
                </div>
                <div style="overflow-x: auto;">
                    <table>
                        <thead>
                            <tr>
                                <th>Thời Gian</th>
                                <th>Tù Trưởng / ID</th>
                                <th>Chế Độ</th>
                                <th>Đợt</th>
                                <th>Vàng</th>
                                <th>Máu Làng</th>
                                <th>Tháp / Bẫy Đã Dựng</th>
                            </tr>
                        </thead>
                        <tbody id="telemetryTableBody">
                            <tr><td colspan="7" style="text-align:center; padding: 20px;">Đang tải dữ liệu telemetry...</td></tr>
                        </tbody>
                    </table>
                </div>
            </div>

            <!-- Leaderboard Table -->
            <div class="card">
                <div class="card-header">
                    <div class="card-title">🏆 BẢNG XẾP HẠNG TOÀN CẦU (LEADERBOARD)</div>
                    <div style="font-size: 11px; color: var(--text-secondary);">Top Tù Trưởng</div>
                </div>
                <div style="overflow-x: auto;">
                    <table>
                        <thead>
                            <tr>
                                <th>#</th>
                                <th>Tù Trưởng</th>
                                <th>Điểm Số</th>
                                <th>Màn</th>
                                <th>Chế Độ</th>
                            </tr>
                        </thead>
                        <tbody id="leaderboardTableBody">
                            <tr><td colspan="5" style="text-align:center; padding: 20px;">Đang tải bảng xếp hạng...</td></tr>
                        </tbody>
                    </table>
                </div>
            </div>
        </div>

        <!-- Registered Cloud Accounts -->
        <div class="card" style="margin-bottom: 24px;">
            <div class="card-header">
                <div class="card-title">👤 DANH SÁCH TÀI KHOẢN TÙ TRƯỞNG ĐỒNG BỘ ĐÁM MÂY (CLOUD SAVES)</div>
                <div style="font-size: 11px; color: var(--text-secondary);">Hỗ trợ Cross-Device Play (PC & Mobile)</div>
            </div>
            <div style="overflow-x: auto;">
                <table>
                    <thead>
                        <tr>
                            <th>Tên Tài Khoản (Username)</th>
                            <th>Tên Hiển Thị (Chief Name)</th>
                            <th>Đợt Cao Nhất</th>
                            <th>Tài Nguyên (Vàng/Thịt)</th>
                            <th>Chế Độ Lưu</th>
                            <th>Thời Điểm Đồng Bộ Gần Nhất</th>
                        </tr>
                    </thead>
                    <tbody id="accountsTableBody">
                        <tr><td colspan="6" style="text-align:center; padding: 20px;">Đang tải danh sách tài khoản...</td></tr>
                    </tbody>
                </table>
            </div>
        </div>

        <!-- Footer -->
        <footer>
            <b>Đồ án: Adaptive Prehistoric Tower Defense 3D & AI Director</b><br>
            Hệ thống Tích Hợp Đám Mây (IO Cloud FastAPI) + Mạng Nơ-ron Sâu (Deep Learning MLP) + Tương Tác Thực Tế Ảo (AR Tabletop)
        </footer>
    </div>

    <!-- Scripts -->
    <script>
        let defenseChart = null;
        let archetypeChart = null;

        function initCharts() {
            const ctxDef = document.getElementById('defenseChart').getContext('2d');
            defenseChart = new Chart(ctxDef, {
                type: 'line',
                data: {
                    labels: ['Trận 1', 'Trận 2', 'Trận 3', 'Trận 4', 'Trận 5', 'Trận 6'],
                    datasets: [
                        {
                            label: 'Tháp Cung Thủ',
                            data: [2, 3, 3, 4, 4, 5],
                            borderColor: '#38BDF8',
                            backgroundColor: 'rgba(56, 189, 248, 0.1)',
                            tension: 0.3,
                            fill: true
                        },
                        {
                            label: 'Tháp Nỏ Sát Thương',
                            data: [0, 1, 1, 2, 2, 3],
                            borderColor: '#F59E0B',
                            backgroundColor: 'transparent',
                            tension: 0.3
                        },
                        {
                            label: 'Máy Bắn Đá Lan',
                            data: [0, 0, 1, 1, 2, 2],
                            borderColor: '#EF4444',
                            backgroundColor: 'transparent',
                            tension: 0.3
                        },
                        {
                            label: 'Bẫy Chông / Đất Sét',
                            data: [1, 2, 3, 4, 4, 5],
                            borderColor: '#10B981',
                            backgroundColor: 'transparent',
                            tension: 0.3
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { labels: { color: '#94A3B8', font: { size: 11 } } }
                    },
                    scales: {
                        x: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#94A3B8' } },
                        y: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#94A3B8' }, beginAtZero: true }
                    }
                }
            });

            const ctxArch = document.getElementById('archetypeChart').getContext('2d');
            archetypeChart = new Chart(ctxArch, {
                type: 'doughnut',
                data: {
                    labels: ['Velociraptor (Runner)', 'Pterodactyl (Flyer)', 'Ankylosaurus (Tank)', 'T-Rex (Boss)'],
                    datasets: [{
                        data: [35, 25, 30, 10],
                        backgroundColor: ['#FB923C', '#38BDF8', '#4ADE80', '#F43F5E'],
                        borderColor: '#0B0F19',
                        borderWidth: 2
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { position: 'bottom', labels: { color: '#94A3B8', font: { size: 10 } } },
                        tooltip: {
                            callbacks: {
                                label: function(context) {
                                    const val = context.parsed;
                                    return ` ${context.label}: ${isNaN(val) ? 0 : val}%`;
                                }
                            }
                        }
                    }
                }
            });
        }

        async function fetchTelemetrySummary() {
            try {
                const res = await fetch('/api/v1/cloud/telemetry/summary?limit=15');
                if (!res.ok) return;
                const data = await res.json();
                
                document.getElementById('kpiTotalBattles').innerText = data.total_records || 0;
                const tbody = document.getElementById('telemetryTableBody');
                tbody.innerHTML = '';

                if (!data.recent_logs || data.recent_logs.length === 0) {
                    tbody.innerHTML = '<tr><td colspan="7" style="text-align:center; padding: 20px; color:#64748B;">Chưa có dữ liệu trận đấu nào. Hãy chơi game để xem telemetry tự động đổ về!</td></tr>';
                    return;
                }

                const labels = [];
                const watchtowers = [];
                const ballistas = [];
                const catapults = [];
                const traps = [];

                data.recent_logs.slice(0, 10).reverse().forEach((log, idx) => {
                    labels.push(`Đợt ${log.round || (idx + 1)}`);
                    watchtowers.push(log.watchtowers || 0);
                    ballistas.push(log.ballistas || 0);
                    catapults.push(log.catapults || 0);
                    traps.push((log.spiketraps || 0) + (log.tarpits || 0));
                });

                if (defenseChart && labels.length > 0) {
                    defenseChart.data.labels = labels;
                    defenseChart.data.datasets[0].data = watchtowers;
                    defenseChart.data.datasets[1].data = ballistas;
                    defenseChart.data.datasets[2].data = catapults;
                    defenseChart.data.datasets[3].data = traps;
                    defenseChart.update();
                }

                data.recent_logs.forEach(log => {
                    const row = document.createElement('tr');
                    const d = log.logged_at ? new Date(log.logged_at * 1000).toLocaleTimeString('vi-VN') : '--:--';
                    const modeClass = log.game_mode === 'DinoAssault' ? 'badge-assault' : 'badge-td';
                    const modeName = log.game_mode === 'DinoAssault' ? 'Công Thành' : 'Thủ Thành';
                    
                    const towersDesc = `🏹 ${log.watchtowers || 0} Cung | 🎯 ${log.ballistas || 0} Nỏ | ☄️ ${log.catapults || 0} Pháo | 🪤 ${(log.spiketraps || 0) + (log.tarpits || 0)} Bẫy | 🧱 ${log.barricades || 0} Tường`;

                    row.innerHTML = `
                        <td>${d}</td>
                        <td style="font-weight:600; color:#38BDF8;">${escapeHtml(log.player_id || 'Tù Trưởng')}</td>
                        <td><span class="badge-mode ${modeClass}">${modeName}</span></td>
                        <td><b>Đợt ${log.round || 1}</b></td>
                        <td>💰 ${log.gold || 0}g</td>
                        <td>❤️ ${log.base_hp || 100}%</td>
                        <td style="font-size:12px;">${towersDesc}</td>
                    `;
                    tbody.appendChild(row);
                });
            } catch (err) {
                console.error("Telemetry fetch error:", err);
            }
        }

        async function fetchLeaderboard() {
            try {
                const res = await fetch('/api/v1/cloud/leaderboard?limit=10');
                if (!res.ok) return;
                const data = await res.json();
                const tbody = document.getElementById('leaderboardTableBody');
                tbody.innerHTML = '';

                if (!data.leaderboard || data.leaderboard.length === 0) {
                    tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; padding: 20px; color:#64748B;">Chưa có bảng xếp hạng.</td></tr>';
                    return;
                }

                let maxWave = 1;
                data.leaderboard.forEach((entry, idx) => {
                    const rank = idx + 1;
                    let rankBadge = rank === 1 ? '🥇 1' : (rank === 2 ? '🥈 2' : (rank === 3 ? '🥉 3' : rank));
                    if ((entry.wave_reached || 1) > maxWave) maxWave = entry.wave_reached;

                    const row = document.createElement('tr');
                    row.innerHTML = `
                        <td style="font-weight:800; color:${rank <= 3 ? '#F59E0B' : '#94A3B8'};">${rankBadge}</td>
                        <td style="font-weight:700; color:#F8FAFC;">${escapeHtml(entry.player_name || entry.player_id)}</td>
                        <td style="font-weight:700; color:#10B981;">${(entry.score || 0).toLocaleString()}</td>
                        <td>Đợt ${entry.wave_reached || 1}</td>
                        <td><span class="badge-mode ${entry.mode === 'DinoAssault' ? 'badge-assault' : 'badge-td'}">${entry.mode || 'Thủ Thành'}</span></td>
                    `;
                    tbody.appendChild(row);
                });

                document.getElementById('kpiHighestWave').innerText = maxWave;
            } catch (err) {
                console.error("Leaderboard fetch error:", err);
            }
        }

        async function fetchAccounts() {
            try {
                const res = await fetch('/api/v1/cloud/accounts');
                if (!res.ok) return;
                const data = await res.json();
                const tbody = document.getElementById('accountsTableBody');
                tbody.innerHTML = '';

                document.getElementById('kpiTotalAccounts').innerText = data.count || (data.accounts ? data.accounts.length : 0);

                if (!data.accounts || data.accounts.length === 0) {
                    tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; padding: 20px; color:#64748B;">Chưa có tài khoản tù trưởng nào được đăng ký.</td></tr>';
                    return;
                }

                data.accounts.forEach(acc => {
                    const row = document.createElement('tr');
                    const d = acc.last_saved ? new Date(acc.last_saved * 1000).toLocaleString('vi-VN') : (acc.last_login ? new Date(acc.last_login * 1000).toLocaleString('vi-VN') : 'Mới tạo');
                    row.innerHTML = `
                        <td style="font-weight:700; color:#A855F7;">${escapeHtml(acc.username)}</td>
                        <td style="font-weight:600; color:#E2E8F0;">${escapeHtml(acc.display_name || acc.player_name || acc.username)}</td>
                        <td><b style="color:#38BDF8;">Đợt ${acc.highest_wave || 1}</b></td>
                        <td>💰 ${acc.coins || 500}</td>
                        <td><span class="badge-mode badge-td">${acc.selected_mode || 'TowerDefense'}</span></td>
                        <td style="font-size:12px; color:#94A3B8;">${d}</td>
                    `;
                    tbody.appendChild(row);
                });
            } catch (err) {
                console.error("Accounts fetch error:", err);
            }
        }

        async function runAiSandboxPrediction() {
            const wave = parseInt(document.getElementById('sbWave').value) || 5;
            const gold = parseInt(document.getElementById('sbGold').value) || 450;
            const baseHp = parseInt(document.getElementById('sbBaseHp').value) || 85;
            const ballistas = parseInt(document.getElementById('sbBallistas').value) || 0;
            const catapults = parseInt(document.getElementById('sbCatapults').value) || 0;
            const walls = parseInt(document.getElementById('sbWalls').value) || 0;

            const payload = {
                player_id: "sandbox_lecturer_demo",
                round: wave,
                gold: gold,
                base_hp: baseHp,
                game_mode: "TowerDefense",
                watchtowers: 3,
                ballistas: ballistas,
                catapults: catapults,
                shamans: 0,
                tarpits: 1,
                spiketraps: 2,
                barricades: walls,
                avg_survival_sec: 8.5,
                defense_density: (walls + 2) / 2.0
            };

            const box = document.getElementById('sandboxResult');
            box.style.display = 'block';
            document.getElementById('sbRationale').innerText = "Đang gửi dữ liệu vào mạng nơ-ron...";

            try {
                const res = await fetch('/api/v1/ai/predict-wave', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                if (!res.ok) throw new Error("Server error");
                const pred = await res.json();

                document.getElementById('sbRationale').innerHTML = `<b>Lý lẽ chiến thuật của AI:</b> "${pred.tactical_rationale}" (Quy mô đợt đề xuất: <b>${pred.recommended_wave_size} quái</b>, Điểm sơ hở: <b>${(pred.vulnerability_score * 100).toFixed(0)}%</b>)`;

                const aw = pred.archetype_weights || {};
                let p0, p1, p2, p3;
                if (Array.isArray(aw)) {
                    p0 = Math.round((Number(aw[0]) || 0.25) * 100);
                    p1 = Math.round((Number(aw[1]) || 0.25) * 100);
                    p2 = Math.round((Number(aw[2]) || 0.25) * 100);
                    p3 = Math.round((Number(aw[3]) || 0.25) * 100);
                } else if (typeof aw === 'object' && aw !== null) {
                    p0 = Math.round((Number(aw.Velociraptor_Runner ?? aw.runner ?? aw['0']) || 0.25) * 100);
                    p1 = Math.round((Number(aw.Pterodactyl_Flyer ?? aw.flyer ?? aw['1']) || 0.25) * 100);
                    p2 = Math.round((Number(aw.Ankylosaurus_Tank ?? aw.tank ?? aw['2']) || 0.25) * 100);
                    p3 = Math.round((Number(aw.TRex_Boss ?? aw.boss ?? aw['3']) || 0) * 100);
                } else {
                    p0 = 35; p1 = 25; p2 = 30; p3 = 10;
                }
                if (isNaN(p0) || p0 < 0) p0 = 25;
                if (isNaN(p1) || p1 < 0) p1 = 25;
                if (isNaN(p2) || p2 < 0) p2 = 25;
                if (isNaN(p3) || p3 < 0) p3 = 0;

                const totalP = p0 + p1 + p2 + p3;
                if (totalP > 0 && totalP !== 100) {
                    p0 = Math.round((p0 / totalP) * 100);
                    p1 = Math.round((p1 / totalP) * 100);
                    p2 = Math.round((p2 / totalP) * 100);
                    p3 = Math.max(0, 100 - p0 - p1 - p2);
                }

                document.getElementById('sbProb0').innerText = p0 + '%';
                document.getElementById('sbFill0').style.width = p0 + '%';

                document.getElementById('sbProb1').innerText = p1 + '%';
                document.getElementById('sbFill1').style.width = p1 + '%';

                document.getElementById('sbProb2').innerText = p2 + '%';
                document.getElementById('sbFill2').style.width = p2 + '%';

                document.getElementById('sbProb3').innerText = p3 + '%';
                document.getElementById('sbFill3').style.width = p3 + '%';

                if (archetypeChart) {
                    archetypeChart.data.datasets[0].data = [p0, p1, p2, p3];
                    archetypeChart.update();
                }
            } catch (err) {
                document.getElementById('sbRationale').innerText = "Lỗi khi gọi API AI: " + err.message;
            }
        }

        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        function refreshAllData() {
            fetchTelemetrySummary();
            fetchLeaderboard();
            fetchAccounts();
        }

        window.onload = function() {
            initCharts();
            refreshAllData();
            // Tự động kích hoạt dự báo mẫu để biểu đồ Tỷ Trọng Khủng Long AI hiển thị trực quan ngay lập tức
            runAiSandboxPrediction();
            // Auto refresh every 6 seconds
            setInterval(refreshAllData, 6000);
        };
    </script>
</body>
</html>
"""
