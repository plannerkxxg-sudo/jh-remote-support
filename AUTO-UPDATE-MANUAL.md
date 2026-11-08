# 자동 업데이트 · 처음 한 번 설정하기

이 설정이 끝난 다음부터 공통 업데이트를 받습니다. 현재 계정에 자동 설정된 상태는 아닙니다.

## 준비물 구분

| 파일 | 누가 받나요? | 용도 |
| --- | --- | --- |
| JHRemoteSupport-v0.10.zip | 정현쌤·아진쌤·민주쌤 | 공통 소스·실행파일·설정 안내 |
| 정현쌤전용-배포키-공유금지.zip | 정현쌤만 | 업데이트 발행 권한을 증명하는 비밀키 |

두 ZIP을 함께 직원에게 전달하지 마세요. 비밀키는 원본 GitHub의 Secret 설정에만 넣습니다.

## 1. 정현쌤이 발행 준비하기

1. 정현쌤전용 ZIP을 별도 폴더에 풉니다.
2. JH_UPDATE_SIGNING_KEY_B64.txt를 메모장으로 엽니다.
3. Ctrl+A → Ctrl+C로 내용을 전부 복사합니다.
4. 원본 GitHub 저장소 https://github.com/plannerkxxg-sudo/jh-remote-support 를 엽니다.
5. Settings → Secrets and variables → Actions → Secrets → New repository secret을 누릅니다.
6. Name에 `JH_UPDATE_SIGNING_KEY_B64`를 입력합니다. Secret에 방금 복사한 내용을 붙여넣고 저장합니다. 파일 업로드 기능은 사용하지 않습니다.
7. 공개 ZIP JHRemoteSupport-v0.10.zip의 압축을 모두 풉니다.
8. GitHub 저장소 최상위의 Add file → Upload files에서 공개 ZIP의 내용을 반영합니다. ZIP 자체를 올리는 것이 아니라 압축 안의 client, relay, scripts, updates, .github와 안내 파일을 같은 경로로 올립니다. 비밀키 ZIP/폴더는 포함하지 않습니다.
9. main에 반영합니다. 보호된 브랜치는 조직 정책에 맞는 PR/승인 절차가 필요할 수 있습니다.
10. Actions → Publish verified shared update에서 실행 결과를 봅니다. 최초 반영으로 실행되지 않았다면 Run workflow로 실행합니다.
11. 성공하면 Releases에서 v0.10.0.0과 EXE·relay-update.zip·update-manifest.json 3개를 확인합니다. 이때부터 직원이 받을 발행본이 생깁니다.

이번 공개 패키지의 사전 빌드 EXE는 시험용이며, GitHub 발행 작업은 Windows의 Microsoft 컴파일러로 다시 빌드합니다. 두 빌드의 해시는 다를 수 있지만 신뢰하는 공개키와 프로그램 버전은 같습니다. 가능하면 직원에게 첫 EXE도 발행 작업이 만든 JHRemoteSupport.exe를 전달하세요.

## 2. 직원이 자기 서버를 v0.10으로 한 번 올리기

예: 아진쌤은 본인 GitHub와 본인 Render에서 진행합니다. 민주쌤도 각자 진행합니다.

1. 기존 GitHub 소스와 상담원 EXE를 보관합니다. 진행 중인 지원은 종료합니다.
2. 공개 ZIP을 모두 풉니다.
3. 본인 GitHub 최상위에서 이번 relay 폴더를 같은 위치에 업로드해 교체합니다. GitHub에 relay/relay 폴더가 생기지 않도록 합니다.
4. 본인 Render의 기존 서비스를 열고 최신 커밋을 수동 배포합니다. 새 서버를 만들거나 Docker/Python 유형을 바꾸지 않습니다.
5. 본인 홈페이지 주소 뒤에 /api/status를 붙여 접속합니다.
6. version은 0.10, deployedVersion은 0.10.0.0, downloadReady는 true가 나와야 합니다. busySessions는 대기/지원 중인 방 개수입니다.
7. 이름은 기존 Render의 SUPPORT_NAME/SUPPORT_DESCRIPTION을 유지합니다.

