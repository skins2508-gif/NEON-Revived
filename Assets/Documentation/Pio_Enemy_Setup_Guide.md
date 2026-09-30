# Pio 적 등장 및 애니메이션 설정 문서

## 1. 현재 구현 목표

Player (3)가 지정된 Trigger를 통과하면 Pio가 등장한다.

등장한 Pio는 Player (3)와 일정한 거리를 유지하면서 계속 화면 안에 남아 있어야 한다.- Player (3)가 역중력을 사용해 회전하거나 위아래로 이동하더라도 Pio는 플레이어의 위치만 따라가고 회전이나 반전은 따라가지 않는다.

Pio가 등장한 뒤에는 다음 순서로 애니메이션을 재생한다.

1. Pio1 위치 애니메이션 재생
2. Pio1 애니메이션 반복
3. 지정된 반복 횟수가 끝나면 pio shoot 애니메이션 재생

현재 구현 범위는 총을 쏘는 모션까지다. 실제 총알 오브젝트 생성, 총알 이동, 충돌 및 피해 처리는 아직 포함하지 않는다.

---

## 2. 관련 오브젝트 구조

현재 Pio는 Player (3)의 자식이 아니다.

```text
Player (3)

Pio
├─ Firepoint
└─ Visuil
```

Pio를 Player (3)의 자식으로 넣지 않는 이유는 역중력 때문이다. Pio가 플레이어의 자식이면 플레이어의 위치뿐만 아니라 회전과 Scale 반전까지 상속한다. 그러면 역중력 상태에서 Pio가 뒤집히거나 반대 방향으로 움직일 수 있다.

따라서 Pio는 Scene의 독립 오브젝트로 유지하고 Position Constraint를 이용해 Player (3)의 위치만 따라가도록 설정한다.

### Pio

Pio는 적 전체의 기준점이다.

Pio에 연결된 주요 컴포넌트:

- Transform
- Animator
- Position Constraint

Pio의 Transform Position을 애니메이션으로 직접 움직이면 안 된다. Pio의 위치는 Position Constraint가 관리한다.

### Visuil

Visuil은 실제 적 그림과 움직임을 담당하는 자식 오브젝트다.

Visuil에 연결된 주요 컴포넌트:

- Transform
- Sprite Renderer

Idle 위치 움직임은 Pio가 아니라 Visuil의 Local Position에 기록한다. Shoot 애니메이션의 이미지 변경도 Visuil의 Sprite Renderer에 기록한다.

### Firepoint

Firepoint는 나중에 실제 총알을 생성할 총구 위치다.

현재는 실제 총알 생성 기능이 없으므로 총알을 발사하지 않는다. 추후 총알 기능을 만들 때 Firepoint의 월드 위치에서 총알을 생성하면 된다.

---

## 3. 등장 Trigger 설정

등장 Trigger 오브젝트 이름:

```text
EnemySpawnTrigger
```

필요한 컴포넌트:

- Box Collider 2D
- Enemy Spawn Trigger 스크립트

Box Collider 2D 설정:

```text
Is Trigger: 체크
```

Enemy Spawn Trigger 설정:

```text
Enemy: Pio
Target Player: Player (3)
Action: Show
Hide Enemy On Start: 체크
Trigger Only Once: 체크
```

### 동작 과정

1. 게임이 시작되면 EnemySpawnTrigger가 Pio를 비활성화한다.
2. Player (3)가 Trigger의 Box Collider 2D에 진입한다.
3. EnemySpawnTrigger가 Pio를 활성화한다.
4. Pio의 Animator가 기본 상태인 Pio1을 처음부터 재생한다.
5. Trigger Only Once가 활성화되어 있으므로 같은 Trigger가 다시 실행되지 않는다.

Target Player에 Player (3)가 직접 연결되어 있으므로 다른 Player 태그 오브젝트가 Trigger에 닿아도 Pio가 등장하지 않는다.

### Trigger 위치

현재 등장 Trigger의 위치는 의도적으로 정한 위치이므로 변경하지 않는다.

---

## 4. EnemySpawnTrigger의 Show/Hide 기능

EnemySpawnTrigger에는 다음 두 Action이 있다.

```text
Show: Enemy를 활성화
Hide: Enemy를 비활성화
```

현재 Scene에서는 Show 기능을 사용하는 등장 Trigger만 설정되어 있다.

Hide Trigger 오브젝트 배치는 현재 작업 범위에서 제외했다. 따라서 Pio는 등장한 뒤 별도의 Hide Trigger에 닿지 않는 한 계속 활성화 상태를 유지한다.

나중에 Hide Trigger가 필요하면 등장 Trigger를 복제한 뒤 다음처럼 설정한다.

```text
Action: Hide
Enemy: Pio
Target Player: Player (3)
Hide Enemy On Start: 해제
Trigger Only Once: 체크
```

