# JH Remote Support v0.8 - 다운로드 배포 방식 개선

학생이 코드 입력 → Windows EXE 다운로드·실행 → 이름·서버 확인 및 동의 → 화면과 마우스·키보드 공유.

**Chrome 차단 해제를 보장하는 버전이 아닙니다.** 알려지지 않은 파일도 경고할 수 있습니다. 이번 수정은 실행파일 변형을 없애고 배포 파일을 확인하기 쉽게 만드는 개선이며, Chrome에서 실제 경고가 줄어드는지는 검증하지 못했습니다. 브라우저·Windows의 보안 기능을 끄지 않습니다.

## 적용 순서

압축을 완전히 푼 뒤 **먼저읽어주세요.html** 또는 **UPDATE-DOWNLOAD.md**를 엽니다.

1. Windows에서 client/Build.cmd 실행. 이 패키지에는 미리 컴파일된 EXE가 없습니다.
2. GitHub의 서버 파일과 relay/downloads/JHRemoteSupport.exe를 새 파일로 갱신.
3. 기존 Render 서비스를 최신 커밋으로 재배포. 기존 주소·이름·소개를 유지합니다.
4. /api/status에서 version이 0.8, downloadReady가 true인지 확인.
5. 두 Windows PC로 다운로드·이름·승인·화면·조작·종료를 시험합니다.

구버전 EXE와 새 서버를 섞지 않습니다. 새 서버는 구버전 EXE를 감지하면 다운로드를 중단하고 업데이트 안내를 표시합니다. 직원 GitHub에 client가 없어도 완성된 EXE와 서버 파일로 운영할 수 있습니다. 직원별 서버는 각각 갱신해야 합니다.

## 변경 내용

- EXE 뒤에 JSON 설정을 추가하지 않습니다. 같은 빌드의 실행파일 바이트는 모든 코드·직원 서버에서 동일합니다.
- JHSupport-123456-tls-본인서버.onrender.com.exe처럼 접속 코드와 서버를 파일 이름으로 전달합니다. 난독화나 압축으로 검사를 피하는 방식이 아닙니다. 학생 파일 이름을 직접 바꾸지 마세요. Chrome의 중복 파일 표시 (1), (2)는 처리합니다.
- 정상 HTTPS 첨부파일 다운로드를 요청하며 브라우저의 파일 검사를 유지합니다. Blob으로 파일을 다시 만들지 않습니다.
- 배포 버전·목적·SHA-256을 표시합니다. SHA-256은 내용 비교용이며 제작자 인증이나 안전 판정이 아닙니다.
- 이름·소개는 실제 연결 방의 서버 응답에서 받아 표시합니다. 동의 창에 접속 서버도 표시합니다.
- 공인 코드서명을 도입해도 서버는 서명된 파일을 수정하지 않습니다. 이번 패키지에 공인 서명이나 인증서를 구매·적용한 것은 아닙니다.
- 선택 사항: GOOGLE_SITE_VERIFICATION 환경변수를 홈페이지 인증 메타 태그에 반영합니다. Search Console URL 접두어 속성을 본인이 인증할 때 사용할 수 있습니다.

## 검증 결과와 남은 확인

2026-10-21 기준 로컬 HTTP/WebSocket 자동 검사 **36개 통과**. 세션별 실행파일 바이트 일치, SHA-256, 이름·소개, 동의 전 차단, 종료, 만료, 세션 격리, 구버전 EXE 차단을 검사했습니다. node tests/test_download_ui.js와 node --check relay/web/app.js도 통과했습니다.

테스트 PE 바이트는 모형입니다. **실제 Windows 컴파일·파일명 자동 접속·화면 캡처·입력 제어·Chrome 다운로드 경고 여부는 이 환경에서 실행하지 못했습니다.** Build.cmd와 GitHub Windows 빌드에 파일명 해석·TLS·설정 검사를 추가했습니다. 실제 운영 전 Windows 두 PC에서 확인하세요. GitHub·Render에 반영하거나 Google에 검토 요청한 상태는 아닙니다.

## 유지되는 기능과 범위

숫자 6자리 코드와 앞자리 0 보존, 대기 10분·동의 60초·코드 발급부터 최대 2시간, 승인 후 기본 모니터 공유와 마우스·키보드 조작, 종료 즉시 해제. 무인 접속·자동 시작은 없습니다. 담당자별 이름·소개와 전체 안내 링크를 유지합니다. 기본 사이트 이름은 Render 환경변수에서 설정합니다.

Windows 10/11과 .NET Framework 4.8 대상입니다. 안드로이드·아이폰 제어는 미구현입니다. 공용 인터넷 HTTPS/WSS, 개발용 HTTP/WS는 localhost·127.0.0.1만 자동 파일명 전달에 사용합니다. 화면은 최대 1280px, JPEG 55, 약 6fps입니다. 파일 전송·다중 모니터 선택·UAC 보안 화면 조작은 미지원입니다.

한 창은 한 학생과 연결합니다. 여러 방은 분리되지만 동시 접속 성능과 월 데이터 사용량은 별도입니다. 화면과 입력은 중계서버를 거치며 PC↔서버 TLS 암호화입니다. 종단간 암호화는 아닙니다. 저장·녹화 기능은 없으며 담당자 이름은 직접 입력한 표시 이름입니다.

## 공식 안내

- Google Chrome 다운로드 차단: https://support.google.com/chrome/answer/6261569?hl=ko
- Google 소프트웨어 문제·사이트 검토: https://developers.google.com/search/docs/monitor-debug/security/malware
- Microsoft 서명·SmartScreen: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation

기존 UPDATE 문서와 TLS 문서는 이전 버전의 변경 기록입니다. 다운로드 배포 방식은 이 문서와 UPDATE-DOWNLOAD.md를 우선합니다.
