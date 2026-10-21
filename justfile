# List recipes
default:
    @just --list

# Install frontend deps and restore backend packages
setup:
    cd backend && dotnet restore
    cd frontend && npm install

# Run backend (watch) and frontend dev server together; Ctrl-C stops both
dev:
    #!/usr/bin/env bash
    trap 'kill 0' EXIT
    (cd backend && dotnet watch --project BouvetBike --no-hot-reload) &
    (cd frontend && npm run dev) &
    wait

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
