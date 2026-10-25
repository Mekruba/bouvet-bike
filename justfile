# List recipes
default:
    @just --list

# Install frontend deps and restore backend packages
setup:
    cd backend && dotnet restore
    cd frontend && npm install

# Like dev, but a new database gets sample orders (an existing one is left alone; just reset-db first to start fresh)
dev-sample:
    SeedSampleData=true just dev

# Run backend (watch) and frontend dev server together; Ctrl-C stops both
dev:
    #!/usr/bin/env bash
    # dotnet watch must run in the foreground: it ignores SIGTERM and stops only on Ctrl-C, which bash hides
    # from background jobs. In the background it outlived this recipe and kept port 5080 busy.
    trap 'trap "" TERM; kill 0' EXIT
    (cd frontend && npm run dev) &
    cd backend && dotnet watch --project BouvetBike --no-hot-reload

backend:
    cd backend && dotnet watch --project BouvetBike --no-hot-reload

frontend:
    cd frontend && npm run dev

# Run all tests
test:
    cd backend && dotnet test
    cd frontend && npm test

# Delete the local SQLite database (schema is recreated on next start)
reset-db:
    rm -f backend/BouvetBike/bouvetbike.db*

# Set the admin account seeded on startup (stored in user secrets, not in the repo), then restart the backend
admin email password:
    cd backend && dotnet user-secrets set --project BouvetBike "Admin:Email" "{{email}}"
    cd backend && dotnet user-secrets set --project BouvetBike "Admin:Password" "{{password}}"

# Build and start the app in Docker on http://localhost:8080 (needs .env, see .env.example)
up:
    docker compose up --build -d

# Stop the Docker app (the data volume is kept)
down:
    docker compose down
