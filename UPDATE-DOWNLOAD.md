> v0.10 안내: 자동 업데이트 최초 설정은 최상위 README.md와 AUTO-UPDATE-MANUAL.md를 먼저 따르세요. 아래는 이전 변경 기록입니다.

> v0.9 안내: 이 문서는 이전 버전 기록입니다. 현재 환경·배포·실행파일 적용은 최상위 README.md와 UPDATE-WINDOWS7.md를 우선하세요.

# v0.8 적용 순서 - 정현쌤이 먼저 진행하세요

## 먼저 알아둘 것

무료로 할 수 있는 배포 구조 개선입니다. **Chrome의 '의심스러운 다운로드' 경고가 사라지는 것은 보장하지 않습니다.** 알려지지 않은 파일, 사이트 판단, 악성파일 탐지 등은 각각 확인해야 합니다. 학생에게 보호 기능을 끄도록 안내하지 않습니다.

이번 ZIP은 소스와 서버 파일입니다. **완성된 v0.8 EXE는 Windows에서 새로 만들어야 합니다.** Windows 빌드와 Chrome 실기 시험은 이 환경에서 수행하지 못했습니다. GitHub·Render에 자동 반영한 상태도 아닙니다.

## 1. Windows에서 새 실행파일 만들기

1. ZIP을 내려받습니다.
2. ZIP을 마우스 오른쪽 → 모두 압축 풀기 → 압축 풀기를 누릅니다.
3. 풀린 폴더의 client를 엽니다.
4. Build.cmd를 두 번 누릅니다.
5. Build complete: JHRemoteSupport.exe가 나오는지 확인합니다.
6. client/JHRemoteSupport.exe와 relay/downloads/JHRemoteSupport.exe가 생겼는지 확인합니다.

이전 v0.7 실행파일을 복사해서 쓰면 안 됩니다. Build.cmd가 새 v0.8 소스를 컴파일해야 합니다. GitHub Windows 빌드를 쓰는 경우 새 client 소스와 workflow도 반영합니다.

## 2. GitHub의 같은 위치에 올리기

진행 중인 원격지원을 끝내고 시작합니다. 직원별 서버라면 각 직원의 GitHub에도 같은 작업이 필요합니다.

| GitHub에서 먼저 열 폴더 | 내 PC에서 올릴 새 파일 |
|---|---|
| relay | server.py, webapp.py, branding.py, Dockerfile, requirements.txt |
| relay/web | index.html, app.js, style.css |
| relay/downloads | 1단계에서 만든 JHRemoteSupport.exe, JHRemoteSupport.sha256.txt |

각 폴더에서 **Add file → Upload files**로 파일을 올립니다. 변경을 main에 저장합니다. 직접 저장할 수 없으면 변경 제안 후 Pull request를 병합합니다.

폴더를 중복해서 만들지 마세요. relay/relay/server.py가 아니라 relay/server.py입니다. ZIP 자체를 서버 파일 대신 올리지 않습니다. 직원 배포용 템플릿도 갱신합니다. 이미 만든 직원 복사본은 자동 업데이트되지 않습니다.

직원 GitHub에는 client가 없어도 됩니다. 정현쌤이 만든 새 EXE를 직원에게 전달하고, 그 EXE를 각자의 relay/downloads에도 올리면 됩니다.

## 3. 기존 Render에서 다시 배포하기

1. Render에 로그인하고 기존 서비스를 엽니다. 새 서버는 만들지 않습니다.
2. 현재 Docker 또는 Python 실행 방식을 유지합니다.
3. Manual Deploy → Deploy latest commit으로 최신 파일을 배포합니다.
4. 최신 배포가 Live가 될 때까지 기다립니다.
5. 본인 사이트 주소 끝에 /api/status를 붙여 엽니다.
6. version이 0.8, downloadReady가 true인지 확인합니다.

false면 새 EXE가 relay/downloads/JHRemoteSupport.exe에 올라갔는지 확인하세요. 구버전 EXE는 새 접속 파일명을 해석하지 못하므로 서버가 다운로드를 중단합니다. 이때 나오는 업데이트 안내는 정상입니다.

SUPPORT_NAME과 SUPPORT_DESCRIPTION은 기존 값을 사용합니다. 아진쌤 서버면 아진쌤 값이어야 합니다. 기본 사이트 이름이 바뀌지 않으면 오래된 ?name= 링크 대신 기본 주소로 확인합니다.

