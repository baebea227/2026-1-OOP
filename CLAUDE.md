# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A 3D Unity co-op puzzle game for 2 players.

## Networking

Photon Fusion 2, **Host 모드** (`FusionBootstrap.AutoStartAs = Host`).

- 호스트가 모든 NetworkObject의 StateAuthority 보유
- 클라는 자기 폰의 InputAuthority만 보유, `FixedUpdateNetwork`는 예측으로 실행되고 호스트 상태 도착 시 롤백
- `[Networked]` 변수 쓰기는 호스트 기준이 권위, 클라 측 쓰기는 예측이므로 권한 체크 누락 시 롤백되며 사라짐
- 권한 변경 / 상태 변경은 `HasStateAuthority` 분기 + RPC(`RpcTargets.StateAuthority`) 패턴 사용

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
