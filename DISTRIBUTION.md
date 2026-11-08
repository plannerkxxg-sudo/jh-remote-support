> v0.10 안내: 자동 업데이트 최초 설정은 최상위 README.md와 AUTO-UPDATE-MANUAL.md를 먼저 따르세요. 아래는 이전 변경 기록입니다.

> v0.9 안내: 이 문서는 이전 버전 기록입니다. 현재 환경·배포·실행파일 적용은 최상위 README.md와 UPDATE-WINDOWS7.md를 우선하세요.

> v0.8 다운로드 배포 변경: 루트의 UPDATE-DOWNLOAD.md와 먼저읽어주세요.html을 먼저 읽으세요. 이 문서의 이전 EXE 설정 삽입 설명은 최신 방식과 다릅니다.

# 직원별 독립 서버로 배포하기 — v0.7

각 상담자가 자신의 GitHub 저장소와 Render Hobby 워크스페이스의 Free 서버를 사용합니다. 학생은 GitHub나 Render에 가입하지 않습니다. 학생은 상담자가 보낸 웹주소에서 숫자 6자리 코드를 입력하고 EXE를 실행·승인합니다.

v0.7 프로그램은 특정 상담자의 서버를 고정해서 사용하지 않습니다. **첫 실행에서 본인 사이트 주소와 표시 이름·소개 문구를 한 번 저장**하고, 다음 실행부터 자동으로 적용합니다. 설정은 Windows 사용자 폴더의 `%LOCALAPPDATA%/JHRemoteSupport/settings.json`에 저장됩니다. 연결 중에는 설정을 변경할 수 없습니다. 학생용 EXE는 자기 서버의 다운로드 기능이 넣은 주소와 코드로 접속하며 이 설정 창을 표시하지 않습니다.

## 정현쌤이 배포용 묶음을 준비하는 순서

1. `client/Build.cmd`를 실행하여 **v0.7 EXE를 한 번 빌드**합니다. 성공하면 `relay/downloads/JHRemoteSupport.exe`에도 복사됩니다.
2. 배포할 GitHub 저장소에 전체 프로젝트를 올립니다. 최상위에 `client`, `relay`, `.github`, `tests`, `render.yaml`이 있어야 합니다. `.exe`와 소스·빌드 설정만 올리고 설정 파일이나 계정 인증정보는 넣지 않습니다.
3. 다른 직원이 접근할 수 있게 저장소를 준비합니다. 접근 권한이 있는 비공개 저장소를 쓰거나, 별도의 배포용 소스 저장소를 공개할 수 있습니다. 이 작업에서 기존 비공개 저장소의 공개 범위를 변경하지 않았습니다.
4. 저장소 Settings에서 **Template repository**를 켭니다. 직원은 **Use this template → Create a new repository**로 자기 계정에 복사할 수 있습니다.
5. 직원에게 저장소 주소와 빌드된 `JHRemoteSupport.exe`를 전달합니다. 사전에 EXE를 포함하면 직원마다 다시 컴파일할 필요는 없습니다.

개인화 적용과 기존 서버 업데이트는 **UPDATE-PERSONAL-NAME.md**를 먼저 읽습니다. 안내 복사 링크의 사이트는 즉시 해당 이름으로 표시되며, 이름 없는 기본 주소에는 서버의 SUPPORT_NAME과 SUPPORT_DESCRIPTION 설정을 사용합니다. 새 Blueprint 설치에는 첫 배포 입력 항목이 포함되어 있습니다.

현재 ZIP에는 미리 컴파일한 EXE가 없습니다. 이 작업에서 Windows 빌드와 GitHub Template 등록, 각 직원의 서버 생성은 실행하지 않았습니다. 정현쌤 PC에서 빌드하거나 제공된 GitHub Actions Windows 빌드를 사용해야 합니다.

## 각 직원이 설치하는 순서

1. 자기 GitHub 계정에 로그인하고 배포용 저장소를 **Use this template**로 복사합니다.
2. 자기 Render 계정에 로그인합니다.
3. **New → Blueprint**를 선택하고 자기 저장소를 연결합니다. 최상위 `render.yaml`이 Free 서버 설정을 제공합니다. 생성 화면에서 **Free / $0**인지 확인한 뒤 진행합니다.
4. Blueprint 화면에서 결제 확인이 요구되거나 절차가 맞지 않으면 **New → Web Service**로 직접 설치합니다. 아래 설정은 같은 서버를 만드는 대안입니다. 같은 서비스를 두 번 만들 필요는 없습니다.

