#!/bin/bash

# Azure App Service startup script for Node.js React app
echo "🚀 Starting Member Property Alert UI..."
echo "📁 Working directory: $(pwd)"
echo "📋 Directory contents:"
ls -la

echo "🔍 Checking Node.js environment:"
echo "  Node version: $(node --version)"
echo "  NPM version: $(npm --version)"

echo "📦 Checking dependencies:"
if [ -f "package.json" ]; then
    echo "✅ package.json found"
    echo "📄 Package.json content:"
    cat package.json
else
    echo "❌ package.json not found"
    exit 1
fi

if [ -d "node_modules" ]; then
    echo "✅ node_modules directory found"
    echo "📋 Installed packages:"
    ls node_modules/ | head -10
else
    echo "⚠️ node_modules not found, installing dependencies..."
    npm install --production
fi

echo "🌐 Checking build directory:"
if [ -d "build" ]; then
    echo "✅ Build directory found"
    ls -la build/ | head -5
else
    echo "❌ Build directory not found"
    exit 1
fi

echo "🎯 Starting Express server..."
export PORT=${PORT:-8080}
echo "🔌 Using port: $PORT"

exec node server.js
