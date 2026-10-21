// 標準の JavaScript API のみを使用し、DOM 操作ライブラリには依存しない。
(() => {
    'use strict';
    const root = document.documentElement;
    const toggle = document.getElementById('theme-toggle');
    const update = () => {
        if (!toggle) return;
        const isDark = root.getAttribute('data-bs-theme') === 'dark';
        toggle.textContent = isDark ? '☀️ ライトモード' : '🌙 ダークモード';
        toggle.setAttribute('aria-pressed', String(isDark));
    };
    if (toggle) {
        update();
        toggle.addEventListener('click', () => {
            const next = root.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
            root.setAttribute('data-bs-theme', next);
            try { localStorage.setItem('osaka-theme', next); } catch { /* 権限なし */ }
            update();
        });
    }
    const form = document.getElementById('address-detail-form');
    const town = document.getElementById('town');
    if (form && town) {
        town.addEventListener('change', () => {
            const chome = document.getElementById('chome');
            if (chome) chome.value = '';
            // 町名変更時は前の町の番・号を引き継がない。
            const ban = document.getElementById('ban');
            const go = document.getElementById('go');
            if (ban) ban.value = '';
            if (go) go.value = '';
            // "mode=show" を送信せず、候補の一覧だけを更新する。
            form.submit();
        });
    }
})();
