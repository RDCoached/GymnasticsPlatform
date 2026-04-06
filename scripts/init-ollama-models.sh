#!/bin/bash
set -e

echo "Initializing Ollama models..."

OLLAMA_CONTAINER="gymnastics-ollama"

# Wait for Ollama to be ready
echo "Waiting for Ollama to be ready..."
until docker exec "${OLLAMA_CONTAINER}" curl -f -s http://localhost:11434/api/tags > /dev/null 2>&1; do
    echo "  Ollama is unavailable - sleeping"
    sleep 2
done
echo "Ollama is ready!"

# Pull embedding model (all-minilm:l6-v2)
echo "Pulling embedding model (all-minilm:l6-v2)..."
docker exec "${OLLAMA_CONTAINER}" ollama pull all-minilm:l6-v2
echo "Embedding model pulled successfully!"

# Pull generation model (llama3.2:3b)
echo "Pulling generation model (llama3.2:3b)..."
docker exec "${OLLAMA_CONTAINER}" ollama pull llama3.2:3b
echo "Generation model pulled successfully!"

echo ""
echo "Ollama initialization complete!"
echo "Available models:"
docker exec "${OLLAMA_CONTAINER}" ollama list
