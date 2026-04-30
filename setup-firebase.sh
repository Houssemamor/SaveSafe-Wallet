#!/bin/bash

# Firebase Setup Script for SaveSafe Wallet
# This script helps you configure Firebase for Google Authentication

echo "Firebase Setup Script for SaveSafe Wallet"
echo "============================================"
echo ""

# Check if .env.example exists
if [ ! -f "src/frontend/.env.example" ]; then
    echo "Error: .env.example not found"
    exit 1
fi

# Create .env file if it doesn't exist
if [ ! -f "src/frontend/.env" ]; then
    echo "Creating .env file from .env.example..."
    cp src/frontend/.env.example src/frontend/.env
    echo ".env file created"
else
    echo ".env file already exists"
fi

# Create environment files if they don't exist
if [ ! -f "src/frontend/src/environments/environment.ts" ]; then
    echo "Creating environment.ts from template..."
    cp src/frontend/src/environments/environment.template.ts src/frontend/src/environments/environment.ts
    echo "environment.ts created"
else
    echo "environment.ts already exists"
fi

if [ ! -f "src/frontend/src/environments/environment.prod.ts" ]; then
    echo "Creating environment.prod.ts from template..."
    cp src/frontend/src/environments/environment.prod.template.ts src/frontend/src/environments/environment.prod.ts
    echo "environment.prod.ts created"
else
    echo "environment.prod.ts already exists"
fi

echo ""
echo "Next Steps:"
echo "1. Go to Firebase Console: https://console.firebase.google.com/"
echo "2. Create a new project or select existing one"
echo "3. Enable Google Authentication in: Authentication → Sign-in method"
echo "4. Add authorized domains: localhost, 127.0.0.1"
echo "5. Get your Firebase config from: Project Settings → General → Your apps → Web app"
echo "6. Update src/frontend/.env with your Firebase credentials"
echo "7. Update src/frontend/src/environments/environment.ts with your Firebase credentials"
echo ""
echo "Security Notes:"
echo "- Never commit .env files or environment files with real credentials"
echo "- Use different Firebase projects for development and production"
echo "- Enable Firebase security rules for database and storage"
echo ""
echo "Setup complete! Please update your Firebase credentials in the .env file."