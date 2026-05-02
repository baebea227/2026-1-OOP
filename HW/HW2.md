---
marp: true
style: |
  section {
    padding: 40px 60px;
  }
  h1 {
    font-size: 1.8em;
    color: #b7410e;
    margin-bottom: 0.5em;
  }
  table {
    width: 100%;
    font-size: 0.70em;
    line-height: 1.5;
  }
  th {
    background-color: #f4f4f4;
    text-align: center;
    display: none;
  }
  td {
    vertical-align: top;
  }
  table td:first-child { white-space: nowrap }

  .actor-box h3, .system-box h3, .exception-box h3 { 
  margin-top: 0; 
  font-size: 0.9em; 
  }

  .actor-box p, .actor-box li, 
  .system-box p, .system-box li, 
  .exception-box p, .exception-box li { 
    font-size: 0.7em; 
    line-height: 1.6; 
  }

  .actor-box ul, .system-box ul, .exception-box ul { 
    font-size: 0.7em; 
    margin: 0; 
    padding-left: 20px; 
  }
  .columns {
    display: flex;
    gap: 30px;
    margin-bottom: 25px;
  }
  .actor-box {
    flex: 1;
    background-color: #eef2f5;
    padding: 20px;
    border-radius: 10px;
    border-top: 5px solid #; /* 파란색 포인트 */
  }
  .system-box {
    flex: 1;
    background-color: #eef2f5;
    padding: 20px;
    border-radius: 10px;
    border-top: 5px solid #; /* 녹색 포인트 */
  }
  .exception-box {
    background-color: #eef2f5;
    padding: 20px;
    border-radius: 10px;
    border-left: 5px solid #; /* 노란색 포인트 */
  }
  

---

# HW2
### It maybe takes two?

20223085 배상혁
20220611 이규원
20223104 양준영

---

# It maybe takes two?
| | | | |
| :---: | :---: | :---: | :---: |
| 이름 | **배상혁**<br>20223085 | **이규원**<br>20220611 | **양준영**<br>20223104 |
| 업무 경험 | 게임소프트웨어에서 Unity, GitHub를 사용한 애자일 방식으로 4인 프로젝트 완성 후 배포한 경험 | 게임 소프트웨어에서 팀장을 맡아 애자일 및 칸반 형식을 사용한 4인 팀 게임 프로젝트 경험 | 소프트웨어프로젝트2에서 4인 팀 프로젝트로 GitHub, Pygame을 이용한 게임 개발 경험 |
| 강점 | 어떻게든 문제를 해결해내는 끈기 | 한번 맡은 일은 확실하게 처리하고 끝낸다는 책임감 | 문제 해결을 위해 자료를 찾고 선별하는 능력 |
| 역할 | **플레이어 구현 및 UI, 씬 관리** | **게임에서의 멀티 구현 및 전반적인 서버 담당** | **스테이지 기믹 및 상호작용 가능한 물체 구현** |

---

# Vision
- **Unity & Photon**: 안정적이고 끊김 없는 2인 멀티플레이 환경 구축
- **Co-op Puzzle**: 유기적인 협동이 필요한 창의적인 퍼즐 레벨 디자인

---

# Scope
* **핵심 시스템 설계 및 구현**
  * 캐릭터 컨트롤: 3D 기반 이동, 점프 및 물리 기반 조작
  * 퍼즐 기믹: 오브젝트 상호작용(잡기, 밀기) 및 트리거 시스템
  * UI/UX: 멀티플레이 로비, 서버 접속 및 인게임 인터페이스

* **멀티플레이어 환경 (2인 협동)**
  * Photon 엔진: 실시간 동기화(위치, 애니메이션, 오브젝트 상태)
  * 협동 메커니즘: 2인 협력이 필수적인 퍼즐 로직 구현
  * 게임 흐름: [접속 → 매칭 → 스테이지 진행 → 클리어] 프로세스 구축

---

# Use Case: 플레이어, UI
담당: 배상혁

---

# Use Case 1: 플레이어 이동 (1/2)

| | |
| :--- | :--- |
| **Use Case Name** | 플레이어 이동 (Player Movement) |
| **Scenario** | 플레이어가 방향키를 조작하여 월드 내에서 캐릭터를 이동시킴 |
| **Triggering Event** | 플레이어가 키보드의 이동 키(방향키 등)를 입력함 |
| **Brief Description** | 플레이어가 이동 키를 입력하면 캐릭터가 즉각적으로 해당 방향으로 이동하며, 이 위치 정보가 서버를 통해 다른 클라이언트에 동기화됨 |
| **Actors** | 플레이어 |
| **Related Use Cases** | 없음 |
| **Stakeholders** | 플레이어, 게임 시스템 |
| **Preconditions** | • 게임이 정상적으로 실행 중이어야 함<br>• 플레이어 캐릭터가 씬(Scene)에 스폰되어 존재해야 함 |
| **Post Conditions** | • 플레이어 캐릭터의 월드 내 위치가 변경됨<br>• 변경된 위치 정보가 같은 방에 있는 모든 플레이어에게 동기화됨 |

---

# Use Case 1: 플레이어 이동 (2/2)

<div class="columns">
  <div class="actor-box">
    <h3>👤 Actor (플레이어)</h3>
    <p><b> 1. 이동 입력</b><br>
    가고자 하는 방향에 맞춰 키보드의 이동 키(WASD 등)를 입력함.</p>
  </div>

  <div class="system-box">
    <h3>🖥️ System (게임 시스템/서버)</h3>
    <p><b> 2. 로컬 처리 및 동기화 요청</b><br>
    클라이언트가 입력을 감지하고 즉시 입력된 방향으로 플레이어 캐릭터를 이동시키며, 서버에 입력/위치 정보를 전송함.</p>
    <p><b> 3. 화면 동기화</b><br>
    수신된 위치 정보를 바탕으로, 다른 플레이어들의 화면에도 해당 캐릭터의 이동 상태를 반영함.</p>
  </div>