역중력이나 점프로 Trigger를 피하지 못하도록 Collider를 통로 높이 전체에 배치하는 것이 좋다.

---

## 5. Position Constraint 설정

Pio가 Player (3)를 따라다니도록 Pio에 Position Constraint가 연결되어 있다.

현재 설정:

```text
Is Active: 체크
Weight: 1
Source: Player (3)
Source Weight: 1

Freeze Position Axes
X: 체크
Y: 체크
Z: 해제

Position Offset
X: 22
Y: -0.4
Z: 0
```

### Offset 의미

```text
X 22
```

Pio가 Player (3)보다 오른쪽으로 22유닛 떨어진 위치를 유지한다.

```text
Y -0.4
```

Pio가 Player (3)보다 아래쪽으로 0.4유닛 떨어진 위치를 유지한다.

Unity 직렬화 값에는 Y가 `-0.40000153`처럼 표시될 수 있다. 이는 부동소수점 표현 차이이며 실제 설정값 `-0.4`와 같은 것으로 취급한다.

### 카메라를 계속 따라오는 이유

카메라가 Player (3)의 이동을 따라간다. Pio도 Position Constraint로 Player (3)의 X와 Y 위치를 따라가므로 플레이어와 일정한 간격을 유지하면서 카메라 화면 안에 계속 남는다.

Pio의 X를 월드 좌표에 고정하면 카메라가 오른쪽으로 이동할 때 Pio가 화면 밖으로 사라진다. 따라서 현재 설정에서는 X와 Y를 모두 추적해야 한다.

### 역중력에서 정상 동작하는 이유

Position Constraint는 현재 X와 Y 위치만 복사한다. Pio는 Player (3)의 자식이 아니므로 다음 항목을 상속하지 않는다.

- Player (3)의 Z 회전
- Player (3)의 Scale 반전
- Sprite Renderer의 Flip
- 역중력 방향 자체

따라서 Player (3)가 역중력으로 뒤집혀도 Pio는 자신의 방향을 유지하면서 위치만 따라간다.

### 주의 사항

- Pio를 Player (3)의 자식으로 다시 넣지 않는다.
- Position Constraint의 Source를 다른 플레이어로 바꾸지 않는다.
- X 축 체크를 해제하면 Pio가 카메라 이동을 따라가지 못하고 화면 밖으로 사라질 수 있다.
- Position Offset은 현재 `X 22`, `Y -0.4`를 기준으로 한다.
- Zero 버튼을 누르면 Offset이 0으로 초기화되어 Pio가 Player (3)과 겹칠 수 있다.

---

## 6. Pio1 애니메이션

애니메이션 파일:

```text
Assets/Pio1.anim
```

Pio1은 등장 후 반복되는 위치 애니메이션이다.

현재 중요한 설정:

```text
Loop Time: 체크
```

Pio1 애니메이션은 부모 Pio의 Position을 움직이지 않는다. 다음 속성만 사용한다.

```text
Visuil : Position
Visuil : Sprite Renderer.Sprite
```

Visuil의 Local Position을 움직이기 때문에 Pio의 Position Constraint에는 영향을 주지 않는다.

### 삭제하면 안 되는 항목

```text
Visuil : Position
```

이 항목이 반복 위치 움직임을 담당한다. 이 항목을 삭제하면 Idle 위치 애니메이션이 사라진다.

### 존재하면 안 되는 항목

```text
Pio : Position
```

부모 Pio의 Position 키가 생기면 Animator가 Position Constraint의 결과를 덮어쓸 수 있다. 애니메이션 녹화 중 Pio Transform을 변경하지 않도록 주의한다.

---

## 7. pio shoot 애니메이션

애니메이션 파일:

```text
Assets/pio shoot.anim
```

pio shoot은 총을 쏘는 자세로 Sprite 이미지를 변경하는 애니메이션이다.

현재 중요한 설정:

```text
Loop Time: 해제
Position Curves: 없음
```

현재 남아 있는 주요 속성:

```text
Visuil : Sprite Renderer.Sprite
```

부모 Pio 또는 Visuil의 위치를 바꾸지 않으므로 Shoot 모션이 재생되어도 Position Constraint가 유지된다.

### 주의 사항

- `Pio : Position`을 추가하지 않는다.
- Shoot 이미지 변경은 반드시 Visuil의 Sprite Renderer.Sprite에 기록한다.
- 새로운 Sprite Renderer를 추가할 필요가 없다.
- 기존 Visuil의 Sprite Renderer 하나를 Idle과 Shoot에서 함께 사용한다.
- Loop Time은 해제 상태를 유지한다.

---

## 8. Animator Controller 설정

Animator Controller 파일:

```text
Assets/Pio.controller
```

Pio 오브젝트의 Animator Controller에 연결되어 있다.

현재 상태 구조:

```text
Entry
  ↓
Pio1
  ↓
pio shoot
```

Pio1이 기본 상태다. Pio가 Trigger에 의해 활성화되면 Pio1이 먼저 재생된다.

