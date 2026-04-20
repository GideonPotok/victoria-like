#!/bin/bash
# Victoria-Like local development startup script

set -e

echo "🚀 Victoria-Like Local Environment Startup"
echo ""

# Check Docker
if ! command -v docker &> /dev/null; then
    echo "❌ Docker not found. Please install Docker and Docker Compose."
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    echo "❌ Docker Compose not found. Please install Docker Compose."
    exit 1
fi

# Start services
echo "Starting PostgreSQL and Redis..."
docker-compose up -d

# Wait for services
echo "Waiting for services to be healthy..."
sleep 3

# Check status
if docker-compose ps | grep -q "Up"; then
    echo ""
    echo "✅ Services started successfully"
    echo ""
    docker-compose ps
    echo ""
    echo "Connection strings:"
    echo "  PostgreSQL: postgresql://victoria:victoria_dev_password@localhost:5432/victoria_world"
    echo "  Redis:      redis://localhost:6379"
    echo ""
    echo "To view logs:     docker-compose logs -f"
    echo "To stop:          docker-compose down"
else
    echo "❌ Services failed to start"
    docker-compose logs
    exit 1
fi