</div>

<div class="exception-box">
  <h3>⚠️ Exception Conditions</h3>
  <ul>
    <li><b>1. 무입력 상태:</b> 방향키 입력이 없을 경우 캐릭터는 이동하지 않고 대기(Idle) 상태를 유지함.</li>
    <li><b>2. 충돌 제한:</b> 이동 경로 상에 이동 불가능한 영역(벽, 장애물 등)이 있을 경우, 물리 엔진에 의해 이동이 차단되고 해당 위치에 멈춤.</li>
  </ul>
</div>

---

# Use Case 2: 물리 오브젝트 잡기 및 던지기 (1/2)

| | |
| :--- | :--- |
| **Use Case Name** | 물리 오브젝트 잡기 및 던지기 |
| **Scenario** | 퍼즐 해결을 위해 월드의 물리 객체를 집어 들고 원하는 방향으로 던짐 |
| **Triggering Event** | 상호작용 유효 사거리 내에서 '잡기' 키 입력 실행 |
| **Brief Description** | 플레이어가 객체를 선택해 들고 이동하며, 입력에 따라 힘을 가해 던짐<br>상태 변화(잡기/해제/던지기)는 서버를 통해 동기화됨  |
| **Actors** | 플레이어 |
| **Related Use Cases** | **Include:** 플레이어 이동 |
| **Stakeholders** | 플레이어, 게임 시스템 |
| **Preconditions** | • 캐릭터가 조작 가능한 상태일 것<br>• 대상이 상호작용 가능한 물리 객체일 것<br>• 상호작용 사거리 이내이며 플레이어의 손이 비어있을 것 |
| **Post Conditions** | • 객체가 물리 법칙에 따라 이동하고 최종 위치가 동기화됨<br>• 플레이어 캐릭터는 기본(빈 손) 상태로 복귀함 |

---

# Use Case 2: 물리 오브젝트 잡기 및 던지기 (2/2)

<div class="columns">
  <div class="actor-box">
    <h3>👤 Actor (클라이언트)</h3>
    <p><b> 1. 잡기</b><br>
    객체가 플레이어의 손으로 이동 및 잡기 애니메이션 처리 후 서버에 상태 전송</p>
    <p><b> 3. 던지기</b><br>
    던지기 입력 시 로컬에서 물리적 힘을 우선 적용하여 즉각적인 시각적 피드백 제공 후 서버에 데이터 전송</p>
  </div>

  <div class="system-box">
    <h3>🖥️ System (서버)</h3>
    <p><b> 2. 주도권 부여</b><br>
    상태 검증 후 해당 클라이언트에 객체 제어 주도권 부여 및 전체 브로드캐스트</p>
    <p><b> 4. 물리 동기화</b><br>
    수신된 데이터로 궤적을 시뮬레이션하며, 전체 클라이언트 위치 지속 보정</p>
  </div>
</div>

<div class="exception-box">
  <h3>⚠️ Exception Conditions</h3>
  <ul>
    <b>1. 동시 잡기 충돌:</b> 서버 판단 하에 늦게 도달한 클라이언트의 행동을 강제 취소하고 롤백함.<br>
    <b>2. 지형 끼임:</b> 물리 연산 폭주 감지 시 객체 속도를 0으로 초기화하고 근처 안전 구역으로 강제 이동시킴.<br>
    <b>3. 잡기 중 상태 이상:</b> 객체를 들고 있는 도중 피격되거나 사망 시, 던지기를 취소하고 그 자리에 객체를 즉시 드롭함.<br>
  </ul>
</div>

---

# Use Case 3: 무거운 물리 객체 밀기 (1/2)

| | |
| :--- | :--- |
| **Use Case Name** | 무거운 물리 객체 밀기 |
| **Scenario** | 혼자서는 움직일 수 없는 무거운 기믹을 두 플레이어가 힘을 합쳐 목표 지점까지 이동시킴 |
| **Triggering Event** | 상호작용 유효 사거리 내에서 객체에 '밀기/당기기' 키 입력 실행 |
| **Brief Description** | 두 플레이어의 지속적인 힘 입력을 서버에서 합산하여 객체를 이동시키고, 클라이언트 캐릭터의 위치를 객체에 동기화 |
| **Actors** | 플레이어 |
| **Related Use Cases** | **Include:** 플레이어 이동 |
| **Stakeholders** | 플레이어, 게임 시스템 |
| **Preconditions** | • 캐릭터가 조작 가능한 상태일 것<br>• 대상이 혼자서는 밀 수 없는 무거운 물리 객체일 것<br>• 두 플레이어 모두 상호작용 사거리 이내에 있을 것 |
| **Post Conditions** | • 무거운 객체가 목표 지점으로 이동하고 최종 위치가 동기화됨<br>• 플레이어 캐릭터는 객체에서 분리되어 기본 상태로 복귀함 |

---

# Use Case 3: 무거운 물리 객체 밀기 (2/2)

<div class="columns">
  <div class="actor-box">
    <h3>👤 Actor (클라이언트)</h3>
    <p><b> 1. 밀기/당기기</b><br>
    객체에 달라붙어 상호작용 애니메이션 처리 후 지속적인 힘(입력 벡터)을 서버에 전송</p>
    <p><b> 3. 위치 동기화</b><br>
    서버로부터 객체의 이동 좌표를 받아 자신의 캐릭터 위치를 강제로 동기화하며 이동</p>
  </div>

  <div class="system-box">
    <h3>🖥️ System (서버)</h3>
    <p><b> 2. 힘 합산 및 승인</b><br>
    두 플레이어의 힘을 합산하여 객체의 저항을 넘어서면 객체의 이동을 승인 및 브로드캐스트</p>
    <p><b> 4. 물리 동기화</b><br>
    수신된 데이터로 객체의 이동 궤적을 시뮬레이션하며, 전체 클라이언트 위치 지속 보정</p>
  </div>
