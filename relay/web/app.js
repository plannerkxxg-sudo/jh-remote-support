'use strict';
const form = document.getElementById('connect-form');
const input = document.getElementById('support-code');
const button = document.getElementById('download');
const feedback = document.getElementById('feedback');
const success = document.getElementById('after-download');
const fileInfo = document.getElementById('file-info');
const fileName = document.getElementById('file-name');
const fileHash = document.getElementById('file-hash');
input.addEventListener('input', () => {
  input.value = input.value.replace(/[^0-9]/g, '').slice(0, 6);
  input.removeAttribute('aria-invalid');
  feedback.textContent = '';
  success.hidden = true;
  fileInfo.hidden = true;
});
form.addEventListener('submit', async (event) => {
  event.preventDefault();
  const code = input.value.toUpperCase().replace(/[\s-]/g, '');
  if (!/^[0-9]{6}$/.test(code)) {
    feedback.textContent = '전달받은 숫자 6자리 코드를 정확히 입력해주세요.';
    input.setAttribute('aria-invalid', 'true');
    input.focus();
    return;
  }
  success.hidden = true;
  feedback.textContent = '';
  button.disabled = true;
  button.firstElementChild.textContent = '프로그램을 준비하고 있어요…';
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 15000);
  try {
    const response = await fetch('/api/download-info?code=' + encodeURIComponent(code), {
      cache: 'no-store', credentials: 'omit', signal: controller.signal, referrerPolicy: 'no-referrer'
    });
    if (!response.ok) {
      let error;
      try { error = await response.json(); } catch { error = {}; }
      throw new Error(error.message || '파일을 준비하지 못했습니다. 잠시 후 다시 시도해주세요.');
    }
    if (!response.headers.get('Content-Type')?.includes('application/json')) {
      throw new Error('다운로드 응답을 확인하지 못했습니다. 잠시 후 다시 시도해주세요.');
    }
    const info = await response.json();
    if (info.version !== '0.10' || !/^[a-f0-9]{64}$/.test(info.sha256)
        || !/^[A-Za-z0-9.~-]+\.exe$/.test(info.filename)
        || !info.filename.startsWith('JHSupport-' + code + '-')
        || info.size < 1024 || info.size > 1500000
        || info.downloadUrl !== '/download?code=' + code) {
      throw new Error('배포 파일 정보를 확인하지 못했습니다. 담당자에게 알려주세요.');
    }
    fileName.textContent = info.filename;
    fileHash.textContent = info.sha256;
    fileInfo.hidden = false;
    // A normal same-origin HTTPS attachment. The browser retains its download checks.
    const link = document.createElement('a');
    link.href = info.downloadUrl;
    link.download = info.filename;
    link.rel = 'noreferrer';
    document.body.appendChild(link);
    link.click();
    link.remove();
    success.hidden = false;
    feedback.textContent = '다운로드를 요청했습니다. 완료 여부는 브라우저 다운로드 목록에서 확인하세요.';
    success.scrollIntoView({ behavior: 'auto', block: 'nearest' });
  } catch (error) {
    feedback.textContent = error.name === 'AbortError'
      ? '서버가 시작 중일 수 있습니다. 약 1분 뒤 다시 시도해주세요.'
      : error.message || '연결하지 못했습니다. 인터넷 연결을 확인해주세요.';
  } finally {
    clearTimeout(timeout);
    button.disabled = false;
    button.firstElementChild.textContent = '지원 프로그램 다운로드';
  }
});
