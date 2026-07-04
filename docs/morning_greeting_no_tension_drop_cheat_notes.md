# Morning greeting no-tension-drop cheat notes

작성일: 2026-07-04

## 결론

아침 인사(`code == mo`)에서 실패 선택지를 골라도 텐션이 떨어지지 않게 하는 가장 안전한 방법은, 선택지 분기의 결과 점수(score)를 그 이벤트의 최고 점수(max_score)로 바꾸는 것이다. 텐션 값을 직접 찾거나 고정하지 않아도 된다. 게임은 아침 인사 선택지의 결과 점수를 보고 후속 판정과 텐션 변화를 처리하므로, 실패 분기의 score만 성공 분기와 같은 값으로 올리면 된다.

기존 패처의 "영업 선택지 항상 퍼펙트" 구현이 이미 이 방식을 사용한다. 치트 프로그램은 이 로직을 그대로 재사용하거나, 아래 7개 아침 인사 대상만 별도 적용하면 된다.

## 근거 자료

- 최신 상세 데이터: `work/communication_choices/communication_events_detailed.csv`
- 패치 대상 목록: `work/communication_choices/always_perfect_patch_targets.csv`
- 배포용 JSONL: `ImasKoreanPatcher/Assets/communication_always_perfect.jsonl`
- CSV -> JSONL 변환: `ImasKoreanPatcher/scripts/Build-Release.ps1`
- 실제 점수 패치 구현: `ImasKoreanPatcher/CommunicationPerfectPatcher.cs`

`always_perfect_patch_targets.csv`는 `max_score > 0` 이고 `score < max_score`인 행만 모은 목록이다. 여기에서 `code == mo`로 필터링하면 아침 인사 실패 선택지 패치 대상은 총 7개다. `in_current_guide_scope` 값은 모두 `False`지만, 아침 인사 치트에서는 이 필드로 제외하면 안 된다.

## 패치 원리

SCB 안의 `CMD` 섹션에서 각 선택지 커맨드의 score 필드를 바꾼다.

- `cmd_offset`은 SCB 파일 전체 기준이 아니라 `CMD` 섹션 시작 기준 오프셋이다.
- `choice` 타입 score 필드 위치는 `command_start + 0x08`이다.
- `touch` 타입 score 필드 위치는 기존 패처 기준 `command_start + 0x10`이지만, 현재 아침 인사 no-drop 대상 7개는 모두 `choice`다.
- score/max_score는 32비트 빅엔디언 정수다.
- 현재값이 CSV의 `score`와 같으면 `max_score`로 쓴다.
- 현재값이 이미 `max_score`면 이미 패치된 것으로 보고 건너뛴다.
- 현재값이 둘 다 아니면 다른 빌드거나 오프셋이 어긋난 것이므로 패치를 중단하고 mismatch로 보고한다.

산식:

```text
command_start    = scb_cmd_section_offset + cmd_offset
score_offset_scb = command_start + 0x08
write_value      = max_score as big-endian u32
```

SCB에서 `CMD` 섹션을 찾는 방법은 기존 패처와 동일하다. SCB 섹션 테이블은 `0x70`에서 시작하고, 16바이트짜리 행 7개를 검사해 라벨이 `CMD`인 행의 size/offset을 읽으면 된다.

## 대상 7개

아래 `bna_score_offset`은 `work/translation2/patch/translation2_bna_expand_reaudit_xiso_root`의 현재 BNA 파일에서 검증한 파일 내부 오프셋이다. 재빌드된 BNA에서는 엔트리 배치가 달라질 수 있으므로, 치트 프로그램은 가능하면 `bna`/`entry`/`cmd_offset`을 기준으로 다시 계산해야 한다.