</div>

<div class="exception-box">
  <h3>⚠️ Exception Conditions</h3>
  <ul>
    <b>1. 단독 입력 충돌:</b> 한 명의 플레이어만 입력할 경우, 합산된 힘이 부족하여 객체의 이동이 즉시 정지됨.<br>
    <b>2. 지형 끼임:</b> 물리 연산 폭주 감지 시 객체 속도를 0으로 초기화하고 근처 안전 구역으로 강제 이동시킴.<br>
    <b>3. 조작 중 상태 이상:</b> 객체를 조작하는 도중 피격되거나 사망 시, 상호작용을 취소하고 힘 합산에서 즉시 제외함.<br>
  </ul>
</div>

---

# Use Case 4: 게임 상태 확인하기 (1/2)

| | |
| :--- | :--- |
| **Use Case Name** | 게임 상태 확인하기 (Check Game Status) |
| **Scenario** | 플레이어가 게임 플레이 중 자신의 캐릭터 상태와 게임 진행 상황을 확인함 |
| **Triggering Event** | 상태창 단축키(예: Tab, Esc 등)를 누르거나 화면 UI 상의 버튼을 클릭함 |
| **Brief Description** | 시스템은 플레이어의 호출 요청을 받아, 현재 캐릭터의 상태(체력, 소지품 등)와 스테이지 진행도(퍼즐 달성률, 남은 시간 등)를 화면에 UI 오버레이로 출력함 |
| **Actors** | 플레이어 |
| **Related Use Cases** | 없음 |
| **Stakeholders** | 플레이어, 게임 시스템 |
| **Preconditions** | • 게임이 실행 중이며 플레이어 화면이 렌더링되고 있어야 함<br>• 상태 정보를 표시할 UI 시스템이 초기화되어 있어야 함 |
| **Post Conditions** | • 플레이어 화면에 상태 정보창(UI)이 활성화(또는 비활성화)됨<br>• (멀티플레이 특성상) 상태창을 띄워도 게임은 뒤에서 실시간으로 계속 진행됨 |

---

# Use Case 4: 게임 상태 확인하기 (2/2)

<div class="columns">
  <div class="actor-box">
    <h3>👤 Actor (플레이어)</h3>
    <p><b> 1. 상태창 호출</b><br>
    게임 진행 상황을 파악하기 위해 상태창 단축키(Tab 등)를 입력함.</p>
    <p><b> 3. 상태창 닫기</b><br>
    확인이 끝나면 동일한 단축키를 다시 누르거나 닫기 버튼을 클릭함.</p>
  </div>

  <div class="system-box">
    <h3>🖥️ System (게임 시스템)</h3>
    <p><b> 2. 데이터 수집 및 UI 출력</b><br>
    입력을 감지하면 현재 클라이언트와 서버에서 동기화 중인 최신 플레이어 상태/진행도 데이터를 수집하여 UI 화면에 오버레이 형태로 띄움.</p>
    <p><b> 4. UI 숨김 처리</b><br>
    닫기 입력을 감지하면 활성화된 상태창 UI를 화면에서 제거함.</p>
  </div>
</div>

<div class="exception-box">
  <h3>⚠️ Exception Conditions</h3>
  <ul>
    <li><b>1. 특수 상황 제한:</b> 중요한 컷신(Cinematic)이 재생 중이거나, 로딩 화면, 또는 캐릭터가 기절/사망 상태일 때는 상태창 호출 단축키를 눌러도 무시됨.</li>
  </ul>
</div>

---

# Non-Functional Requirements

---

# Player
| | | | |
| :--- | :--- | :--- | :--- |
| Use Case Name| NFR 내역 (Non-Functional Requirements) | Quality | Quality Attributes |
| **플레이어 이동** | **클라이언트 응답성:** 이동 키 입력 시 클라이언트 화면에서 캐릭터가 지연(Lag) 없이 즉각적으로 반응하여 이동해야 함. | Performance Efficiency<br>(성능 효율성) | 입력 후 **50ms 이내** 캐릭터 이동 렌더링 완료 |
| **플레이어 이동** | **서버 동기화 성능:** 플레이어의 위치 정보가 서버를 거쳐 다른 클라이언트들에게 실시간에 가깝게 전송되고 화면에 부드럽게 동기화되어야 함. | Performance Efficiency<br>(성능 효율성) | 위치 동기화 지연 시간 **100ms 이하** 및 초당 **30 Tick** 유지 |
| **플레이어 이동** | **물리 충돌 처리 정확도:** 이동 경로 상의 벽, 장애물 등 이동 불가능 영역을 정확히 판정하여 캐릭터가 지형을 뚫고 지나가는 현상(Clipping)이 발생하지 않아야 함. | Functional Suitability<br>(기능 적합성) | 장애물 충돌 판정 실패 및 Clipping 발생률 **0%** |
| **오브젝트 잡기/던지기** | **물리 궤적 동기화 정확도:** 로컬에서 예측된 물리 궤적과 서버에서 시뮬레이션된 실제 궤적 간의 오차를 최소화하고, 차이가 발생할 경우 부드럽게 위치를 보정해야 함. | Functional Suitability<br>(기능 적합성) | 서버-로컬 궤적 오차 발생 시 **100ms 이내** 보정 프로세스 완료 |