## 4. 직원 프로그램도 교체하기

1. 기존 프로그램을 닫습니다.
2. 새 client/JHRemoteSupport.exe를 실행합니다.
3. 저장된 주소·이름·소개를 확인합니다. 필요하면 연결 설정에서 수정합니다.
4. 홈페이지를 연 뒤 새 코드를 발급합니다.
5. 안내 복사를 눌러 다른 Windows PC에 보냅니다.

## 5. 다른 PC에서 Chrome으로 확인하기

1. 홈페이지에서 새 코드 입력 → 지원 프로그램 다운로드.
2. Chrome 다운로드 목록에서 완료 여부를 봅니다.
3. 완료한 파일을 **이름을 바꾸지 않고** 실행합니다.
4. 파일명은 JHSupport-123456-tls-본인서버.onrender.com.exe처럼 길어집니다. 서버와 코드를 전달하기 위한 이름입니다. 파일 내용은 코드에 따라 바꾸지 않습니다.
5. 학생 승인 창에서 담당자 이름과 서버를 확인합니다.
6. 승인 후 메모장 입력과 마우스 이동을 시험하고 즉시 종료합니다.

Chrome이 붙인 (1), (2) 같은 중복 파일명은 처리합니다. 직접 다른 이름으로 바꾸면 자동 접속할 수 없습니다. 새 코드를 받으면 새로 다운로드합니다. 매우 긴 서버 주소나 IPv6 개발 주소는 이 자동 파일명 방식에서 지원되지 않을 수 있습니다. Render 기본 주소와 일반 HTTPS 도메인을 사용하세요.

## 6. 여전히 Chrome이 차단하면

이번 수정만으로 Chrome이 바로 신뢰하는 것은 아닙니다. 경고 문구와 실제 배포 EXE를 기준으로 확인해야 합니다. 학생에게 우회 실행을 요구하지 말고 배포 담당자가 먼저 파일을 점검합니다.

1. 실제 받은 파일과 원본의 SHA-256을 비교합니다. 홈페이지의 '배포 파일 정보 확인'에 해시가 표시됩니다. Windows PowerShell에서 Get-FileHash -Algorithm SHA256 -LiteralPath '받은 파일의 실제 경로'로 비교합니다. 해시 일치는 내용 일치만 의미합니다.
2. 원본과 다운로드 파일을 Windows 보안 등으로 검사하고 코드·배포 환경을 점검합니다. 단순 미확인 파일 경고와 실제 악성 탐지를 구분합니다.
3. Search Console에 본인 사이트를 URL 접두어 속성으로 등록하고 소유권을 확인합니다. HTML 태그 방식에서 받은 content 값만 Render의 GOOGLE_SITE_VERIFICATION에 넣고 재배포하면 홈페이지에 반영됩니다. 전체 meta 태그를 값으로 넣지 않습니다. 공개 사이트 소유권 확인용 값입니다.
4. Search Console의 **보안 문제** 보고서에 문제가 있을 때, Google 안내에 따라 원인을 해결하고 검토를 요청합니다. 보고서에 문제가 없다면 검토 요청으로 낮은 다운로드 인지도가 해결된다고 보장할 수 없습니다.

공인 코드서명은 게시자 확인과 배포 무결성에 도움이 되지만 유료 서명을 붙여도 Chrome 또는 SmartScreen 경고 제거를 보장하지 않습니다. 이번 패키지에는 공인 서명이 포함되어 있지 않습니다. 서명 도입 시 최종 EXE를 서명·검증한 뒤 그 파일을 client 배포용과 relay/downloads에 동일하게 두고 SHA-256을 다시 계산하세요. 다시 빌드하면 서명도 다시 해야 합니다.

## 확인 결과

- 로컬 HTTP/WebSocket 자동 검사 36개 통과
- JavaScript 다운로드 UI 검사와 구문 검사 통과
- 실제 Windows 빌드·연결·Chrome 차단 여부는 미검증
- 실제 GitHub·Render 배포와 Google 검토 요청은 수행하지 않음

공식 안내(2026-10-21 확인):

- https://support.google.com/chrome/answer/6261569?hl=ko
- https://developers.google.com/search/docs/monitor-debug/security/malware
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation

사이트 소유권 확인 공식 안내: https://support.google.com/webmasters/answer/9008080?hl=ko
