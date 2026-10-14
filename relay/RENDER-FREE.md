# 웹사이트 + 원격지원 중계서버 무료 설치

Render Hobby 워크스페이스와 **Free Web Service**를 사용합니다. 웹페이지와 중계서버는 같은 서비스에서 동작하며, 기본 `onrender.com` 주소와 TLS 인증서가 제공됩니다. 별도 도메인 구매나 Docker 설치가 필요하지 않습니다. 무료 한도를 넘으면 서비스가 중단될 수 있으므로 무제한 무료 운영은 아닙니다.

현재 패키지는 웹사이트·윈도우 프로그램의 **소스와 빌드 설정**입니다. 실제 사이트와 EXE가 이미 배포되어 있다는 뜻은 아닙니다. 먼저 템플릿 EXE를 만들어 서버에 넣어야 학생의 다운로드가 동작합니다.

## 1. 윈도우 EXE 만들기

1. ZIP 압축을 완전히 풀고 `client/Build.cmd`를 실행합니다.
2. 성공하면 `client/JHRemoteSupport.exe`가 만들어지며 `relay/downloads/JHRemoteSupport.exe`로도 복사됩니다.
3. 컴파일 실패 시 오류 메시지로 수정해야 합니다. 이 작업 환경에서는 Windows 컴파일과 실기 실행을 검증하지 못했습니다.

Windows 10/11의 .NET Framework 4.8 컴파일러를 사용합니다. 학생은 컴파일하지 않고 웹사이트에서 EXE만 내려받습니다.

윈도우 PC 대신 GitHub에서 빌드할 수도 있습니다. 저장소에 `.github/workflows/build-windows.yml`을 포함하고 Actions → **Build Windows remote support client → Run workflow**를 실행합니다. 표준 `windows-latest` 실행 환경을 사용하며, 공개 저장소는 표준 GitHub 호스팅 실행 환경이 무료입니다. 비공개 저장소는 계정의 Actions 무료 잔여 사용량을 확인합니다. 유료 대형 실행 환경은 사용하지 않습니다.

자동 빌드 성공 시 EXE를 Artifacts로 제공하고 `relay/downloads`에 저장하는 커밋도 수행합니다. 보호 브랜치·Actions 권한 때문에 커밋이 거절되면 Artifacts에서 파일을 받아 직접 올립니다. 자동 검증은 C# 컴파일, EXE에 붙인 접속 설정 읽기, 64비트 INPUT 구조체 크기까지입니다. GUI 화면 캡처와 두 PC 실기 조작 테스트는 아닙니다. **이 작업에서 GitHub 빌드는 아직 실행되지 않았습니다.**

## 2. GitHub에 전체 파일 올리기

https://github.com/ 에 로그인하고 `jh-remote-support` 저장소를 만듭니다. Add file → Upload files에서 **압축을 푼 remote-support 폴더 안의 내용**을 올리고 Commit changes를 누릅니다. ZIP 파일 자체를 올리거나 폴더를 중복으로 감싸지 않습니다.

| 저장소 최상위 폴더 | 포함 내용 |
| --- | --- |
| client | C# 프로그램과 윈도우 빌드 도구 |
| relay | server.py, webapp.py, requirements.txt, web 폴더, downloads 폴더 |
| tests | 서버·다운로드 테스트 |
| .github/workflows | 선택 사항: Windows 자동 빌드 |

`relay/downloads/JHRemoteSupport.exe`가 실제로 올라갔는지 확인합니다. 이 템플릿에는 운영 코드나 학생 정보가 없습니다.

## 3. Render 무료 서버 만들기

https://dashboard.render.com/ 에 로그인합니다. Hobby 워크스페이스에서 New → Web Service를 선택하고 GitHub의 `jh-remote-support` 저장소를 연결합니다.

| 설정 | 입력값 |
| --- | --- |
| Name | jh-remote-support 또는 사용 가능한 다른 이름 |
| Region | Singapore, 선택 가능한 경우 |
| Branch | main 또는 저장소의 기본 브랜치 |
| Language / Runtime | **Python 3** — Docker로 자동 선택되면 변경 |
| Root Directory | **relay** |
| Build Command | pip install -r requirements.txt |
| Start Command | python server.py |
| Instance Type / Compute Plan | **Free ($0)** |
| Health Check Path | /healthz |
| Environment Variable | **BIND_HOST = 0.0.0.0** |