---

| | | | |
| :--- | :--- | :--- | :--- |
| Use Case Name| NFR 내역 (Non-Functional Requirements) | Quality | Quality Attributes |
| **오브젝트 잡기/던지기,<br>무거운 물리 객체 밀기** | **물리 엔진 안정성 및 복구력:** 객체가 지형에 끼어 물리 연산이 폭주하는 것을 감지하고, 즉시 속도를 0으로 초기화 후 안전 구역으로 복구시켜 서버 크래시를 방지해야 함. | Reliability<br>(신뢰성) | 연산 무한 루프 감지 시 **0.1초 이내** 상태 초기화 및 복구 실행 |
| **오브젝트 잡기/던지기**,<br>**무거운 물리 객체 밀기** | **상태 일관성 유지:** 객체를 들고 있는 도중 피격, 사망, 또는 네트워크 단절이 발생할 경우, 들고 있던 객체가 즉시 드롭되도록 상태를 일관성 있게 동기화해야 함. | Functional Suitability<br>(기능 적합성) | 예외 상태 발생 후 **100ms 이내** 객체 드롭 상태 동기화 완료 |
| **게임 상태 확인** | **상황 인지성 및 가시성:** 상태창이 활성화된 동안에도 플레이어가 주변 위협을 인지할 수 있도록 UI의 반투명 처리나 적절한 배치가 보장되어야 함. | Usability<br>(사용성) | UI 활성화 시 배경 게임 화면의 투명도(Alpha) **50% 이상** 확보 |
| **게임 상태 확인** | **컨텍스트 기반 제어 정확성:** 컷신, 로딩, 캐릭터 사망 등 예외 상황에서 호출 단축키 입력 시, UI가 강제로 열리지 않도록 정확히 차단되어야 함. | Functional Suitability<br>(기능 적합성) | 정의된 예외 상황(컷신 등)에서의 UI 호출 차단 성공률 **100%** |

---

# Use Case: 서버
담당: 이규원
    
---

# Use Case 1: 방 생성하기 (1/2)

| | |
| :--- | :--- |
| **Use Case Name** | 방 생성하기 (Create Room) |
| **Scenario** | 사용자가 게임서버에 접속하여 새로운 방을 생성함 |
| **Triggering Event** | 사용자가 '방 생성하기' 버튼을 누름 |
| **Brief Description** | 시스템은 플레이어의 요청을 받아 새로운 Photon Room을 생성하며, 이때 방 이름, 최대 인원, 공개 여부 등의 속성을 설정함 |
| **Actors** | 플레이어<br>|
| **Related Use Cases** | **Include:** 방 참가하기<br>**Extend:** 게임 시작하기 |
| **Stakeholders** | 플레이어, 같은 방에 입장할 다른 플레이어들, 게임 시스템 |
| **Preconditions** | • 플레이어가 서버에 연결되어 있어야 함<br>• 방 생성 화면 또는 로비 상태에 있어야 함<br>• 방 생성에 필요한 정보(방 이름, 최대 인원 등)가 입력되어야 함 |
| **Post Conditions** | • 새로운 방이 생성되고, 방을 생성한 플레이어가 즉시 입장됨<br>• 방 정보(속성)가 설정됨<br>• 공개 방이면 타인의 검색/입장이 가능하며, 비공개 방이면 이름을 아는 사용자만 입장 가능함 |

---

# Use Case 1: 방 생성하기 (2/2)

<div class="columns">
  <div class="actor-box">
    <h3>👤 Actor (플레이어)</h3>
    <p><b> 1. 메뉴 선택 및 정보 입력</b><br>
    방 생성 메뉴를 선택한 뒤, 방 이름, 최대 인원 등의 속성 정보를 입력함.</p>
    <p><b> 2. 생성 요청</b><br>
    생성 버튼을 눌러 서버에 방 생성 요청을 전송함.</p>
  </div>

  <div class="system-box">
    <h3>🖥️ System (Photon 서버)</h3>
    <p><b> 3. 유효성 검사 및 방 생성</b><br>
    입력값의 유효성을 확인한 후, 새로운 방을 생성하고 방 정보를 서버에 저장함.</p>
    <p><b> 4. 플레이어 입장 및 UI 갱신</b><br>
    방 생성이 완료되면 플레이어를 해당 방에 입장시키고 대기실 화면을 표시함.</p>
  </div>
</div>

<div class="exception-box">
  <h3>⚠️ Exception Conditions</h3>
  <ul>
    <li><b>1. 연결 오류:</b> 서버 연결이 끊어진 상태일 경우 방 생성 실패 처리 및 알림.</li>
    <li><b>2. 입력값 오류:</b> 중복되거나 유효하지 않은 방 이름, 또는 입력값이 누락된 경우 생성 거부.</li>
    <li><b>3. 서버 요청 실패:</b> 네트워크 문제 등으로 Photon 서버 생성 요청에 실패한 경우 에러 메시지 출력.</li>
    <li><b>4. 잘못된 속성:</b> 허용되지 않은 방 속성(예: 비정상적인 최대 인원 수) 설정 시 생성 제한.</li>
  </ul>
</div>

---

# Use Case 2: 방 참가하기 (1/2)