예전 서버는 busySessions/deployedVersion 정보를 제공하지 않으므로 최초 v0.10 적용은 한 번 수동으로 해야 합니다.

## 3. 직원 GitHub에 자동 수신 파일 넣기

본인 GitHub 최상위에 아래 파일을 이번 패키지와 같은 경로로 넣습니다.

- .github/workflows/sync-updates.yml
- scripts/release_common.py
- scripts/sync_release.py
- scripts/deploy_release.py
- updates/publisher-public.pem

client 소스 폴더는 직원 GitHub에 없어도 됩니다. 공개키는 직원에게 공유해도 되며, 비밀키와 다릅니다.

Actions 사용이 꺼져 있다면 활성화합니다. Fork에서 받은 작업은 활성화 동의가 필요할 수 있습니다.
Settings → Actions → General → Workflow permissions에서 Read and write permissions를 허용합니다.
보호 브랜치나 조직 정책이 봇의 변경 반영을 막는 경우에는 저장소 관리자에게 쓰기 허용 범위를 확인합니다.

## 4. 직원 홈페이지 주소를 변수에 넣기

1. 본인 GitHub의 Settings → Secrets and variables → Actions를 엽니다.
2. Variables 탭 → New repository variable을 누릅니다.
3. Name: `SUPPORT_SITE_URL`
4. Value: 본인 홈페이지 기본 주소를 입력합니다. 예: https://실제직원주소.onrender.com
5. 주소 뒤의 ?name=... 같은 개인화 문구는 넣지 않습니다. 실제 배포된 본인 주소를 사용합니다.

## 5. Render 배포 연결하기

1. 본인 Render 서비스의 Settings에서 Deploy Hook URL을 찾습니다.
2. 이 URL 전체를 복사합니다. 배포 권한이 있는 비밀 주소이므로 학생이나 공개 게시글에 보내지 않습니다.
3. 본인 GitHub의 Settings → Secrets and variables → Actions → Secrets → New repository secret을 누릅니다.
4. Name: `RENDER_DEPLOY_HOOK_URL`
5. Secret: 복사한 Deploy Hook URL 전체를 붙여넣고 저장합니다.
6. 직원 Render 서비스의 Auto-Deploy를 **Off**로 바꿉니다. 이후 자동 수신 작업이 검증과 대기 확인을 끝낸 다음 배포 훅으로 재배포합니다.

정현쌤의 원본 서버와 직원 서버 설정을 혼동하지 마세요. 위 주소·배포 훅은 반드시 해당 직원 서버의 값이어야 합니다.

## 6. 한 번 실행해 확인하기

1. 본인 GitHub의 Actions를 엽니다.
2. Receive shared remote support updates를 선택합니다.
3. Run workflow로 한 번 실행합니다.
4. 초록색 완료인지 확인합니다. 단계에 deferred-support-in-progress가 나오면 지원 방이 있어서 배포를 미룬 것입니다. 지원 종료 후 다음 검사에서 다시 시도합니다.
5. 본인 홈페이지 /api/status에서 새 deployedVersion과 downloadReady를 확인합니다. 작업은 다운로드 EXE의 SHA-256까지 비교합니다.
6. 이후에는 약 30분 간격으로 확인합니다. GitHub 지연/점검/비활성화 상태에 따라 늦어질 수 있습니다.

한 번 정상 배포가 확인된 버전은 변경 없는 예약 검사 때 서버를 깨우지 않습니다. 새 발행본을 적용하거나 확인 실패를 재시도할 때만 서버 상태를 확인합니다.

## 7. 상담원 PC 프로그램 바꾸기

1. 기존 상담원 프로그램을 종료합니다.
2. 새 JHRemoteSupport.exe를 사용자에게 쓰기 권한이 있는 폴더에 둡니다. 예: 문서의 원격지원 폴더. EXE 이름은 그대로 둡니다.
3. 패키지의 JHRemoteSupport.exe.config도 같은 폴더에 둡니다.
4. 실행해서 이름과 본인 서버 주소를 확인합니다. 같은 Windows 사용자라면 기존 연결 설정이 남아 있습니다.
5. 이후 프로그램을 켜거나 대기 중 정기 검사에서 새 버전이 발견되면 검증·교체·재시작합니다. 코드 발급 대기/지원 중에는 적용하지 않습니다.
6. 필요하면 하단의 업데이트 확인을 누릅니다. 지원 중이면 종료 후 이용하라는 안내가 나옵니다.

