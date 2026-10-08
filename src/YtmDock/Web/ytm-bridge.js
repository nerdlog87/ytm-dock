// YTM Dock bridge — 문서 생성 시마다 주입된다. window.__YTM_DOCK_CSS__ 는 C# 이 먼저 넣어 준다.
(function () {
  if (location.hostname !== 'music.youtube.com') return;

  const STYLE_ID = 'ytm-dock-mini';        // 미니 모드에서만 켜지는 스타일
  const BASE_STYLE_ID = 'ytm-dock-base';   // 항상 켜져 있는 스타일 (토글 버튼 등)
  const TOGGLE_ID = 'ytm-dock-toggle';

  // ⤢ 버튼 스타일은 미니/전체와 무관하게 항상 살아 있어야 한다.
  // (처음엔 미니 CSS 안에 넣었다가, 전체 모드에서 CSS 가 빠지며 버튼이 사라지는 사고가 있었다)
  const BASE_CSS = `
    #${TOGGLE_ID} {
      position: fixed; right: 4px; z-index: 2147483647;
      width: 22px; height: 22px; line-height: 22px; text-align: center;
      font-size: 13px; color: #ccc; background: rgba(0,0,0,.55);
      border: 0; border-radius: 4px; cursor: pointer; opacity: .75;
      app-region: no-drag; -webkit-app-region: no-drag;
    }
    #${TOGGLE_ID}:hover { opacity: 1; color: #fff; }
    /* 등장/퇴장 시 내용 페이드 (창 자체는 C# 이 슬라이드) */
    ytmusic-app-layout > ytmusic-player-bar, ytmusic-app-layout > ytmusic-player-page, ytmusic-app-layout > #nav-bar-background, ytmusic-app-layout > #layout {
      transition: opacity .32s ease-out;
    }
    html.ytm-dock-hidden ytmusic-app-layout > * { opacity: 0 !important; }
  `;

  function ensureBaseStyle() {
    if (document.getElementById(BASE_STYLE_ID)) return;
    const el = document.createElement('style');
    el.id = BASE_STYLE_ID;
    el.textContent = BASE_CSS;
    document.documentElement.appendChild(el);
  }

  function ensureToggleButton() {
    if (document.getElementById(TOGGLE_ID)) return;
    if (!document.body) { document.addEventListener('DOMContentLoaded', ensureToggleButton, { once: true }); return; }
    const b = document.createElement('button');
    b.id = TOGGLE_ID;
    b.title = 'YTM Dock: 미니 / 전체 전환 (Esc 로도 미니로 돌아감)';
    b.addEventListener('click', (e) => {
      e.stopPropagation();
      window.chrome?.webview?.postMessage('toggle');
    });
    document.body.appendChild(b);
  }

  window.ytmDock = {
    mini: true,

    applyMini(on) {
      ensureBaseStyle();
      let el = document.getElementById(STYLE_ID);
      if (on) {
        if (!el) {
          el = document.createElement('style');
          el.id = STYLE_ID;
          el.textContent = window.__YTM_DOCK_CSS__ || '';
          document.documentElement.appendChild(el);
        }
      } else {
        el?.remove();
      }
      this.mini = !!on;
      ensureToggleButton();
      const t = document.getElementById(TOGGLE_ID);
      if (t) {
        t.textContent = on ? '⤢' : '⤡';
        // 미니: 바 오른쪽 아래 구석 / 전체: 하단 플레이어 바 바로 위
        t.style.bottom = on ? '2px' : '80px';
        t.style.width = t.style.height = t.style.lineHeight = on ? '16px' : '22px';   // 미니에선 작게
      }
    },

    setShown(on) { document.documentElement.classList.toggle('ytm-dock-hidden', !on); },

    playPause() { document.querySelector('#play-pause-button')?.click(); },

    // 볼륨 ±delta. 플레이어 API(setVolume)는 슬라이더 UI 가 안 따라오고, 슬라이더는 청감 곡선이 적용된
    // 값이라(슬라이더 90 → 실제 74) 슬라이더 값을 바꾸고 change 를 쏘는 쪽이 정답 (2026-10-09 실측).
    volume(delta) {
      const s = document.querySelector('ytmusic-app-layout > ytmusic-player-bar #volume-slider');
      if (!s) return;
      const v = Math.max(0, Math.min(100, Math.round((s.value ?? 0) + delta)));
      s.value = v;
      s.dispatchEvent(new CustomEvent('change', { bubbles: true }));
      // 1초 동안 볼륨 팝업을 보여 준다
      s.classList.add('ytm-dock-volume-show');
      clearTimeout(this._volT);
      this._volT = setTimeout(() => s.classList.remove('ytm-dock-volume-show'), 1000);
    },
    next()      { document.querySelector('.next-button')?.click(); },
    prev()      { document.querySelector('.previous-button')?.click(); },
  };

  // ── 긴 제목/가수 마키 스크롤 ──────────────────────────────────────────────
  // DOM 을 건드리지 않고 scrollLeft 만 움직인다 (yt-formatted-string 이 텍스트를 갈아끼워도 안전).
  // 상태: 처음 1.6초 대기 → 끝까지 30px/s 로 이동 → 1.6초 대기 → 빠르게 되감기 → 반복
  const SPEED = 30, HOLD = 1600;
  const mq = new WeakMap();
  let lastTs = 0;

  function tickMarquee(ts) {
    requestAnimationFrame(tickMarquee);
    const dt = lastTs ? Math.min(ts - lastTs, 100) : 0;
    lastTs = ts;
    if (!window.ytmDock.mini) return;

    const bar = document.querySelector('ytmusic-app-layout > ytmusic-player-bar');
    if (!bar) return;
    for (const el of bar.querySelectorAll('.content-info-wrapper .title, .content-info-wrapper .byline')) {
      const max = el.scrollWidth - el.clientWidth;
      let st = mq.get(el);
      const text = el.textContent;
      if (!st || st.text !== text) { st = { text, phase: 'hold0', t: 0, pos: 0 }; mq.set(el, st); el.scrollLeft = 0; }
      if (max <= 2) { el.classList.remove('ytm-dock-scrolling'); if (el.scrollLeft) el.scrollLeft = 0; continue; }
      el.classList.add('ytm-dock-scrolling');

      switch (st.phase) {
        case 'hold0': st.t += dt; if (st.t >= HOLD) { st.phase = 'fwd'; st.t = 0; } break;
        case 'fwd':   st.pos += SPEED * dt / 1000; if (st.pos >= max) { st.pos = max; st.phase = 'hold1'; st.t = 0; } break;
        case 'hold1': st.t += dt; if (st.t >= HOLD) { st.phase = 'back'; } break;
        case 'back':  st.pos -= SPEED * 4 * dt / 1000; if (st.pos <= 0) { st.pos = 0; st.phase = 'hold0'; st.t = 0; } break;
      }
      el.scrollLeft = st.pos;
    }
  }
  requestAnimationFrame(tickMarquee);

  // ── 곡 바뀜 감지 → C# 에 알림 (숨김 상태에서 잠깐 보여주기용) ─────────────────
  let lastTitle = '';
  function watchTitle() {
    const t = document.querySelector('ytmusic-app-layout > ytmusic-player-bar .content-info-wrapper .title');
    if (!t) { setTimeout(watchTitle, 1000); return; }
    const check = () => {
      const now = (t.textContent || '').trim();
      if (now && now !== lastTitle) {
        const first = lastTitle === '';
        lastTitle = now;
        if (!first) window.chrome?.webview?.postMessage('track:' + now);
      }
    };
    new MutationObserver(check).observe(t, { childList: true, characterData: true, subtree: true });
    check();
  }
  watchTitle();

  // 전체 모드에서 Esc → 미니로
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && !window.ytmDock.mini) {
      window.chrome?.webview?.postMessage('toggle');
    }
  }, true);

  // 기본은 미니. C# 이 NavigationCompleted 에서 실제 상태로 다시 맞춘다.
  window.ytmDock.applyMini(true);
})();