| | |
| :--- | :--- |
| **Use Case Name** | 방 참가하기 (Join Room) |
| **Scenario** | 플레이어가 기존에 생성된 방에 입장하기 위해 참가 요청을 보냄 |
| **Triggering Event** | 플레이어가 방 목록에서 특정 방을 선택하거나, 방 이름을 입력하여 참가를 요청함 |
| **Brief Description** | 시스템은 플레이어의 방 참가 요청을 받아 해당 방의 존재 여부, 입장 가능 여부, 최대 인원 등을 기준으로 참가를 처리함 |
| **Actors** |  플레이어 |
| **Related Use Cases** | **Include:** 서버 접속하기, 방 검색하기<br>**Extend:** 방 생성하기 |
| **Stakeholders** | 플레이어, 같은 방의 기존 플레이어들, 게임 시스템 |
| **Preconditions** | • 플레이어가 Photon 서버에 연결되어 있어야 함<br>• 참가하려는 방이 존재하거나 조건에 맞는 방이 있어야 함<br>• 방이 닫혀 있지 않고 최대 인원에 도달하지 않아야 함 |
| **Post Conditions** | • 플레이어가 해당 방에 입장하며 방 인원 수가 갱신됨<br>• 같은 방의 기존 플레이어들에게 새 참가자의 입장이 동기화됨 |

---

# Use Case 2: 방 참가하기 (2/2)

<div class="columns">
  <div class="actor-box">
    <h3>👤 Actor (플레이어)</h3>
    <p><b> 1. 방 탐색 및 선택</b><br>
    방 목록을 확인하고 참가할 방을 선택하거나 방 이름을 직접 입력함.</p>
    <p><b> 2. 참가 요청</b><br>
    참가 버튼을 눌러 서버에 해당 방으로의 입장을 요청함.</p>
  </div>

  <div class="system-box">
    <h3>🖥️ System (Photon 서버)</h3>
    <p><b> 3. 방 상태 검증</b><br>
    요청받은 방의 존재 여부, 현재 인원 수 및 입장 가능 상태(비밀번호 등)를 확인함.</p>
    <p><b> 4. 입장 처리 및 갱신</b><br>
    검증 통과 시 플레이어를 방에 입장시키고, 참가자 목록을 갱신하여 대기실 화면을 표시함.</p>
  </div>
</div>

<div class="exception-box">
  <h3>⚠️ Exception Conditions</h3>
  <ul>
    <li><b>1. 방 존재 안 함:</b> 방이 이미 사라졌거나 존재하지 않는 경우 참가 실패 처리 및 알림.</li>
    <li><b>2. 인원 초과:</b> 방이 이미 최대 인원에 도달하여 가득 찬 경우 입장 거부 및 에러 메시지 출력.</li>
    <li><b>3. 중복 입장:</b> 동일한 UserID로 같은 방에 이미 입장해 있는 상태에서 중복 참가를 요청할 경우 처리 거부.</li>
  </ul>
</div>

---

# Use Case 3: 채팅하기 (1/2)

| | |
| :--- | :--- |
| **Use Case Name** | 채팅하기 (Chatting) |
| **Scenario** | 같은 게임 방에 입장한 플레이어들이 대기실 또는 플레이 중 텍스트 메시지를 주고받음 |
| **Triggering Event** | 플레이어가 채팅창에 메시지를 입력하고 전송(Enter) 키를 누름 |
| **Brief Description** | 시스템은 플레이어가 입력한 메시지를 RPC(Remote Procedure Call)를 통해 같은 방의 다른 플레이어들에게 전달하고 UI에 표시함 |
| **Actors** | 플레이어|
| **Related Use Cases** | **Include:** 서버 접속하기, 방 생성하기, 방 참가하기 |
| **Stakeholders** | 플레이어, 같은 방의 다른 플레이어, 게임 시스템 |
| **Preconditions** | • 플레이어가 서버에 연결되어 있고, 같은 방에 입장한 상태여야 함<br>• 채팅 UI가 활성화되어 있어야 함<br>• 채팅 처리 오브젝트에 네트워크 컴포넌트(PhotonView)가 연결되어 있어야 함 |
| **Post Conditions** | • 전송된 메시지가 방 안의 모든 플레이어에게 전달됨<br>• 발신자 정보와 함께 각 플레이어의 채팅창 화면에 메시지가 표시됨 |

---

# Use Case 3: 채팅하기 (2/2)

<div class="columns">
  <div class="actor-box">
    <h3>👤 Actor (플레이어)</h3>
    <p><b> 1. 메시지 작성</b><br>
    채팅 입력창을 활성화하고 전송할 텍스트 메시지를 작성함.</p>
    <p><b> 2. 전송 요청</b><br>
    Enter 키 또는 전송 버튼을 눌러 메시지 발송을 서버에 요청함.</p>
  </div>

  <div class="system-box">
    <h3>🖥️ System (Photon 서버)</h3>
    <p><b> 3. 유효성 검증 및 브로드캐스트</b><br>
    입력된 메시지가 비어 있는지 확인한 후, RPC를 통해 방 안의 다른 플레이어들에게 메시지를 전파함.</p>
    <p><b> 4. UI 갱신</b><br>
    각 클라이언트의 채팅 UI에 발신자 정보와 함께 수신된 메시지를 출력함.</p>
  </div>
</div>

<div class="exception-box">
  <h3>⚠️ Exception Conditions</h3>
  <ul>
    <li><b>1. 연결 오류:</b> 서버 연결이 끊어진 경우 메시지 전송이 실패하며 에러 처리됨.</li>
    <li><b>2. 방 미입장 상태:</b> 로비 등 방에 입장하지 않은 상태에서는 채팅 기능을 사용할 수 없음.</li>
    <li><b>3. 빈 메시지 전송:</b> 입력된 텍스트가 비어 있거나 공백만 있는 경우, 시스템이 전송 요청을 무시함.</li>
  </ul>
</div>

---

# Use Case 4: 스테이지 시작하기 (1/2)

