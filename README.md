# JH Remote Support v0.10 · 직원 공통 자동 업데이트

2026-11-07 · **최초 연결 설정이 필요한 시험 적용본**

직원별 GitHub·Render 서버·이름·주소·사용량은 그대로 두고, 정현쌤이 발행한 서명된 업데이트를 공통으로 받습니다. **기능을 추가한 상태이며 실제 GitHub/Render 설정·발행·Windows 실기기 적용까지 완료한 것은 아닙니다.**

먼저 **먼저읽어주세요.html**을 엽니다. 첫 설치는 한 번 필요합니다. v0.9 이하에는 업데이트 기능이 없으므로 처음부터 자동으로 바뀌지 않습니다.

## 이후 업데이트 흐름

1. 정현쌤이 새 소스와 새 버전을 원본 GitHub main에 반영합니다.
2. 발행 작업이 Windows EXE를 빌드·검사하고 관리자 비밀키로 서명한 업데이트를 GitHub Releases에 올립니다.
3. 직원 GitHub가 약 30분 간격으로 새 발행본을 확인합니다. 서명·해시·크기·버전·허용 경로를 검증한 서버 파일만 반영합니다.
4. 대기/지원 방이 있으면 서버 재배포를 미룹니다. 없으면 직원 Render의 배포 훅을 호출하고, 실제 서버의 버전과 다운로드 EXE 해시를 확인합니다.
5. 상담원 프로그램은 시작할 때와 대기 중 약 30분 간격으로 새 버전을 확인합니다. 코드 발급 대기/지원 중에는 적용하지 않습니다. 지원 중이 아니면 파일을 검증하고 재시작합니다.
6. 학생은 직원 홈페이지에서 새로 다운로드하면 최신 학생용 파일을 받습니다. 기존 학생 EXE를 자동으로 바꾸거나 학생 PC에 상주 설치하지 않습니다.

GitHub 예약 작업은 지연될 수 있어 즉시 반영/정확한 30분을 보장하지 않습니다. 공개 저장소에 60일 동안 활동이 없으면 예약 작업이 비활성화될 수 있습니다. Actions 화면에서 상태를 확인하고 필요하면 다시 활성화해야 합니다. 정상 배포를 확인한 뒤에는 변경 없는 주기 검사로 Render 서버를 깨우지 않습니다.

## 정현쌤 최초 1회

별도로 전달한 **정현쌤전용-배포키-공유금지.zip**에서 `JH_UPDATE_SIGNING_KEY_B64.txt`를 엽니다. 내용을 전부 복사해 원본 GitHub 저장소의 Settings → Secrets and variables → Actions → New repository secret에 넣습니다.

- Name: `JH_UPDATE_SIGNING_KEY_B64`
- Secret: 파일 내용 전체

**이 키 파일은 GitHub의 파일 업로드 화면에 올리지 않습니다. 직원에게 보내지 않습니다.** 공개 배포 ZIP에는 비밀키가 없고 검증용 공개키만 있습니다. 이 키를 잃으면 기존 직원들에게 신뢰되는 새 업데이트를 발행할 수 없으므로 개인적으로 보관하세요.

그다음 이번 공개 ZIP의 소스를 원본 저장소 최상위에 반영합니다. 특히 `.github`, `scripts`, `updates`, `client`, `relay`가 필요합니다. 원본 저장소는 `plannerkxxg-sudo/jh-remote-support`, 브랜치는 main으로 설정돼 있습니다.

Actions에서 **Publish verified shared update**를 실행하고 성공 여부와 Releases의 다음 3개 파일을 확인합니다.

- JHRemoteSupport.exe
- relay-update.zip
- update-manifest.json

발행본이 이미 있으면 같은 버전을 덮어쓰지 않습니다. 다음 업데이트는 client 어셈블리 버전과 updates/release-version.txt를 함께 높여 발행합니다. 발행 파이프라인은 새 버전 파일 반영 또는 수동 실행으로 시작합니다. 단순히 파일 하나를 수정하는 것만으로 매번 새 정식 업데이트를 발행하지 않습니다.

## 직원 최초 1회

상세 순서는 **AUTO-UPDATE-MANUAL.md**를 보세요.

