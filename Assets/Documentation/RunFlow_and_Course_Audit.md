# 실행 및 코스 점검 (2026-09-22)

## 화면 흐름

- `Assets/Scenes/MainMenu.unity`를 열어 Play: 사이버펑크 메인화면 → START RUN → 기존 SampleScene.
- Build Settings 첫 씬은 MainMenu, 두 번째 씬은 SampleScene으로 등록했다.
- SampleScene을 직접 열어 Play해도 MainMenu가 먼저 열린다. START RUN을 눌러야 플레이어 이동과 BGM이 시작된다. 결과 화면의 RESTART는 바로 새 게임을 시작한다.
- 비활성 상태로 저장된 원본 `Player`를 실행 시 활성화하고 Player (1)~(4) 테스트 복사본을 끈다. 카메라도 원본을 따라간다. 편집 모드의 활성 상태는 변경하지 않는다.
- `Assets/Resources/RunGameSettings.asset`이 원본 Player 이름과 전체 BGM을 지정한다.
- Finish Tilemap의 실제 Collision/Trigger Enter 및 Stay에서 종료한다. 중복 접촉은 기록을 중복 저장하지 않는다.
- 종료 시 플레이어 물리/입력, 음악, 진행 중 워프를 중단한다. RESTART / RANKING / MAIN MENU를 사용할 수 있다.
- 재시작은 씬을 다시 불러와 플레이어, 적, 파괴된 타일, 음악, 롱노트 상태를 초기화한다.
- 랭킹은 이 기기의 PlayerPrefs에 최대 10개 저장한다. 롱노트 조기 해제 횟수가 적은 순, 동률이면 완주 시간이 짧은 순이다. 서버/온라인 순위가 아니다.
- 메인 배경은 `NeonMenuCity`가 실제 Grid 및 4개 Tilemap(원경, 건물, 네온, 고가도로)으로 생성한다. 배경과 UI는 Play 시 생성되므로 편집 모드의 MainMenu는 비어 있다. 배치·팔레트는 해당 스크립트에서 조절한다.
- UI는 마우스, W/S 또는 위/아래 방향키로 선택하고 Enter(숫자패드 Enter 포함)로 실행한다. 마지막/첫 버튼 사이를 순환하며, 선택된 버튼은 분홍 테두리와 `> <`로 표시한다. 랭킹·게임 종료 화면에서도 동일하게 조작한다. 플레이 중 J: 중력 반전, K: 통과/롱노트 유지.

## 속도·거리·음악

비활성화된 원본 `Player`를 기준으로 점검했다. 최초 점검에서 나온 50.2초/part4 비교는 테스트용 Player (4)의 마지막 구간 계산이며 전체 코스의 완주 시간이 아니다.

| 항목 | 확인값 |
| --- | --- |
| 기준 플레이어 | Player/Player (AutoRunner), 월드 위치 약 (-0.45818, 0.10000) |
| 기본 가로 속도 | 20 유닛/초 |
| 수직 이동 속도 | 20 유닛/초 (코드 기본값 15보다 씬 Inspector 값 20이 우선) |
| 일부 포털 가속 | 10유닛 동안 40 유닛/초; 일반 포털 15 설정은 기본 속도보다 낮아 실제 20 적용 |
| Finish 타일 셀 | (994, -117, 0), 원점/스케일 1인 Grid |
| 전체 BGM | `Seum Dero - Revived (Ft. Michael Barbera).mp3`, 약 251.520초 |
| 저장된 씬의 테스트용 BGM | part4 약 69.744초 → 실행 시 전체 BGM으로 연결 |
| 다른 파일 | part2 약 206.448초, part3 약 70.008초 |

아래는 포털을 따라 각 구간에서 전진하는 X 거리만 합친 기준값이다. 트리거/플레이어 콜라이더의 접촉 경계가 아니라 타일 셀 및 포털 Transform 위치를 이용한다.

| 구간 | 시작 X → 종료 X | 기준 거리 |
| --- | --- | --- |
| Player → Teleport1 | -0.45818 → 942 | 942.45818 |
| Teleport2Exit → Teleport3 | -12.22 → 1061 | 1073.22 |
| Teleport4Exit → TeleportZone | -11.74 → 765.299 | 777.039 |
| 워프 출구 → TeleportZone2 | -11.451 → 523.549 | 535.000 |
| 두 번째 워프 출구 → Finish | -10.451 → 994 | 1004.451 |
| 합계 | | 약 4332.17유닛 |