| | |
| :--- | :--- |
| **Use Case Name** | 스테이지 시작하기 (Start Stage) |
| **Scenario** | 같은 방에 있는 플레이어들의 준비 상태가 충족되면 시스템이 게임 시작 여부를 방 전체에 반영함 |
| **Triggering Event** | 게임 시작 조건이 충족되거나, 플레이어(방장)가 시작 요청을 보냄 |
| **Brief Description** | 시스템은 게임 시작 여부, 시작 시간 등의 정보를 룸 속성(Room Properties)으로 갱신하고, 같은 방의 모든 플레이어에게 이를 동기화함 |
| **Actors** | 플레이어|
| **Related Use Cases** | **Include:** 방 참가하기, Ready 하기, 방 인원 Ready 체크 |
| **Stakeholders** | 플레이어, 같은 방의 다른 플레이어, 게임 시스템 |
| **Preconditions** | • 플레이어들이 같은 Photon Room에 입장해 있어야 함<br>• 모든 플레이어의 게임 시작 조건(Ready 등)이 충족되어야 함<br>• 시작 상태를 저장할 룸 속성(HashTable 형태)이 정의되어 있어야 함 |
| **Post Conditions** | • 게임 시작 여부가 룸 전체 상태 속성에 갱신/반영됨<br>• 필요 시 시작 시간 및 라운드 정보가 함께 저장됨<br>• 같은 방의 모든 플레이어 화면이 동일한 인게임 상태로 전환됨 |

---

# Use Case 4: 스테이지 시작하기 (2/2)

<div class="columns">
  <div class="actor-box">
    <h3>👤 Actor (플레이어)</h3>
    <p><b> 1. 대기 및 준비</b><br>
    같은 방에 입장하여 게임 시작을 위한 준비(Ready) 상태로 대기함.</p>
    <p><b> 2. 시작 요청</b><br>
    조건이 충족된 상태에서 플레이어(방장)가 게임 시작 버튼을 누름.</p>
  </div>

  <div class="system-box">
    <h3>🖥️ System (Photon 서버)</h3>
    <p><b> 3. 시작 조건 검증</b><br>
    시작 요청을 수신한 뒤, 현재 방 인원과 모든 플레이어의 준비 상태를 확인함.</p>
    <p><b> 4. 룸 속성 갱신 및 전환</b><br>
    시작이 가능하다고 판단되면 룸 속성(시작 시간, 상태)을 갱신하고 모든 플레이어의 화면을 게임 진행 상태로 일제히 전환함.</p>
  </div>
</div>

<div class="exception-box">
  <h3>⚠️ Exception Conditions</h3>
  <ul>
    <li><b>1. 인원 부족:</b> 일부 플레이어가 아직 방에 입장하지 않았거나 최소 시작 인원에 미달한 경우.</li>
    <li><b>2. 준비 미완료:</b> 방 안의 모든 플레이어가 'Ready' 상태가 아닌 경우 시작 요청 거절.</li>
    <li><b>3. 속성 갱신 실패:</b> 네트워크 문제 등으로 Photon 룸 속성(HashTable) 갱신에 실패한 경우 진행 중단.</li>
  </ul>
</div>

---

# Non-Functional Requirements

---
# 비기능적 요구사항

| | | | |
| :--- | :--- | :--- | :--- |
| Use Case Name | NFR 내역 (Non-Functional Requirements) | Quality | Quality Attributes |
| **방 생성하기** | **방 생성 응답성:** 사용자가 방 생성 요청을 보냈을 때, 시스템은 지연 없이 방 생성 결과를 반환하고 대기실 화면으로 전환해야 함. | Performance Efficiency<br>(성능 효율성) | Time Behavior(시간 반응성) 평균 2초 이내에 방 생성 결과가 화면에 반영되고 대기실로 전환되어야 함. |
| **방 참가하기** | **입장 판정 정확성:** 존재하지 않는 방, 최대 인원을 초과한 방, 닫힌 방에 대해서는 참가 요청이 정확하게 거부되어야 함. | Functional Suitability<br>(기능 적합성) | Fault Tolerance(결함 수용성) 동시 요청 상황에서도 최대 인원을 초과한 입장을 허용하지 않아야 함. |
| **채팅하기** | **메시지 전달 응답성:** 플레이어가 전송한 채팅 메시지는 같은 방의 다른 플레이어들에게 짧은 시간 안에 표시되어야 함. | Performance Efficiency<br>(성능 효율성) | Confidentiality(기밀성) 같은 방 외부의 사용자에게 채팅 메시지가 전달되거나 노출되지 않아야 함. |
| **스테이지 시작하기** | **시작 조건 판정 정확성:** 모든 플레이어가 Ready 상태일 때만 게임 시작이 허용되어야 하며, 조건이 충족되지 않으면 시작 요청이 거부되어야 함. | Functional Suitability<br>(기능 적합성) | Interoperability(상호운용성) 모든 클라이언트가 동일한 시작 상태와 시작 시점을 공유해야 함.|

---

# Use Case: 오브젝트, 맵
담당: 양준영
    
---

# Use Case 1: 스테이지 입장 - 맵 생성 (1/2)

| | |
| :--- | :--- |
| **Use Case Name** | 스테이지 입장 - 맵 생성 (Load Stage Map) |
| **Scenario** | 플레이어가 스테이지를 선택하면 맵이 생성되고 아바타가 배치됨 |
| **Triggering Event** | 플레이어가 스테이지를 선택하여 입장을 시도함 |
| **Brief Description** | 게임 시스템은 플레이어가 선택한 스테이지를 확인하여 해당하는 맵을 로딩하고, 완료 시 플레이어의 아바타를 스폰 위치에 생성함 |
| **Actors** | 플레이어|
| **Related Use Cases** | **Include:** 서버 - 게임 시작하기 |
| **Stakeholders** | 플레이어, 게임 시스템 |
| **Preconditions** | • 게임이 실행 중이어야 함<br>• 플레이어들이 게임에 접속해 시작할 준비를 마친 상태여야 함 |
| **Post Conditions** | • 스테이지의 맵이 로드됨<br>• 게임에 접속한 플레이어들의 아바타가 맵의 지정된 위치에 놓임 |