| 설정 | 값 |
| --- | --- |
| Runtime | Python 3 |
| Root Directory | relay |
| Build Command | pip install -r requirements.txt |
| Start Command | python server.py |
| Instance Type | Free ($0) |
| Health Check Path | /healthz |
| 환경변수 | BIND_HOST=0.0.0.0 |

5. 배포 완료 후 발급된 자기 HTTPS 사이트 주소를 복사합니다. 예: `https://직원개인사이트.onrender.com`.
6. 공통 `JHRemoteSupport.exe`를 실행합니다. 첫 실행의 **내 사이트 주소**에 방금 받은 HTTPS 주소와 자신의 표시 이름과 소개 문구를 넣고 저장합니다. `wss://`와 `/support`는 프로그램이 자동으로 처리합니다.
7. 이후에는 프로그램을 열고 **코드 발급 → 안내 복사**만 사용합니다. 자기 사이트와 자기 서버의 코드를 학생에게 전달합니다.
8. 주소를 바꿀 때만 하단 **연결 설정**을 사용합니다. 처음 실행에서 설정을 취소한 경우도 이 버튼으로 다시 등록할 수 있습니다.

## 설치 흐름

| 담당 | 해야 할 일 |
| --- | --- |
| 정현쌤 | 공통 EXE 빌드, 소스·서버 템플릿 제공 |
| 직원 | GitHub·Render 가입, 자기 서버 생성, 주소 한 번 등록 |
| 학생 | 직원의 웹사이트에서 코드 입력, 다운로드·실행·승인 |

서버와 웹주소가 직원별로 분리됩니다. 서로 다른 직원 서버에서 우연히 같은 6자리 코드가 나와도 웹주소가 다르면 다른 연결입니다. 안내 복사는 **웹주소와 코드 둘 다** 복사합니다.

GitHub Template로 복사한 저장소들은 독립된 저장소입니다. 원본을 수정해도 이미 복사한 모든 직원 저장소에 자동으로 업데이트되지는 않습니다. 수정본 배포 시 각 직원이 자기 저장소의 변경 파일 또는 EXE를 교체하면 자기 Render 자동 배포에 반영됩니다.

## 무료 사용량

각 직원의 독립된 Hobby 워크스페이스에는 해당 워크스페이스의 월 5GB 포함 전송량이 적용됩니다. 다른 서비스가 있으면 같은 한도를 공유합니다. 회사 전체가 한 서버의 5GB를 공유하는 구조에서 벗어나지만, 각 직원의 서버가 무제한이 되는 것은 아닙니다.

**현재 화면 전송 설정에서 직원 한 명이 하루 30분씩 매일 사용하면 한 직원의 5GB도 부족할 수 있습니다.** 화면 변화 시 전송·전송 빈도 감소 등의 최적화는 이 버전에 추가하지 않았습니다. 결제 수단이 연결되면 초과 사용량이 청구될 수 있으므로 무료 이용을 원하는 사람은 자기 요금제와 결제 상태를 확인해야 합니다.

## 확인 범위

- 기존 서버·웹·다운로드 테스트 28개는 통과했습니다.
- v0.7 Windows 설정 저장·재실행과 GUI 실기는 이 환경에서 확인하지 못했습니다.
- Build.cmd와 Windows GitHub Actions에 HTTPS 주소→WSS 접속 주소 변환 검증을 추가했습니다. Windows 빌드 시 실행됩니다.
- Render Blueprint는 Free 설정으로 작성하고 로컬 구조를 확인했습니다. 실제 Render 계정에서의 설치 검증은 남아 있습니다.

공식 자료:
- https://docs.github.com/en/repositories/creating-and-managing-repositories/creating-a-template-repository
- https://docs.github.com/en/repositories/creating-and-managing-repositories/creating-a-repository-from-a-template
- https://render.com/docs/infrastructure-as-code
- https://render.com/docs/blueprint-spec
- https://render.com/docs/free