v0.9 이하의 기존 EXE에는 이 기능이 없어서 위 교체가 한 번 필요합니다. 학생 EXE는 상담원 자동 업데이트에 참여하지 않습니다. 학생은 매번 홈페이지에서 새 파일을 받아 승인합니다.

## 8. 정현쌤이 다음 업데이트를 배포할 때

1. 변경한 코드와 client 어셈블리 버전, updates/release-version.txt의 버전을 함께 올립니다.
2. 새 버전 파일을 main에 반영하거나 Publish verified shared update를 실행합니다.
3. 빌드와 검사가 통과하면 새 서명된 발행본이 만들어집니다.
4. 직원은 기존 최초 설정이 유지되는 동안 자동으로 받습니다. GitHub 예약 작업이 꺼졌거나 Secret/권한이 바뀌면 다시 확인해야 합니다.
5. 이미 공개한 같은 버전을 덮어쓰지 않습니다. 수정이 필요하면 더 높은 버전을 발행합니다.

## 안 되거나 업데이트를 되돌릴 때

- 발행 실패: 관리자 Secret 이름, 공개키와 비밀키의 일치, 버전 중복, main 반영 여부를 확인합니다.
- 직원 동기화 실패: Actions 활성화·쓰기 권한·보호 브랜치를 확인합니다. 서명 검증 실패를 무시하지 않습니다.
- 배포 실패: 홈페이지 변수/배포 훅이 해당 직원 서버인지, 최초 v0.10 수동 적용이 됐는지 확인합니다.
- 배포 후 버전/해시가 안 맞음: 다음 예약 작업이 재시도합니다. Render의 실제 배포 로그를 확인합니다.
- 상담원 업데이트 실패: 기존 EXE를 유지하거나 복구합니다. 프로그램 폴더에 쓰기 권한이 있는지 확인합니다.
- 새 상담원 버전 실행에 문제가 있으면 프로그램을 닫고 현재 EXE를 다른 이름으로 보관합니다. 같은 폴더의 JHRemoteSupport.previous.exe를 JHRemoteSupport.exe로 이름을 바꿔 실행할 수 있습니다. 자동 검사 재시도를 잠시 막으려면 %LOCALAPPDATA%/JHRemoteSupport/updates/failed-version.txt에 문제가 있는 새 버전 번호를 기록합니다. 수동 업데이트 확인은 다시 시도할 수 있습니다.
- 서버를 되돌릴 때는 기존 GitHub 소스·EXE를 함께 복구하고 자동 수신 작업을 잠시 비활성화합니다. 이미 받은 더 높은 버전보다 낮은 발행본은 자동 적용되지 않습니다.

예약 작업이 60일 무활동으로 꺼질 수 있고, GitHub/Render 사용 정책·장애의 영향을 받습니다. 각자 Actions의 상태를 확인하세요. 지원 방 확인과 배포 사이에 새 접속이 생기는 경합까지 막는 점검 모드는 이번에 포함하지 않았습니다.

## 확인한 범위

자동 검사 66개, C# 컴파일/서명/파일 교체 단위 검사, 다운로드 UI 검사를 통과했습니다. Windows의 실제 프로세스 종료·재시작·파일 교체 GUI와 GitHub Actions/PowerShell/Render 실제 실행은 확인 전입니다. 먼저 아진쌤 등 한 명에게 적용해 Windows 7 SP1 학생과 기존 Windows 10/11 학생의 연결·승인·조작·종료를 확인한 뒤 확대하세요.

공식 안내:

- GitHub Secret: https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets
- GitHub 예약 작업: https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows
- Render Deploy Hook: https://render.com/docs/deploy-hooks
- Render 자동 배포: https://render.com/docs/deploys
