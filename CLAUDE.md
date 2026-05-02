# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A 3D Unity co-op puzzle game for 2 players.

## Common Commands

Unity has no CLI build/test system here. Work is done inside the Unity Editor. From the terminal you can:

```powershell
# Open the project in Unity Hub (adjust Unity version path as needed)
# Unity does not expose a test-runner CLI for this project configuration

# Git workflow (feature branches → main via PR)
git checkout -b <branch>
git push origin <branch>
# Then open a PR to main
```