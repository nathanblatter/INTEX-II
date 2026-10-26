#!/bin/bash
# Nightly ML retraining — mirrors .github/workflows/ml_retrain.yml
# Runs train_all.py against the local kateri DB, then commits + pushes updated models.

set -euo pipefail

PROJECT="/Users/nathanblatter/Desktop/INTEX-II"
VENV="$PROJECT/.venv"
LOG_DIR="$PROJECT/is455/ml-pipelines/logs"
LOG="$LOG_DIR/retrain-$(date -u +%Y-%m-%d).log"

mkdir -p "$LOG_DIR"

exec >> "$LOG" 2>&1
echo "========================================"
echo "  ML Retrain started: $(date -u)"
echo "========================================"

# Create venv on first run
if [ ! -d "$VENV" ]; then
    echo "Creating virtual environment..."
    python3 -m venv "$VENV"
fi

source "$VENV/bin/activate"

echo "Installing/updating dependencies..."
pip install -q --upgrade pip
pip install -q -r "$PROJECT/is455/requirements.txt"

echo "Running train_all.py..."
export PGHOST=localhost
export PGPORT=5432
export PGDATABASE=kateri
export PGUSER=postgres
export PGPASSWORD=postgres
export PGSSLMODE=disable

python "$PROJECT/is455/ml-pipelines/scripts/train_all.py"

echo "Committing updated models..."
cd "$PROJECT"
git config user.name  "retrain-bot"
git config user.email "retrain-bot@localhost"
git add is455/ml-pipelines/models/
if git diff --staged --quiet; then
    echo "No model changes — skipping commit."
else
    git commit -m "chore: nightly model retrain $(date -u +%Y-%m-%d)"
fi

echo "Restarting app container to load fresh models..."
docker restart intex-ii-app-1 && echo "Container restarted." || echo "WARNING: container restart failed."

echo "  Retrain complete: $(date -u)"
echo "========================================"
