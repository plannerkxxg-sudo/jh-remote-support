'use strict';
const form = document.getElementById('connect-form');
const input = document.getElementById('support-code');
const button = document.getElementById('download');
const feedback = document.getElementById('feedback');
const success = document.getElementById('after-download');
input.addEventListener('input', () => {
  input.value = input.value.toUpperCase().replace(/[^A-Z0-9\- ]/g, '');
  input.removeAttribute('aria-invalid');
  feedback.textContent = '';
  success.hidden = true;
});
form.addEventListener('submit', async (event) => {
  event.preventDefault();
  const code = input.value.toUpperCase().replace(/[\s-]/g, '');
  if (!/^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{12}$/.test(code)) {
    feedback.textContent = '전달받은 12자리 코드를 확인해주세요. 영문과 숫자를 정확히 입력하세요.';
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
    const response = await fetch('/download?code=' + encodeURIComponent(code), {
      cache: 'no-store', credentials: 'omit', signal: controller.signal, referrerPolicy: 'no-referrer'
    });
    if (!response.ok) {
      let error;
      try { error = await response.json(); } catch { error = {}; }
      throw new Error(error.message || '파일을 준비하지 못했습니다. 잠시 후 다시 시도해주세요.');
    }
    if (!response.headers.get('Content-Type')?.includes('application/octet-stream')) {
      throw new Error('다운로드 응답을 확인하지 못했습니다. 잠시 후 다시 시도해주세요.');
    }
    const blob = await response.blob();
    if (blob.size < 1000 || blob.size > 2000000) throw new Error('실행파일을 확인하지 못했습니다. 정현쌤에게 알려주세요.');
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'JHSupport-' + code + '.exe';
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 60000);
    success.hidden = false;
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