| event_id | option | option_text | bna | entry | cmd_offset | SCB score offset | current | write |
| --- | ---: | --- | --- | --- | ---: | ---: | ---: | --- |
| `ami_mo:choice:06` | 2 | `必要ないだろ？` | `root/script/ami/ami_mo.bna` | `root/script/ami/ami_mo.scb` | `0x1dcc` | `0x1eb4` | `00 00 00 01` | `00 00 00 03` |
| `ami_mo:choice:06` | 3 | `ない` | `root/script/ami/ami_mo.bna` | `root/script/ami/ami_mo.scb` | `0x1de0` | `0x1ec8` | `00 00 00 00` | `00 00 00 03` |
| `mik_mo:choice:04` | 2 | `条件反射だよ` | `root/script/mik/mik_mo.bna` | `root/script/mik/mik_mo.scb` | `0x0bdc` | `0x0cc4` | `00 00 00 01` | `00 00 00 04` |
| `mik_mo:choice:04` | 3 | `わからないよ` | `root/script/mik/mik_mo.bna` | `root/script/mik/mik_mo.scb` | `0x0bf0` | `0x0cd8` | `00 00 00 00` | `00 00 00 04` |
| `mik_mo:choice:05` | 2 | `あまり / 好きじゃない` | `root/script/mik/mik_mo.bna` | `root/script/mik/mik_mo.scb` | `0x12a4` | `0x138c` | `00 00 00 01` | `00 00 00 04` |
| `mik_mo:choice:06` | 2 | `新鮮だ……` | `root/script/mik/mik_mo.bna` | `root/script/mik/mik_mo.scb` | `0x1678` | `0x1760` | `00 00 00 01` | `00 00 00 04` |
| `mik_mo:choice:06` | 3 | `思い切ったな` | `root/script/mik/mik_mo.bna` | `root/script/mik/mik_mo.scb` | `0x168c` | `0x1774` | `00 00 00 00` | `00 00 00 04` |

검증된 현재 BNA 내부 score 오프셋:

| event_id | option | bna_score_offset |
| --- | ---: | ---: |
| `ami_mo:choice:06` | 2 | `0x35b4` |
| `ami_mo:choice:06` | 3 | `0x35c8` |
| `mik_mo:choice:04` | 2 | `0x2b44` |
| `mik_mo:choice:04` | 3 | `0x2b58` |
| `mik_mo:choice:05` | 2 | `0x320c` |
| `mik_mo:choice:06` | 2 | `0x35e0` |
| `mik_mo:choice:06` | 3 | `0x35f4` |

## 구현 경로

추천 구현은 두 가지다.

1. 기존 전체 퍼펙트 자산 재사용

   `ImasKoreanPatcher/Assets/communication_always_perfect.jsonl`을 읽고 `event_type == choice`, `script_key`가 `ami_ami_mo` 또는 `mik_mik_mo`인 행만 적용한다. 이 파일에는 이미 `bna`, `entry`, `cmd_offset`, `score`, `max_score`가 들어 있다.

2. CSV에서 아침 인사만 직접 필터링

   `work/communication_choices/always_perfect_patch_targets.csv`에서 `code == mo`인 행만 읽는다. `source_file`을 다시 열어 BNA/SCB 위치를 복원하거나, `idol_code`와 `script_name`으로 아래 경로를 만들면 된다.

   ```text
   bna   = root/script/{idol_code}/{script_name}.bna
   entry = root/script/{idol_code}/{script_name}.scb
   ```

## 주의점

- 이 방식은 "잘못 고른 선택지의 점수"를 최고점으로 바꾸는 방식이다. 선택 직후의 대사 분기는 여전히 원래 실패 대사일 수 있지만, 결과 점수가 최고점이므로 후속 텐션 하락을 막는 목적에는 맞다.
- 런타임 메모리 치트로 만들 경우, 위의 BNA/SCB 오프셋은 프로세스 메모리 주소가 아니다. 파일 패치용 상대 오프셋이므로, 메모리에서는 로드된 SCB 블록을 찾아 같은 score 필드를 써야 한다.
- 다른 버전 ISO나 재빌드 BNA에서는 `bna_score_offset`이 달라질 수 있다. `cmd_offset`과 SCB `CMD` 섹션 기준 산식이 더 안정적이다.
- 아침 인사 전체 choice 행은 각 아이돌별로 존재하지만, 현재 데이터 기준 실패 점수가 실제로 존재하는 no-drop 대상은 아미/마미 2개, 미키 5개뿐이다. 다른 아이돌의 `mo` choice 행은 `max_score == 0`이거나 이미 최고점이라 이 치트 대상이 아니다.

## 검증 절차

1. 패치 전 7개 위치의 32비트 빅엔디언 값이 `current`와 같은지 확인한다.
2. 각 위치에 `write` 값을 기록한다.
3. 다시 읽어서 모두 `max_score`가 되었는지 확인한다.
4. 게임에서 해당 아침 인사 선택지를 일부러 틀린다.
5. 실패 대사가 나오더라도 결과적으로 텐션이 내려가지 않는지 확인한다.

만약 5번에서 여전히 텐션이 내려가면, 해당 이벤트에는 score 외에 별도 텐션 감소 커맨드가 있을 가능성이 있다. 그 경우에는 같은 `target_label`로 도달하는 후속 커맨드 중 tension 관련 명령을 추가 분석해야 한다. 다만 현재 기존 패처의 전체 퍼펙트 구조와 점수 데이터상으로는 score를 max_score로 올리는 방법이 1차 구현안이다.