---
# Use Case 1: 스테이지 입장 - 맵 생성 (2/2)

<div class="columns">
  <div class="actor-box">
    <h3>👤 Actor (플레이어)</h3>
    <p><b> 1. 스테이지 선택</b><br>
    스테이지 목록이 나열된 화면에서 플레이할 스테이지를 찾아 선택함.</p>
  </div>

  <div class="system-box">
    <h3>🖥️ System (게임 시스템)</h3>
    <p><b> 2. 씬 이동 및 맵 로딩</b><br>
    맵을 로딩할 씬으로 이동한 후, 선택된 스테이지를 확인하여 해당하는 맵 데이터를 매니저를 통해 로딩함.</p>
    <p><b> 3. 아바타 스폰 및 준비 완료</b><br>
    로딩이 완료되면 플레이어의 아바타를 생성해 맵의 스폰 포인트에 위치시키고, 게임을 시작할 수 있도록 처리함.</p>
  </div>
</div>

<div class="exception-box">
  <h3>⚠️ Exception Conditions</h3>
  <ul>
    <li><b>1. 연결 끊김:</b> 로딩이 진행되는 과정에서 플레이어와의 네트워크 연결이 끊긴다면, 알림 팝업을 띄우고 즉시 로비로 이동 처리함.</li>
  </ul>
</div>

---

# Use Case 2: 기믹 수행 - 레버 작동 (1/2)

| | |
| :--- | :--- |
| **Use Case Name** | 기믹 수행 - 레버 작동 (Operate Lever Gimmick) |
| **Scenario** | 플레이어가 레버 오브젝트와 상호작용해 숨겨진 구조물을 활성화함 |
| **Triggering Event** | 플레이어가 레버 오브젝트와 상호작용을 시도함 |
| **Brief Description** | 플레이어가 레버를 작동시키면, 아바타 조작만으로는 오를 수 없는 절벽에 비계 형태의 구조물이 나타나 이동 가능한 길을 만들어 줌 |
| **Actors** | 플레이어|
| **Related Use Cases** | 없음 |
| **Stakeholders** | 플레이어, 게임 시스템 |
| **Preconditions** | • 게임이 실행 중이며 플레이어가 아바타를 조작할 수 있는 상태여야 함<br>• 오를 수 없는 절벽 지형과 상호작용 가능한 레버가 존재해야 함 |
| **Post Conditions** | • 레버가 작동 후 상호작용 불가(비활성화) 상태로 전환됨<br>• 절벽을 오를 수 있는 비계 형태의 구조물이 맵에 생성/등장함 |

---

# Use Case 2: 기믹 수행 - 레버 작동 (2/2)

<div class="columns">
  <div class="actor-box">
    <h3>👤 Actor (플레이어)</h3>
    <p><b> 1. 상호작용 시도</b><br>
    절벽 근처에 위치한 레버 오브젝트에 다가가 상호작용 키를 입력함.</p>
  </div>

  <div class="system-box">
    <h3>🖥️ System (게임 시스템)</h3>
    <p><b> 2. 상태 비활성화</b><br>
    레버와의 상호작용 입력을 감지하면 즉시 해당 레버를 상호작용 불가 상태로 변경함.</p>
    <p><b> 3. 구조물 활성화</b><br>
    절벽 속이나 땅 아래에 숨어 있던 비계 형태의 지형 구조물을 맵 상에 등장시킴.</p>
  </div>
</div>

<div class="exception-box">
  <h3>⚠️ Exception Conditions</h3>
  <ul>
    <li><b>1. 중복 작동 시도:</b> 이미 작동이 완료되어 비활성화된 레버에 다시 상호작용을 시도할 경우, 시스템은 이를 무시하고 아무런 이벤트도 발생시키지 않음.</li>
  </ul>
</div>

---

# Use Case 3: 기믹 수행 - 버튼 작동 (1/2)

| | |
| :--- | :--- |
| **Use Case Name** | 기믹 수행 - 버튼 작동 (Operate Button Gimmick) |
| **Scenario** | 플레이어가 버튼 오브젝트와 상호작용해서 교차 활성화되는 다리 구조물을 작동시킴 |
| **Triggering Event** | 플레이어가 버튼 오브젝트와 상호작용을 시도함 |
| **Brief Description** | 플레이어가 버튼을 작동시키면 다리를 구성하는 구조물들이 세트(A/B)별로 교차 활성화 및 비활성화되며 이동 가능한 경로를 변경함 |
| **Actors** | 플레이어|
| **Related Use Cases** | 없음 |
| **Stakeholders** | 플레이어, 게임 시스템 |
| **Preconditions** | • 게임이 실행 중이며 플레이어가 아바타를 조작할 수 있는 상태여야 함<br>• 건너가야 하는 지형과 교차 다리 구조물, 상호작용 가능한 버튼이 존재해야 함 |
| **Post Conditions** | • 다리를 이루는 구조물 세트(A, B)의 활성화 상태가 반전됨<br>• 플레이어의 맵 이동 경로 및 퍼즐 해결 조건이 갱신됨 |

---

# Use Case 3: 기믹 수행 - 버튼 작동 (2/2)

