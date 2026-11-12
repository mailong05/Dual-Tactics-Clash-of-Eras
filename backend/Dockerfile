# =========================================================
# Dockerfile for Dual Tactics: Clash of Eras - IO Cloud & AI Director
# Compatible with Local Docker Compose AND Cloud PaaS (Render / Railway / Fly.io)
# =========================================================
FROM python:3.10-slim

# Prevent Python from writing .pyc files and enable unbuffered logging
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1
ENV PORT=8000

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Install Python requirements and lightweight CPU PyTorch
COPY requirements.txt .
RUN pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir torch --index-url https://download.pytorch.org/whl/cpu && \
    pip install --no-cache-dir -r requirements.txt

# Copy source code and model weights
COPY . .

# Expose default API port
EXPOSE 8000

# Docker Healthcheck probe
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD sh -c "curl -f http://localhost:${PORT:-8000}/health || exit 1"

# Start Uvicorn ASGI server respecting dynamic $PORT injected by Cloud hosts
CMD ["sh", "-c", "uvicorn app:app --host 0.0.0.0 --port ${PORT:-8000}"]
