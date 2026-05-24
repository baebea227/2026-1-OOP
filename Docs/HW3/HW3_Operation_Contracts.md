---
marp: true
size: 16:9
paginate: false
style: |
  section {
    padding: 24px 28px;
    background: #ffffff;
    color: #1f2933;
  }
  h1 {
    font-size: 1.05em;
    color: #111827;
    margin: 0 0 12px 0;
    font-weight: 700;
    letter-spacing: 0;
  }
  table {
    width: 100%;
    border-collapse: collapse;
    table-layout: fixed;
    font-size: 0.50em;
    line-height: 1.38;
  }
  th {
    background: #eef2f7;
    color: #111827;
    text-align: center;
    font-weight: 700;
  }
  td {
    vertical-align: top;
    color: #1f2933;
  }
  th, td {
    border: 1px solid #cbd5e1;
    padding: 7px 8px;
    word-break: keep-all;
    overflow-wrap: anywhere;
  }
  table td:first-child {
    width: 24%;
    white-space: nowrap;
  }
  table td:nth-child(2) {
    width: 26%;
  }
  table td:nth-child(3),
  table td:nth-child(4) {
    width: 25%;
  }
  code {
    font-size: 0.93em;
    color: #0f172a;
    background: #f8fafc;
    padding: 1px 3px;
    border-radius: 3px;
  }
---

| System Operation | Responsibility | Pre-condition | Post-condition |
|---|---|---|---|
| `createRoom(roomName, maxPlayers, isPublic)` | 플레이어 1이 새 방 생성을 요청하고 대기실 입장을 시작한다. | 플레이어 1이 로비에 있으며 방 생성에 필요한 값을 입력했다. | 방 생성 성공 시 `enterWaitingRoom(roomId)`가 반환된다. 실패 시 `showCreateRoomError(reason)`가 반환된다. |
| `joinRoom(roomId)` | 플레이어 2가 기존 방 참가를 요청하고 대기실 입장을 시작한다. | 플레이어 2가 로비에 있으며 참가할 `roomId`를 입력했다. | 참가 성공 시 `showWaitingRoom(roomInfo)`가 반환된다. 실패 시 `joinRoomRejected(reason)`가 반환된다. |
| `sendChatMessage(message)` | 대기실에서 플레이어 메시지를 같은 방 참가자에게 전달한다. | 플레이어가 방에 입장해 있고 채팅 입력이 가능하다. | 메시지가 플레이어 1, 플레이어 2의 채팅 화면에 표시된다. 처리할 수 없으면 요청자에게 거절 결과가 반환된다. |
| `setReady(playerId, isReady)` | 각 플레이어의 준비 상태를 대기실 상태에 반영한다. | 플레이어가 대기실에 입장해 있다. | 해당 플레이어의 준비 상태가 갱신되고 대기실 참가자에게 공유된다. |
| `requestStartStage()` | 플레이어 1이 스테이지 시작을 요청한다. | 플레이어 1이 방장 역할이며 스테이지를 시작할 수 있는 대기실 상태이다. | 시작 가능하면 두 플레이어에게 `loadStage(stageId)`가 전달된다. 시작할 수 없으면 플레이어 1에게 `startStageRejected(reason)`가 반환된다. |
| `enterStage(stageId)` | 플레이어가 스테이지 입장을 요청하고 게임 진행 상태로 전환한다. | 스테이지 시작 요청이 수락되었고 `stageId`가 정해져 있다. | 입장 성공 시 `stageReady(stageId)`가 반환된다. 실패 시 `returnToLobbyWithError(reason)`가 반환된다. |

---

| System Operation | Responsibility | Pre-condition | Post-condition |
|---|---|---|---|
| `inputMovement()` | 플레이어 이동 입력을 스테이지 상태에 반영한다. | 스테이지가 진행 중이고 플레이어 조작이 가능한 상태이다. | 플레이어 위치와 화면에 반영될 상태가 `updateStageState(result)`로 갱신된다. |
| `requestGrab()`<br>`operateLever()`<br>`toggleStageSettingsMenu()` | 플레이어 1의 오브젝트 잡기, 레버 조작, 설정 메뉴 요청을 처리한다. | 플레이어 1이 스테이지 안에 있으며 대상 오브젝트나 UI를 조작할 수 있다. | 오브젝트, 구조물, 메뉴 상태가 변경되고 결과가 `updateStageState(result)`로 반환된다. |
| `pushObject()`<br>`operateButton()`<br>`stepOnPressurePlate()` | 플레이어 2의 협동 밀기, 버튼 조작, 발판 활성화 요청을 처리한다. | 플레이어 2가 스테이지 안에 있으며 협동 기믹 수행 위치에 있다. | 협동 오브젝트, 다리, 최종 발판 상태가 변경되고 결과가 `updateStageState(result)`로 반환된다. |
| `enterExitArea()` | 플레이어의 출구 진입을 기록하고 스테이지 클리어 결과를 반환한다. | 출구가 열려 있으며 플레이어가 출구 영역에 진입했다. | 두 플레이어가 모두 진입하면 `stageCleared()`가 반환된다. 일부만 진입한 경우 `waitForPartner()`가 반환된다. |

---

| System Operation | Responsibility | Pre-condition | Post-condition |
| --- | --- | --- | --- |
| `enterStage()` | 스테이지를 선택해 해당하는 스테이지로 입장한다. | 스테이지가 로딩되기 전 스테이지 선택 화면을 마주한다. | 선택한 스테이지에 해당하는 맵이 씬에 로딩된다. |
| `operateLever(leverId)` | 레버를 작동해 구조물 오브젝트의 활성화/비활성화 상태를 전환한다. | 플레이어와 레버 오브젝트가 생성되어 있다. 비활성화되어있는 구조물 오브젝트가 존재한다. | 비활성화되어있던 구조물 오브젝트가 활성화된다. 활성화 상태였을 경우 비활성화된다. |
| `stepOnPressurePlate(plateId)` | 플레이어가 발판 위로 올라가 발판을 활성화하고, 플랫폼 오브젝트가 움직이게 한다. | 플레이어와 발판 오브젝트, 플랫폼 오브젝트가 생성되어 있다. 플랫폼 오브젝트는 멈춰 있는 상태다. | 발판이 활성화된 동안 플랫폼 오브젝트가 지속적으로 움직이기 시작한다. |
| `enterExit()` | 스테이지 클리어 기믹을 수행하면 클리어 절차가 시작된다. | 클리어 조건으로 지정된 기믹을 성공적으로 수행한다. | 관련 매니저가 스테이지 클리어 절차를 수행한다. (UI 띄우기, 스테이지에 효과 띄우기 등...) |