<div class="columns">
  <div class="actor-box">
    <h3>👤 Actor (플레이어)</h3>
    <p><b> 1. 최초 상호작용 시도</b><br>
    버튼 오브젝트 근처로 다가가 상호작용 키를 입력함.</p>
    <p><b> 2. 추가 상호작용 시도</b><br>
    필요에 따라 이동 경로를 바꾸기 위해 버튼과 다시 상호작용함.</p>
  </div>

  <div class="system-box">
    <h3>🖥️ System (게임 시스템)</h3>
    <p><b> 3. 1차 상태 전환</b><br>
    상호작용 감지 시 다리를 형성하는 구조물 A 세트를 활성화하고, 기존에 켜져 있던 B 세트를 비활성화함.</p>
    <p><b> 4. 2차 토글 전환</b><br>
    재상호작용 감지 시 현재 켜진 A 세트를 끄고, 꺼져 있던 B 세트를 다시 켜는 상태 반전(Toggle) 처리를 수행함.</p>
  </div>
</div>

<div class="exception-box">
  <h3>⚠️ Exception Conditions</h3>
  <ul>
    <li><b>1. 사거리 초과:</b> 버튼과의 거리가 먼 상태(상호작용 유효 범위 밖)에서 상호작용을 시도하면 시스템이 이를 무시하고 작동하지 않음.</li>
  </ul>
</div>

---

# Use Case 4: 최종 기믹 해결 및 스테이지 클리어 (1/2)

| | |
| :--- | :--- |
| **Use Case Name** | 최종 기믹 해결 및 스테이지 클리어 (Clear Final Gimmick & Stage) |
| **Scenario** | 스테이지의 최종 기믹을 수행하고 스테이지 클리어 상태로 진입함 |
| **Triggering Event** | 플레이어들이 스테이지에 설정된 최종 클리어 조건을 달성함 |
| **Brief Description** | 두 명의 플레이어가 각각 두 개의 발판을 밟아 출구를 개방하고, 두 플레이어 모두 해당 출구로 진입하여 스테이지를 클리어함 |
| **Actors** | Primary: 플레이어 A, 플레이어 B|
| **Related Use Cases** | **Include:** 플레이어 - 스테이지 이동 및 클리어하기 |
| **Stakeholders** | 플레이어, 게임 시스템 |
| **Preconditions** | • 게임이 실행 중이며 아바타를 조작할 수 있는 상태여야 함<br>• 클리어 조건에 해당하는 두 개의 발판과 닫혀있는 출구가 존재해야 함 |
| **Post Conditions** | • 시스템이 스테이지가 클리어된 것을 확인하고 클리어 관련 절차에 돌입함 |

---

# Use Case 4: 최종 기믹 해결 및 스테이지 클리어 (2/2)

<div class="columns">
  <div class="actor-box">
    <h3>👤 Actor (플레이어 A, B)</h3>
    <p><b> 1. 발판 활성화 (기믹 수행)</b><br>
    두 명의 플레이어가 각각 맵에 배치된 두 개의 발판 오브젝트 위에 올라섬.</p>
    <p><b> 3. 출구 진입</b><br>
    기믹이 풀려 출구 오브젝트가 열리면, 두 플레이어 모두 해당 출구 영역으로 진입함.</p>
  </div>

  <div class="system-box">
    <h3>🖥️ System (게임 시스템)</h3>
    <p><b> 2. 기믹 판정 및 출구 개방</b><br>
    두 개의 발판이 모두 밟혀 활성화된 것을 감지하면, 닫혀있던 출구 오브젝트를 활성화하여 문을 엶.</p>
    <p><b> 4. 클리어 판정 및 절차 실행</b><br>
    두 플레이어의 출구 진입이 모두 감지되면 스테이지 클리어 조건을 충족시키고 클리어 절차를 실행함.</p>
  </div>
</div>

<div class="exception-box">
  <h3>⚠️ Exception Conditions</h3>
  <ul>
    <li><b>1. 연결 끊김:</b> 스테이지 클리어 관련 절차를 실행하기 이전에 네트워크 연결이 끊긴다면, 실행 절차를 즉시 중단하고 재시도 팝업을 띄우거나 메인 메뉴로 강제 이동시킴.</li>
  </ul>
</div>

---

# Non-Functional Requirements

---
# 비기능적 요구사항

| | | | |
| :--- | :--- | :--- | :--- |
| Use Case Name | NFR 내역 (Non-Functional Requirements) | Quality | Quality Attributes |
| **스테이지 입장 - 맵 생성** | **스테이지 생성 응답성:** 플레이어가 스테이지를 선택하고 게임을 시작하려 할 때 맵 로딩이 신속히 진행되어야 함. | Performance Efficiency<br>(성능 효율성) | Time Behavior: 지정된 시간(약 3초) 안에 스테이지가 로딩되도록 함. |
| **기믹 수행 - 레버 작동** | **상호작용 정확성:** 레버 상호작용 시 지정된 오브젝트들이 정확히 작동하도록 해야 함. | Functional Suitability<br>(기능 적합성) | Functional Correctness: 레버와 지정된 오브젝트를 정확히 연결해 함수가 올바르게 작동하도록 구현. |
| **기믹 수행 - 버튼 작동** | **기믹 학습성:** 복잡한 기믹이라도 사용자가 빠른 시간 내에 이해할 수 있게 설계해야 함. | Interaction Capability<br>(상호작용 능력) | Learnability: 한 화면 안에서 기믹의 실행이 파악되는 등 기믹을 직관적으로 이해 가능하게 설계. |
| **스테이지 최종 기믹 해결<br>및 스테이지 클리어** | **클리어 판정 무결성:** 최종 기믹을 정상적으로 수행했을 시에만 스테이지 클리어 판정으로 인식해야 함. | Reliability<br>(안정성) | Faultlessness: 지정된 기믹만 클리어 판정으로 연결, 이외 모든 기믹에 대해 클리어 판정으로 연결되지 않도록 전수조사 |
