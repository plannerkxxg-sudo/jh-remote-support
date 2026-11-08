> v0.10 안내: 자동 업데이트 최초 설정은 최상위 README.md와 AUTO-UPDATE-MANUAL.md를 먼저 따르세요. 아래는 이전 변경 기록입니다.

# v0.9 Windows 7 SP1 호환 적용

**실기기 확인이 필요한 시험 적용본입니다.** 코드·서버 자동 검사 결과는 README.md를 참고하세요.

## 정현쌤이 할 일

1. 기존 GitHub 소스와 현재 상담원 EXE를 보관하세요. 업데이트 중에는 원격지원 세션을 진행하지 않습니다.
2. ZIP 압축을 모두 풉니다. 이번 패키지에는 빌드된 client/JHRemoteSupport.exe와 같은 내용의 relay/downloads/JHRemoteSupport.exe가 들어 있습니다.
3. 기존 GitHub 저장소 최상위에서 Add file → Upload files를 엽니다.
4. 이번 패키지의 relay 폴더를 끌어 넣어 동일한 경로의 파일을 교체합니다. 이미 GitHub의 relay 안에 들어와 있다면 폴더가 아닌 그 안의 내용을 올립니다. 최종 경로는 relay/server.py, relay/webapp.py, relay/web/index.html, relay/downloads/JHRemoteSupport.exe 형태여야 합니다.
5. 현재 사용하는 브랜치에 변경을 반영합니다. 보호된 브랜치는 새 브랜치/PR로 반영해야 할 수 있습니다.
6. Render의 기존 서비스를 열어 최신 커밋을 배포합니다. 자동 배포가 켜져 있으면 완료를 기다리고, 꺼져 있으면 Manual Deploy → Deploy latest commit으로 배포합니다. 기존 Docker/Python 서비스 설정을 바꾸거나 새 서비스를 만들지 않습니다.
7. 본인 홈페이지 주소 + /api/status에 접속합니다. 예를 들어 정현쌤은 https://jh-remote-support.onrender.com/api/status 입니다. 직원은 자신의 주소를 사용합니다.
8. version: 0.9와 downloadReady: true를 확인합니다. true가 아니면 구버전 EXE·누락된 파일·PUBLIC_BASE_URL/Render 주소 설정 등을 확인합니다. 서버 전체 로그 화면을 보내도 좋지만 비밀번호/토큰 값은 가립니다.
9. 기존 상담원 프로그램을 닫고 새 client/JHRemoteSupport.exe를 실행합니다. 같은 Windows 사용자라면 기존 이름/주소 설정을 읽습니다. EXE 옆의 .config도 함께 둡니다.
10. 연결 설정에서 이름/실제 서버 주소를 확인하고 새 코드를 발급합니다. 안내 복사를 학생에게 보냅니다. 예전에 받은 학생 EXE는 재사용하지 않습니다.

상담원에게 주는 공통 EXE를 학생에게 직접 보내지 마세요. 학생은 자기 접속 코드와 서버 주소가 담긴 이름으로 홈페이지에서 내려받아야 합니다. 받은 학생 파일 이름은 바꾸지 않습니다.

## 아진쌤·민주쌤이 할 일

각자 기존 GitHub/Render에 위의 relay 업데이트를 적용하고 새 상담원 EXE를 받습니다.
아진쌤은 이름을 아진쌤, 민주쌤은 민주쌤으로 저장하며 각각 실제 자기 서버 주소를 입력합니다.
원본 템플릿 업데이트는 직원의 복사본에 자동으로 전파되지 않습니다.
기본 홈페이지의 SUPPORT_NAME/SUPPORT_DESCRIPTION과 상담원 PC 설정은 별도입니다.
name/description이 붙은 안내 링크는 표시 문구를 바꾸며 서버 주소 자체를 바꾸지 않습니다.
client 소스 폴더는 직원의 운영용 GitHub에 없어도 됩니다.

## 학생 PC가 Windows 7이라면

1. 윈도우 키 + R → winver → 엔터를 눌러 Windows 7 Service Pack 1인지 확인합니다.
2. .NET Framework 4.8이 없다는 안내가 나오면 Microsoft 공식 페이지에서 Runtime을 설치합니다. Developer Pack이 아닙니다. Windows 7에 4.8.1을 설치하려고 하지 않습니다.
3. 설치 후 PC를 재시작합니다. OS 업데이트/인증서 설치가 막히면 PC 관리 담당자에게 확인합니다.
4. 새 코드로 홈페이지에서 다시 다운로드합니다. 파일 이름을 바꾸지 않고 실행합니다.
5. 승인 창에서 지원자 이름·서버를 확인하고 동의합니다. 그때부터 화면/마우스/키보드를 공유합니다.
6. 담당자 화면에서 학생 화면이 보이고 클릭/입력이 전달되는지 확인합니다. 마지막으로 학생이 즉시 종료를 눌러 실제로 연결이 끊기는지 확인합니다.

.NET 자체가 너무 오래됐거나 없으면 프로그램의 사전 안내도 실행하지 못할 수 있습니다. client/CheckEnvironment.cmd는 .NET 없이 실행할 수 있으므로 상담원에게 진단 화면 사진을 전달합니다. 이 도구는 설정을 바꾸지 않습니다.

## 처음 시험할 때 확인할 것

- Windows 7 SP1: 승인 전에는 화면이 안 보임 → 승인 후 화면 표시 → 클릭 → 한글 입력 → 즉시 종료.
- Windows 10/11: 같은 순서로 확인. 기존에 되던 PC도 한 번 확인합니다.
- 거절하거나 창을 닫으면 조작이 이어지지 않는지 확인합니다.
- 충분히 시험한 뒤 다른 직원들에게 배포합니다. 실패하면 기존 GitHub 소스/EXE를 함께 복구할 수 있습니다.

한글 입력은 기존 가상 키 입력 방식이어서 PC의 입력기/한영 상태에 따라 결과가 다를 수 있습니다. 실제 학생의 수강신청 화면에서 확인하세요. UAC 보안 화면은 지원하지 않습니다.

## 그래도 안 된다면

- 충돌: 오류창 전체 사진과 Windows 버전을 확인합니다. 새 파일인지도 확인합니다.
- .NET 안내: 4.8 Runtime 설치 여부와 재시작을 확인합니다.
- 인증서/암호화 오류: PC 날짜·시간, Windows 업데이트와 루트 인증서를 확인합니다. 인증서 확인을 끄지 않습니다.
- 연결 실패: 상담원/학생이 같은 서버를 쓰는지, 새 코드인지, 인터넷과 회사망 정책을 확인합니다.
- 새 EXE에서 오류가 기록됐다면 %LOCALAPPDATA%/JHRemoteSupport/logs/last-error.txt를 확인합니다. 자동 전송되지 않습니다. 화면·키 입력·접속 코드·비밀번호는 기록하지 않습니다.

직접 HTTPS/WSS 연결이 필요한 버전입니다. 인증 프록시/PAC를 강제하는 회사망은 별도 지원이 필요할 수 있습니다.

## 공식 안내

- .NET 4.8: https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48
- .NET 요구사항: https://learn.microsoft.com/ko-kr/dotnet/framework/get-started/system-requirements
- GitHub 업로드: https://docs.github.com/en/repositories/working-with-files/managing-files/adding-a-file-to-a-repository
- Render 배포: https://render.com/docs/deploys