20유닛/초 등속 환산은 약 **216.61초**로 전체 음악 251.52초보다 약 **34.91초** 짧다. 두 큰 워프의 설정상 지연(각 0.3 + 1.5 + 0.8 + 0.2 = 2.8초)을 더한 단순 기준은 약 222.21초다.

그러나 롱 타일의 세로 이동·되돌아가는 연결 경로, 작은 포털의 좌표 점프·가속, 장애물 대기, 입력 타이밍이 실제 시간에 영향을 준다. 위 값은 전체 코스를 실제 플레이하여 얻은 완주 시간이 아니며, BGM에 정확히 맞는다고 확정할 수 없다. 코스 길이와 속도는 강제로 조정하지 않았다. MP3 프레임 합산은 인코더 패딩 때문에 실제 재생 길이와 소폭 다를 수 있다.

실제 완주 시 결과 화면의 `BGM`과 `FINISH GAP`으로 확인할 수 있다. GAP은 완주 시간 − 음악 길이이며 음수는 음악보다 먼저 종료했다는 뜻이다. Console에도 `[Course]`, `[Course complete]`로 출력한다.

추가로 BOTTOM에 X=-2390388의 멀리 떨어진 타일이 존재한다. 전체 Tilemap bounds를 코스 길이로 사용하면 잘못된 값이 나오므로 이번 계산은 플레이어와 Finish 위치를 기준으로 했다. 기존 타일 배치는 수정하지 않았다.

## 뒤집힘·표면 정렬 수정

기존 코드는 다음 물리 프레임에 BOTTOM을 감지하면 수직 속도를 즉시 0으로 만들었다. 속도 20, fixedDeltaTime 0.02라면 최대 약 0.4유닛의 이동량을 남긴 채 멈출 수 있었다. 또한 BOTTOM만 검사해 TOP/통과 타일과 처리 방식이 달랐다.

- 바닥/천장 표면까지 남은 거리만큼 수직 이동량을 제한한다. 접촉 여유는 Physics2D contact offset/설정된 surfaceOffset을 따른다.
- 이동 방향을 마주 보는 표면만 처리하고 옆 벽·트리거·IgnoreCollision 중인 타일을 제외한다.
- 반전 시 콜라이더 중심을 유지해 오프셋이 있는 콜라이더도 회전 때문에 튀지 않게 한다.
- K 통과 위치는 플레이어 원점 + 고정 두께 대신 실제 타일 반대쪽 경계와 콜라이더 반크기로 계산한다. Grid/Tilemap 스케일과 연속 타일 두께도 반영한다.

## 검증

별도 복제 프로젝트 `Temp/RunFlowValidation`에서 원본 에디터 씬과 실제 랭킹 저장소에 영향을 주지 않고 검증한다. C# 컴파일, 메인화면/랭킹/게임 전환, 실제 Finish 접촉, 기록 중복 방지, 재시작, BGM 정지·재생, 바닥/천장 정렬 및 오프셋 회전을 확인한다. 로그와 결과는 `Temp/runflow-unity.log`, `Temp/RunFlowValidation/verification-result.txt`에 저장된다.

최종 결과: C# 컴파일 오류 0개, Unity 실행 검증 18개 통과. 기존 `AttackTriggerFollow`의 deprecated API 경고 2개는 남아 있다. 원본 Player 자동 활성화와 전체 BGM 251.520초 연결도 실행 중 확인했다. 테스트는 Finish 근처로 플레이어를 옮겨 실제 충돌을 유발하며, 전체 코스 완주 측정은 아니다. 결과 화면 미리보기의 0.08초는 이 충돌 테스트 기록이다.

보존된 결과: `Artifacts/RunFlow/verification.txt`, `Artifacts/RunFlow/main-menu.png`, `Artifacts/RunFlow/finish-screen.png`. 재실행용 테스트는 `Tests/RunFlow/RunFlowVerification.cs`와 README를 참고한다. Unity 배치 모드는 오디오 재생이 비활성화되어, 최종 검증은 음소거된 숨김 에디터에서 진행했다. 복제 에디터의 Unity SearchDatabase 인덱싱 예외는 게임 코드와 별개이며 검증 결과에 구분했다.

메뉴 우선 진입 수정: SampleScene에서 바로 Play하는 경우에도 게임 오브젝트를 중지하고 MainMenu를 먼저 로드한다. START RUN / RESTART를 통한 진입만 게임을 시작한다. 메뉴 우선 진입 및 화면 전환 회귀 검사 6개 통과 (Artifacts/RunFlow/menu-first-verification.txt).