1. 이번 v0.10의 relay를 본인 GitHub에 반영하고 기존 Render에 수동으로 한 번 배포합니다. /api/status에서 version 0.10, deployedVersion 0.10.0.0, downloadReady true를 확인합니다.
2. 본인 GitHub에 `.github/workflows/sync-updates.yml`, `scripts/release_common.py`, `scripts/sync_release.py`, `scripts/deploy_release.py`, `updates/publisher-public.pem`을 추가합니다.
3. GitHub Actions 쓰기 권한을 허용하고, Actions 변수 `SUPPORT_SITE_URL`에 본인 홈페이지 주소를 저장합니다.
4. 본인 Render의 Deploy Hook을 GitHub Actions Secret `RENDER_DEPLOY_HOOK_URL`로 저장합니다.
5. **직원 Render의 Auto-Deploy를 Off로 바꿉니다.** 이후 검증·대기 여부 확인을 마친 업데이트 작업이 배포 훅을 호출합니다. 이 설정을 끄지 않으면 Git 반영만으로 먼저 재배포돼 대기 확인을 건너뛸 수 있습니다.
6. Actions의 **Receive shared remote support updates**를 활성화하고 한 번 실행합니다.
7. 상담원 PC의 기존 EXE를 종료하고 새 client/JHRemoteSupport.exe와 옆의 .config로 교체해 실행합니다. 같은 Windows 사용자라면 기존 이름·주소·소개를 읽습니다.

직원별 `.env`와 Render 환경변수, PC의 `%LOCALAPPDATA%/JHRemoteSupport/settings.json`은 동기화 대상이 아닙니다. 직원은 client 소스를 GitHub에 올리지 않아도 됩니다. 프로그램·서버 자체 설정은 직원별로 유지합니다.

## 변경과 검증

- Windows 7 SP1 호환 통신, 숫자 6자리 코드, 학생 승인, 즉시 종료, 안내 제목 중복 생략을 유지했습니다.
- RSA 3072 / SHA-256으로 서명된 업데이트만 받아들이며, 공개키는 상담원 EXE에 고정했습니다. 파일 해시만 확인하는 방식이 아닙니다.
- 서버 ZIP은 허용된 런타임 파일만 교체합니다. 경로 이탈·심볼릭 링크·추가 파일·이전 버전 재적용·같은 버전 내용 변경을 거부합니다.
- 상담원 EXE를 검증한 뒤 별도 도우미가 기존 프로세스 종료를 기다리고 교체합니다. 이전 파일은 JHRemoteSupport.previous.exe로 남깁니다. 교체/재시작 실패 시 기존 파일을 복구하거나 유지하고 같은 실패 버전의 반복 적용을 제한합니다.
- 자동 업데이트용 서명은 Windows 공인 코드서명 인증서와 다릅니다. Chrome/SmartScreen 경고가 없어지는 것을 보장하지 않습니다.

C# 5/AnyCPU 관리 코드 EXE를 Mono 6.8에서 교차 빌드했습니다. Windows에서 다시 빌드하는 Build.cmd와 GitHub Windows 발행 작업도 포함했습니다. 사용 PC에 Mono 설치를 요구하지 않습니다.

검증: Python 자동 검사 **66개 통과**(서버/웹·실제 C# 통신·서명 검증/설정 유지/변조 차단/백업/배포 흐름). JavaScript UI·구문 검사, C# 컴파일·설정/TLS/파일명 검사도 통과했습니다. 실제 Windows GUI·실행 중 EXE 교체·GitHub Actions·PowerShell 발행·Render 재배포는 아직 실행하지 않았습니다. 처음에는 직원 한 명과 확인한 뒤 확대하세요.

지원 환경은 Windows 7 SP1 / 8.1 / 10 / 11과 .NET Framework 4.8 이상입니다. Windows 7은 4.8을 사용하며 4.8.1은 설치할 수 없습니다. 날짜·업데이트·루트 인증서가 정상이어야 합니다. XP/Vista/Windows 8.0·모바일은 대상이 아닙니다. CLR 4가 아예 없으면 자체 안내 실행 전 Windows 설치 안내가 나올 수 있습니다.

기업망의 강제 인증 프록시/PAC는 별도 확인이 필요합니다. 서버 재배포 전 방 개수를 확인하지만, 확인 직후 새 연결이 생기는 경합까지 차단하는 서버 점검 모드는 이번에 포함하지 않았습니다. 파일 전송·무인 접속·UAC 보안 화면·다중 모니터 선택·종단간 암호화는 기존과 같이 미지원입니다.

자세한 검증 범위와 실패 시 복구는 BUILD-INFO.md와 AUTO-UPDATE-MANUAL.md를 참고하세요. 이전 UPDATE 문서와 v0.7 PDF는 변경 기록이며, 자동 업데이트 설정은 이번 안내를 우선합니다.