Pio1에서 pio shoot으로 연결된 Transition 설정:

```text
Has Exit Time: 활성화
Exit Time: 2
Transition Duration: 0
별도 Condition: 없음
```

Exit Time이 2이므로 Pio1 애니메이션을 두 번 재생한 다음 pio shoot 상태로 이동한다.

pio shoot에는 다음 상태로 이동하는 Transition이 없다. Shoot 애니메이션이 끝나면 마지막 발사 자세를 유지한다.

발사 후 다시 Idle로 돌아가게 만들거나 일정 시간마다 반복 발사하는 기능은 현재 구현 범위에 포함하지 않는다.

---

## 9. 전체 실행 흐름

```text
게임 시작
  ↓
EnemySpawnTrigger가 Pio 비활성화
  ↓
Player (3)가 등장 Trigger 진입
  ↓
Pio 활성화
  ↓
Position Constraint로 Player (3) 위치 추적
  ├─ X Offset: 22
  └─ Y Offset: -0.4
  ↓
Pio1 애니메이션 2회 재생
  ↓
pio shoot 애니메이션 재생
  ↓
마지막 발사 자세 유지
```

Player (3)가 역중력을 사용해도 Pio는 회전하거나 뒤집히지 않고 X/Y 위치만 계속 따라간다.

---

## 10. 완료된 항목

- Player (3) 전용 등장 Trigger 연결
- 게임 시작 시 Pio 숨김
- Trigger 통과 시 Pio 등장
- Pio와 Player (3)의 X 간격 유지
- Pio와 Player (3)의 Y 간격 유지
- 카메라 이동 중 화면 안에 Pio 유지
- 역중력 회전 및 반전 미상속
- Pio1 위치 애니메이션 Loop
- Pio1에서 Shoot으로 자동 전환
- Shoot 애니메이션에서 부모 위치 키 제거
- Position Constraint 직렬화 오류 해결
- EnemySpawnTrigger에 Show/Hide 선택 기능 추가

---

## 11. 현재 제외된 기능

다음 기능은 아직 구현하지 않았다.

- 실제 총알 Prefab 생성
- Firepoint에서 총알 Instantiate 또는 Object Pooling
- 총알 이동
- 플레이어 조준
- 유도 총알
- 총알 충돌 및 피해 처리
- 화면 밖 총알 제거
- Shoot Animation Event
- 반복 발사 타이머
- Shoot 후 Pio1으로 복귀
- Scene에 Hide Trigger 배치

---

## 12. 문제 발생 시 확인 순서

### Pio가 처음부터 보이는 경우

EnemySpawnTrigger에서 다음 설정을 확인한다.

```text
Action: Show
Hide Enemy On Start: 체크
Enemy: Pio
```

### Trigger를 밟아도 Pio가 나타나지 않는 경우

다음을 확인한다.

- Trigger의 Box Collider 2D에서 Is Trigger가 체크되어 있는지
- Target Player가 Player (3)인지
- Player (3)에 Collider 2D와 Rigidbody 2D가 있는지
- Enemy에 Pio가 연결되어 있는지
- Console에 컴파일 오류가 없는지

### Pio가 카메라 밖으로 사라지는 경우

Position Constraint를 확인한다.

```text
Source: Player (3)
X: 체크
Y: 체크
Weight: 1
Is Active: 체크
```

### 역중력에서 Pio가 뒤집히는 경우

- Pio가 Player (3)의 자식인지 확인한다.
- 자식이라면 Scene 루트로 이동한다.
- Position Constraint로만 위치를 추적해야 한다.

### Shoot 때 Pio 위치가 갑자기 바뀌는 경우

Animation 창에서 pio shoot을 선택하고 다음 항목을 제거한다.

```text
Pio : Position
```

다음 항목은 유지한다.

```text
Visuil : Sprite Renderer.Sprite
```

### Position Constraint Type mismatch 오류가 나타나는 경우

정상 Position Constraint는 Unity가 Inspector의 Add Component를 통해 생성한 컴포넌트여야 한다.

현재 씬에는 Unity 6이 정상 직렬화한 Position Constraint가 저장되어 있다. 과거 잘못된 클래스 ID `181`에서 발생했던 AudioFilter Type mismatch 문제는 수정되었다.

---

## 13. 최종 기준값 요약

```text
등장 감지 대상: Player (3)
등장 대상: Pio
Trigger Action: Show
시작 시 숨김: 활성화
한 번만 실행: 활성화

Position Constraint Source: Player (3)
Position Constraint Weight: 1
X 추적: 활성화
Y 추적: 활성화
Z 추적: 비활성화
X Offset: 22
Y Offset: -0.4

기본 Animator State: Pio1
Pio1 Loop: 활성화
Pio1 반복 후 Shoot 전환: 2회
Shoot Loop: 비활성화
Shoot 부모 Position Curve: 없음
```
