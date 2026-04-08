#!/bin/bash

# Development helper script for Gymnastics Platform

set -e

case "$1" in
  up)
    echo "🚀 Starting infrastructure services..."
    docker compose -f docker-compose.dev.yml up -d
    echo ""
    echo "✅ Infrastructure started!"
    echo ""
    echo "📋 Next steps:"
    echo "   1. Run API:           dotnet run --project src/GymnasticsPlatform.Api"
    echo "   2. Run User Portal:   cd frontend/user-portal && npm install && npm run dev"
    echo "   3. Run Admin Portal:  cd frontend/admin-portal && npm install && npm run dev"
    echo ""
    echo "🌐 Services:"
    echo "   API:              http://localhost:5001"
    echo "   User Portal:      http://localhost:3001"
    echo "   Admin Portal:     http://localhost:3002"
    echo "   Database Admin:   http://localhost:8081"
    echo "   MailHog:          http://localhost:8025"
    echo "   Grafana:          http://localhost:3000"
    ;;

  down)
    echo "🛑 Stopping infrastructure services..."
    docker compose -f docker-compose.dev.yml down
    echo "✅ Infrastructure stopped!"
    ;;

  restart)
    echo "🔄 Restarting infrastructure services..."
    docker compose -f docker-compose.dev.yml restart
    echo "✅ Infrastructure restarted!"
    ;;

  logs)
    docker compose -f docker-compose.dev.yml logs -f "${@:2}"
    ;;

  ps)
    docker compose -f docker-compose.dev.yml ps
    ;;

  clean)
    echo "⚠️  This will remove all volumes and data!"
    read -p "Are you sure? (yes/no): " confirm
    if [ "$confirm" = "yes" ]; then
      docker compose -f docker-compose.dev.yml down -v
      echo "✅ All volumes cleaned!"
    else
      echo "❌ Cancelled"
    fi
    ;;

  api)
    echo "🚀 Starting API..."
    dotnet run --project src/GymnasticsPlatform.Api
    ;;

  user-portal)
    echo "🚀 Starting User Portal..."
    cd frontend/user-portal
    npm install
    npm run dev
    ;;

  admin-portal)
    echo "🚀 Starting Admin Portal..."
    cd frontend/admin-portal
    npm install
    npm run dev
    ;;

  test)
    echo "🧪 Running all tests..."
    dotnet test
    ;;

  *)
    echo "Gymnastics Platform - Development Helper"
    echo ""
    echo "Usage: ./dev.sh [command]"
    echo ""
    echo "Commands:"
    echo "  up              Start infrastructure (Postgres, observability, etc.)"
    echo "  down            Stop infrastructure"
    echo "  restart         Restart infrastructure"
    echo "  logs [service]  View logs (optional: specify service name)"
    echo "  ps              Show running services"
    echo "  clean           Remove all volumes and data"
    echo ""
    echo "  api             Run backend API locally"
    echo "  user-portal     Run user portal locally"
    echo "  admin-portal    Run admin portal locally"
    echo "  test            Run all backend tests"
    echo ""
    echo "Examples:"
    echo "  ./dev.sh up              # Start infrastructure"
    echo "  ./dev.sh api             # Run API in another terminal"
    echo "  ./dev.sh logs postgres   # View PostgreSQL logs"
    echo "  ./dev.sh down            # Stop everything"
    ;;
esac
