> v0.8 다운로드 배포 변경: 루트의 UPDATE-DOWNLOAD.md와 먼저읽어주세요.html을 먼저 읽으세요. 이 문서의 이전 EXE 설정 삽입 설명은 최신 방식과 다릅니다.

# SSL/TLS 연결 오류 수정 — v0.3

## 확인된 상태

2026-10-14 14시대에 실제 서버 `https://jh-remote-support.onrender.com/healthz`가 HTTP 200으로 응답했고, `wss://jh-remote-support.onrender.com/support`에 TLS 1.2로 접속하여 코드 발급도 성공했습니다. 점검용 연결은 종료했으며 접속 코드는 남겨두지 않았습니다.

첨부한 Windows 화면의 `SSL/TLS 보안 채널을 만들 수 없습니다`는 이 프로그램의 오래된 .NET TLS 기본값이 유력한 원인입니다. 초기 소스에는 .NET 4.8 대상으로 표시하는 속성과 최신 TLS 선택 설정이 누락되어 있었습니다. v0.3에서는 .NET 4.8 대상으로 표시하고, Windows가 최신 TLS를 선택하도록 앱 설정과 코드를 보완했습니다. 인증서 검증은 그대로 적용됩니다. Windows 전역 레지스트리 변경이 필요하지 않습니다.

종료 단축키는 F12가 Windows 디버거 예약 키이므로 `Ctrl + Alt + F10`으로 바꿨습니다. 이미 다른 프로그램이 사용 중이면 F11, F9 순서로 시도하고 실제 등록된 조합을 프로그램 하단에 표시합니다.

## 빠르게 TLS 오류만 해결하기

1. 실행 중인 정현쌤 원격지원 창을 모두 닫습니다.
2. 수정 패키지의 `client/JHRemoteSupport.exe.config`를 기존 `JHRemoteSupport.exe`가 있는 폴더에 복사합니다.
3. 파일 이름이 정확히 **JHRemoteSupport.exe.config**인지 확인합니다. 파일 이름 끝에 `.txt`가 붙으면 적용되지 않습니다.
4. 기존 `JHRemoteSupport.exe`를 다시 실행하여 코드 발급을 누릅니다.

이 설정 파일은 TLS 호환성 설정만 적용합니다. 단축키 수정과 학생용 다운로드 EXE까지 반영하려면 아래처럼 새로 빌드합니다. Windows 실제 실행은 이 작업 환경에서 검증하지 못했으므로 위 단계의 결과로 확인해야 합니다.

## 수정본 전체 적용

1. 실행 중인 원격지원 프로그램을 모두 닫습니다.
2. 최신 ZIP을 새 폴더에 완전히 압축 해제합니다.
3. `client/Build.cmd`를 실행합니다. 새 EXE를 만들고 현대적인 TLS 설정이 적용되었는지 자동 점검합니다.
4. `client/Start.cmd`를 실행합니다. 현재 배포용 수정본은 제목에 **v0.6**이 표시되어야 합니다.
5. 서버 주소는 `wss://jh-remote-support.onrender.com/support`입니다. 수정본 설정에 이 주소를 넣어두었습니다.
6. 웹사이트를 먼저 열어 무료 서버가 시작될 때까지 기다린 뒤 프로그램에서 코드 발급을 누릅니다.
7. 학생도 수정본을 받도록 GitHub의 **relay/downloads/JHRemoteSupport.exe**를 새로 생성된 같은 파일로 교체합니다.
8. GitHub 저장 후 Render 자동 배포가 끝나면 새 코드를 발급하고 학생은 파일을 다시 다운로드합니다.

Render의 Docker 설정은 기존대로 사용할 수 있습니다. 현재 화면의 Root Directory `relay/`, Docker build context `relay/`, Dockerfile `relay/./Dockerfile`, Health Check `/healthz` 값만으로 TLS 오류라고 판단할 근거는 없습니다. 서버 HTTPS·WSS 연결이 실제로 성공했으므로 서비스를 다시 만들 필요가 없습니다.

## 검증과 남은 확인

- 실제 Render 서버: HTTPS 상태 확인과 TLS 1.2 WebSocket 코드 발급 성공.
- 서버·웹·다운로드 로컬 통합 테스트: 17개 통과.
- C# 수정본 Windows 컴파일·TLS 연결·종료 단축키 등록: 사용자 PC에서 확인 필요.
- 화면 캡처와 마우스·키보드 입력: 두 Windows PC 연결에서 확인 필요.

수정본에서도 오류가 나오면 먼저 제목이 최신 버전인지 확인하고, 그 화면의 오류 메시지로 추가 진단합니다.

공식 참고:
- https://learn.microsoft.com/en-us/dotnet/framework/network-programming/tls
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