PORT는 Render가 제공합니다. Create Web Service를 눌러 배포를 기다립니다. Render의 기본 주소는 다운로드 EXE에 자동으로 포함됩니다. 별도 도메인·DNS·인증서 설정은 필요하지 않습니다.

결제 수단이 없는 계정은 무료 전송량을 다 썼을 때 청구 대신 남은 월 서비스가 중단된다고 공식 문서가 안내합니다. 카드가 연결된 계정에서는 초과 전송량 비용이 발생할 수 있으므로 결제·요금제 상태를 확인합니다. 가입 과정에서 결제 확인이 요구되면 그 화면의 조건을 확인하고 진행합니다.

## 4. 주소와 이용 순서

실제 발급 주소가 `https://jh-remote-support-xxxx.onrender.com`인 경우:

| 용도 | 주소 |
| --- | --- |
| 학생 웹사이트 | https://jh-remote-support-xxxx.onrender.com/ |
| 정현쌤 프로그램 서버 주소 | wss://jh-remote-support-xxxx.onrender.com/support |
| 상태 확인 | https://jh-remote-support-xxxx.onrender.com/healthz |

위 주소는 예시이며 실제 Render 주소로 바꿉니다. 학생은 서버 주소를 입력하지 않습니다.

정현쌤 프로그램에서 **코드 발급 → 안내 복사**로 학생에게 웹주소와 코드를 전달합니다. 학생은 **웹사이트에서 코드 입력 → 다운로드 → EXE 실행 → 승인** 순서로 연결합니다. 내려받은 단일 EXE에는 코드와 주소가 들어 있습니다.

## 5. 무료 한도와 절전

2026-10-14에 확인한 공식 안내 기준이며 Billing의 최신 한도가 우선합니다.

- Free 서버 실행 시간: 워크스페이스 전체 합계 월 750시간.
- Hobby 포함 전송량: 월 5GB, 다른 서비스와 공유.
- 15분 미사용 시 절전, 다음 요청에서 약 1분 재시작.
- 결제 수단이 없으면 전송량 초과 후 남은 월 이용 중단. 결제 수단이 있으면 초과분 청구 가능.
- 무료 서비스는 재시작되거나 이용량 조건에 따라 중단될 수 있습니다.

원격지원 전에 웹사이트를 열고 페이지가 나올 때까지 기다린 뒤 코드 발급을 누릅니다. 프로그램의 연결 제한시간은 10초라 자는 서버에 바로 접속하면 실패할 수 있습니다. `/healthz`에서 `JH Remote Support Relay ready`를 확인할 수 있습니다. 서버를 계속 깨우는 자동 호출은 설정하지 않습니다. 서버 재시작 시 코드는 새로 발급합니다.

화면 JPEG가 장당 50KB이고 초당 6장인 경우 한 시간에 약 1.08GB라는 계산이 나옵니다. **실측이 아닌 예시**입니다. 실제 사용 후 Billing → Monthly Included Usage에서 확인합니다. 자주 하는 상담 업무에는 무료 전송량이 부족할 수 있습니다.

## 6. 오류 점검

| 증상 | 점검할 부분 |
| --- | --- |
| No open ports | BIND_HOST=0.0.0.0 |
| server.py를 못 찾음 | Root Directory=relay, 폴더 중복 여부 |
| webapp을 못 찾음 / 페이지 없음 | webapp.py와 web 폴더 업로드 여부 |
| 프로그램 배포 준비 중 메시지 | relay/downloads/JHRemoteSupport.exe 존재 여부 |
| handshake 오류 | 정현쌤 프로그램 주소의 wss://와 /support |
| 이전 코드 접속 실패 | 서버 재시작이나 10분 만료 후 새 코드 발급 |
| Windows 실행 경고 | 메시지를 전달. 보안 기능을 끄도록 안내하지 않음 |

웹페이지·다운로드·중계 기능의 로컬 테스트 **17개**가 통과했습니다. 다운로드 테스트의 EXE 바이트는 PE 모형이며 실제 실행 가능한 EXE를 검증한 결과가 아닙니다. Windows 빌드·실제 EXE 실행·인터넷 두 PC 연결·브라우저 시각 검증은 아직 완료되지 않았습니다.

공식 자료:
- https://render.com/docs/free
- https://render.com/docs/web-services
- https://render.com/docs/websocket
- https://render.com/docs/environment-variables
- https://render.com/articles/how-much-does-cloud-application-hosting-cost-for-small-businesses
- https://docs.github.com/en/actions/reference/runners/github-hosted-runners
