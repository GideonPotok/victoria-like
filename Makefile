.PHONY: help up down logs test-db test-redis test-connections clean \
	kill-server reset-db reset-snapshots reset-world run-albion run-tiny run-medium

REPO_ROOT := $(abspath $(dir $(lastword $(MAKEFILE_LIST))))
SERVER_PROJECT := $(REPO_ROOT)/server/src/VictoriaLike.Server
SNAPSHOT_DIR := $(SERVER_PROJECT)/bin/Debug/net10.0/snapshots
SCENARIO_DIR := $(REPO_ROOT)/server/content/scenarios
TINY_SCENARIO := $(SCENARIO_DIR)/tiny-2country.json
ALBION_SCENARIO := $(SCENARIO_DIR)/phase1-albion-server.json
MEDIUM_SCENARIO := $(SCENARIO_DIR)/medium-8country.json
SERVER_PORT ?= 5001

help:
	@echo "Victoria-Like Local Development"
	@echo ""
	@echo "Infrastructure:"
	@echo "  make up               - Start PostgreSQL and Redis containers"
	@echo "  make down             - Stop and remove containers"
	@echo "  make logs             - View container logs"
	@echo "  make test-db          - Test database connectivity"
	@echo "  make test-redis       - Test Redis connectivity"
	@echo "  make test-connections - Test both database and Redis"
	@echo "  make clean            - Remove all containers and volumes"
	@echo ""
	@echo "Server resilience helpers:"
	@echo "  make kill-server      - Kill any process bound to port \$$SERVER_PORT (default 5001)"
	@echo "  make reset-db         - Drop and recreate the victoria_world database"
	@echo "  make reset-snapshots  - Delete server snapshot directory"
	@echo "  make reset-world      - kill-server + reset-db + reset-snapshots"
	@echo "  make run-albion       - reset-world then run server with phase1-albion-server scenario"
	@echo "  make run-tiny         - reset-world then run server with tiny-2country scenario"
	@echo "  make run-medium       - reset-world then run server with medium-8country scenario"
	@echo ""

up:
	@echo "Starting PostgreSQL and Redis..."
	docker-compose up -d
	@echo ""
	@echo "Waiting for services to be healthy..."
	@sleep 2
	@docker-compose ps
	@echo ""
	@echo "Services started. Connection strings:"
	@echo "  PostgreSQL: postgresql://victoria:victoria_dev_password@localhost:5432/victoria_world"
	@echo "  Redis:      redis://localhost:6379"

down:
	@echo "Stopping services..."
	docker-compose down

logs:
	docker-compose logs -f

test-db:
	@echo "Testing PostgreSQL connection..."
	@command -v psql >/dev/null 2>&1 || { echo "psql not found. Install PostgreSQL client."; exit 1; }
	PGPASSWORD=victoria_dev_password psql -h localhost -U victoria -d victoria_world -c "SELECT 'PostgreSQL is reachable' as status;"

test-redis:
	@echo "Testing Redis connection..."
	@command -v redis-cli >/dev/null 2>&1 || { echo "redis-cli not found. Install Redis client."; exit 1; }
	redis-cli -h localhost -p 6379 PING

test-connections: test-db test-redis
	@echo ""
	@echo "✓ Both services are reachable"

clean:
	@echo "Cleaning up containers and volumes..."
	docker-compose down -v
	@echo "Cleanup complete"

kill-server:
	@pids=$$(lsof -ti :$(SERVER_PORT) 2>/dev/null); \
	if [ -z "$$pids" ]; then \
		echo "No process listening on :$(SERVER_PORT)"; \
	else \
		echo "Killing process(es) on :$(SERVER_PORT): $$pids"; \
		kill $$pids 2>/dev/null || true; \
		sleep 1; \
		pids=$$(lsof -ti :$(SERVER_PORT) 2>/dev/null); \
		if [ -n "$$pids" ]; then \
			echo "Forcing kill -9 on: $$pids"; \
			kill -9 $$pids 2>/dev/null || true; \
		fi; \
		echo "Port :$(SERVER_PORT) is free."; \
	fi

reset-db:
	@echo "Dropping and recreating victoria_world database..."
	@docker exec victoria-postgres psql -U victoria -d postgres -c "DROP DATABASE IF EXISTS victoria_world;" >/dev/null
	@docker exec victoria-postgres psql -U victoria -d postgres -c "CREATE DATABASE victoria_world OWNER victoria;" >/dev/null
	@echo "✓ victoria_world recreated."

reset-snapshots:
	@if [ -d "$(SNAPSHOT_DIR)" ]; then \
		echo "Removing $(SNAPSHOT_DIR)"; \
		rm -rf "$(SNAPSHOT_DIR)"; \
	else \
		echo "No snapshot directory at $(SNAPSHOT_DIR)"; \
	fi

reset-world: kill-server reset-db reset-snapshots
	@echo "World reset complete. Run 'make run-albion', 'make run-tiny', or 'make run-medium' to seed a scenario."

run-albion: reset-world
	@echo "Starting server with scenario: $(ALBION_SCENARIO)"
	World__ScenarioPath=$(ALBION_SCENARIO) dotnet run --project $(SERVER_PROJECT)

run-tiny: reset-world
	@echo "Starting server with scenario: $(TINY_SCENARIO)"
	World__ScenarioPath=$(TINY_SCENARIO) dotnet run --project $(SERVER_PROJECT)

run-medium: reset-world
	@echo "Starting server with scenario: $(MEDIUM_SCENARIO)"
	World__ScenarioPath=$(MEDIUM_SCENARIO) dotnet run --project $(SERVER_PROJECT)
