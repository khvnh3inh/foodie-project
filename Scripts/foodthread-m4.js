/**
 * ============================================================
 *  FoodThread – M4 Frontend Module
 *  Phụ trách: #page-home | #page-explore | #page-recipes | #page-saved
 *  Kết nối: API M2 (REST + JWT)
 * ============================================================
 */

/* ─────────────────────────────────────────────
   0. CẤU HÌNH & TIỆN ÍCH
───────────────────────────────────────────── */
var API_BASE = '/api';
var M4_TOKEN_KEY = 'ft_jwt';

/** Lấy JWT hiện tại từ localStorage */
function getToken() {
    return localStorage.getItem(M4_TOKEN_KEY) || '';
}

/**
 * Wrapper fetch() tự động gắn Authorization header
 * @param {string} url
 * @param {RequestInit} [options]
 */
async function apiFetch(url, options = {}) {
    const token = getToken();
    const headers = {
        'Content-Type': 'application/json',
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
        ...(options.headers || {}),
    };
    const res = await fetch(`${API_BASE}${url}`, { ...options, headers });
    if (!res.ok) {
        const err = await res.json().catch(() => ({ message: res.statusText }));
        throw new Error(err.message || `HTTP ${res.status}`);
    }
    return res.json();
}

/** Định dạng thời gian tương đối */
function timeAgo(isoString) {
    const diff = (Date.now() - new Date(isoString)) / 1000;
    if (diff < 60) return 'vừa xong';
    if (diff < 3600) return `${Math.floor(diff / 60)} phút trước`;
    if (diff < 86400) return `${Math.floor(diff / 3600)} giờ trước`;
    return `${Math.floor(diff / 86400)} ngày trước`;
}

/** Rút gọn số: 1400 → 1.4k */
function fmtCount(n = 0) {
    return n >= 1000 ? `${(n / 1000).toFixed(1)}k` : String(n);
}

/** Màu avatar ngẫu nhiên theo index */
function avClass(idx) {
    return `av${(idx % 6) + 1}`;
}

/** Initials từ tên */
function initials(name = '') {
    return name.split(' ').map(w => w[0]).join('').slice(0, 2).toUpperCase();
}

/** Badge HTML theo loại bài */
var BADGE_MAP = {
    recipe: '<span class="post-badge badge-recipe">Công thức</span>',
    review: '<span class="post-badge badge-review">Review quán</span>',
    video: '<span class="post-badge badge-video">Video</span>',
    photo: '<span class="post-badge badge-photo">Ảnh</span>',
    location: '<span class="post-badge badge-location">Địa điểm</span>',
};

/** Skeleton card dùng khi đang loading */
function skeletonPost() {
    return `
    <article class="post" style="pointer-events:none">
      <div class="post-header">
        <div style="width:40px;height:40px;border-radius:50%;background:var(--cream3);flex-shrink:0;"></div>
        <div style="flex:1;display:flex;flex-direction:column;gap:6px;">
          <div style="height:12px;width:140px;border-radius:6px;background:var(--cream3);"></div>
          <div style="height:10px;width:90px;border-radius:6px;background:var(--cream3);"></div>
        </div>
      </div>
      <div style="height:14px;border-radius:6px;background:var(--cream3);margin-bottom:8px;"></div>
      <div style="height:14px;width:70%;border-radius:6px;background:var(--cream3);margin-bottom:12px;"></div>
      <div style="height:180px;border-radius:14px;background:var(--cream3);margin-bottom:12px;"></div>
    </article>`;
}

/** Toast thông báo nhanh */
function showToast(msg, type = 'success') {
    const existing = document.getElementById('ft-toast');
    if (existing) existing.remove();
    const toast = document.createElement('div');
    toast.id = 'ft-toast';
    toast.style.cssText = `
    position:fixed;bottom:24px;left:50%;transform:translateX(-50%);
    background:${type === 'error' ? 'var(--red)' : 'var(--green)'};
    color:white;padding:10px 22px;border-radius:24px;font-size:13px;
    font-family:var(--sans);z-index:9999;box-shadow:0 4px 16px rgba(0,0,0,.15);
    animation:fadeUp .25s ease;
  `;
    toast.textContent = msg;
    document.body.appendChild(toast);
    setTimeout(() => toast.remove(), 2800);
}


/* ─────────────────────────────────────────────
   1. TEMPLATE STRINGS – render bài viết
───────────────────────────────────────────── */

/**
 * Render hình ảnh của bài viết (emoji placeholder khi chưa có ảnh thật)
 * API trả về: post.images = [{ url, emoji, colorClass }]
 */
function renderImages(images = []) {
    if (!images.length) return '';
    const wrapClass = images.length === 1 ? 'single' : images.length === 2 ? 'double' : 'triple';
    const imgs = images.map(img =>
        img.url
            ? `<div class="post-img" style="background-image:url('${img.url}');background-size:cover;background-position:center;"></div>`
            : `<div class="post-img ${img.colorClass || 'img-warm'}" style="font-size:${images.length === 1 ? '64px' : '40px'}">${img.emoji || '🍽️'}</div>`
    ).join('');
    return `<div class="post-images ${wrapClass}">${imgs}</div>`;
}

/**
 * Render location pill nếu bài có địa điểm
 */
function renderLocation(location) {
    if (!location) return '';
    return `
    <a class="location-pill" href="https://maps.google.com/?q=${encodeURIComponent(location.name + ' ' + (location.address || ''))}" target="_blank">
      <div class="location-icon">
        <svg viewBox="0 0 16 16" fill="#4361EE"><path d="M8 1a5.5 5.5 0 00-5.5 5.5c0 4 5.5 8.5 5.5 8.5S13.5 10.5 13.5 6.5A5.5 5.5 0 008 1zm0 7.5a2 2 0 110-4 2 2 0 010 4z"/></svg>
      </div>
      <div>
        <div class="location-name">${location.name}</div>
        <div class="location-addr">${location.address || ''}</div>
      </div>
      <span class="location-map-badge">Mở Google Maps ↗</span>
    </a>`;
}

/**
 * Template đầy đủ cho 1 bài viết trong feed
 * @param {Object} post – dữ liệu từ API
 * @param {number} idx  – thứ tự để chọn màu avatar
 */
function postTemplate(post, idx) {
    const liked = post.isLiked ? 'liked' : '';
    const saved = post.isSaved ? 'saved' : '';
    const likeIcon = post.isLiked
        ? `<svg viewBox="0 0 20 20" fill="currentColor"><path d="M3.172 5.172a4 4 0 015.656 0L10 6.343l1.172-1.171a4 4 0 115.656 5.656L10 17.657l-6.828-6.829a4 4 0 010-5.656z"/></svg>`
        : `<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M3.172 5.172a4 4 0 015.656 0L10 6.343l1.172-1.171a4 4 0 115.656 5.656L10 17.657l-6.828-6.829a4 4 0 010-5.656z"/></svg>`;
    const saveIcon = post.isSaved
        ? `<svg viewBox="0 0 20 20" fill="currentColor"><path d="M5 4a2 2 0 012-2h6a2 2 0 012 2v14l-5-3-5 3V4z"/></svg>`
        : `<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M5 4a2 2 0 012-2h6a2 2 0 012 2v14l-5-3-5 3V4z"/></svg>`;

    return `
    <article class="post" data-post-id="${post.id}" onclick="handlePostClick(event, '${post.id}')">
      <div class="post-header">
        <div class="post-avatar ${avClass(idx)}">${initials(post.author?.name)}</div>
        <div class="post-meta">
          <div class="post-top">
            <span class="post-name">${post.author?.name || 'Ẩn danh'}</span>
            <span class="post-handle">@${post.author?.username || 'user'}</span>
            <span class="post-time">· ${timeAgo(post.createdAt)}</span>
            ${BADGE_MAP[post.type] || ''}
          </div>
        </div>
      </div>
      <p class="post-text">${post.content || ''}</p>
      ${renderImages(post.images)}
      ${renderLocation(post.location)}
      <div class="post-actions" onclick="event.stopPropagation()">
        <button class="action ${liked}" id="like-btn-${post.id}"
          onclick="toggleLike('${post.id}')">
          ${likeIcon}
          <span id="like-count-${post.id}">${fmtCount(post.likesCount)}</span>
        </button>
        <button class="action" onclick="openModal('${post.id}')">
          <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M18 10c0 3.866-3.582 7-8 7a8.841 8.841 0 01-4.083-.98L2 17l1.338-3.123C2.493 12.767 2 11.434 2 10c0-3.866 3.582-7 8-7s8 3.134 8 7z"/></svg>
          ${fmtCount(post.commentsCount)}
        </button>
        <div class="action-spacer"></div>
        <button class="action ${saved}" id="save-btn-${post.id}"
          onclick="toggleSave('${post.id}')">
          ${saveIcon}
          ${post.isSaved ? 'Đã lưu' : 'Lưu'}
        </button>
      </div>
    </article>`;
}

/**
 * Template cho explore card (trending)
 */
function exploreCardTemplate(post, idx) {
    const img = (post.images || [])[0] || {};
    return `
    <div class="explore-card" onclick="openModal('${post.id}')">
      <div class="explore-card-img ${img.colorClass || 'img-warm'}" style="${img.url ? `background-image:url('${img.url}');background-size:cover;` : ''}">
        ${img.url ? '' : (img.emoji || '🍽️')}
      </div>
      <div class="explore-card-body">
        <div class="explore-card-title">${post.title || post.content?.slice(0, 50) || ''}</div>
        <div class="explore-card-meta">@${post.author?.username || 'user'} · ${fmtCount(post.likesCount)} ❤️</div>
      </div>
    </div>`;
}

/**
 * Template cho saved card
 */
function savedCardTemplate(post, idx) {
    const img = (post.images || [])[0] || {};
    const typeLabel = { recipe: 'Công thức', review: 'Quán ăn', video: 'Video', photo: 'Ảnh' }[post.type] || 'Bài viết';
    return `
    <div class="saved-card" data-saved-id="${post.id}">
      <div class="saved-card-img ${img.colorClass || 'img-warm'}"
           style="${img.url ? `background-image:url('${img.url}');background-size:cover;` : 'font-size:28px;display:flex;align-items:center;justify-content:center;'}">
        ${img.url ? '' : (img.emoji || '🍽️')}
      </div>
      <div class="saved-card-body" onclick="openModal('${post.id}')">
        <div class="saved-card-title">${post.title || post.content?.slice(0, 40) || ''}</div>
        <div class="saved-card-meta">@${post.author?.username || 'user'} · ${typeLabel}</div>
      </div>
      <div class="saved-remove" title="Bỏ lưu" onclick="removeSaved('${post.id}', this)">
        <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M12 4l-8 8M4 4l8 8"/></svg>
      </div>
    </div>`;
}

/**
 * Template cho comment trong modal
 */
function commentTemplate(c, idx) {
    return `
    <div class="comment-item">
      <div class="post-avatar ${avClass(idx)}" style="width:32px;height:32px;font-size:12px;flex-shrink:0;">
        ${initials(c.author?.name)}
      </div>
      <div>
        <div style="font-size:13px;font-weight:500;color:var(--ink);">${c.author?.name || 'Ẩn danh'}</div>
        <div class="comment-text">${c.content}</div>
        <div class="comment-time">${timeAgo(c.createdAt)} · ${c.likesCount || 0} ❤️</div>
      </div>
    </div>`;
}


/* ─────────────────────────────────────────────
   2. FETCH FUNCTIONS – lấy dữ liệu API
───────────────────────────────────────────── */

/**
 * Fetch & render bài viết feed (#page-home)
 * Endpoint: GET /api/posts/feed
 */
async function fetchFeedPosts() {
    const container = document.getElementById('feed-posts-container');
    if (!container) return;

    // Hiện skeleton loading
    container.innerHTML = Array(3).fill(skeletonPost()).join('');

    try {
        const data = await apiFetch('/posts/feed');
        const posts = Array.isArray(data) ? data : (data.posts || data.data || []);

        if (!posts.length) {
            container.innerHTML = `
        <div style="padding:48px 26px;text-align:center;color:var(--ink3);">
          <div style="font-size:36px;margin-bottom:12px;">🍽️</div>
          <div style="font-size:15px;">Chưa có bài viết nào. Hãy theo dõi thêm người dùng!</div>
        </div>`;
            return;
        }

        container.innerHTML = posts.map((p, i) => postTemplate(p, i)).join('');
    } catch (err) {
        console.error('[fetchFeedPosts]', err);
        container.innerHTML = `
      <div style="padding:32px 26px;text-align:center;color:var(--ink3);">
        <div style="font-size:13px;margin-bottom:10px;">Không tải được bài viết. Kiểm tra kết nối mạng.</div>
        <button onclick="fetchFeedPosts()" style="padding:8px 18px;border-radius:20px;background:var(--orange);color:white;border:none;font-family:var(--sans);font-size:13px;cursor:pointer;">Thử lại</button>
      </div>`;
    }
}

/**
 * Fetch & render trending posts cho trang Khám phá (#page-explore)
 * Endpoint: GET /api/posts/trending
 */
async function fetchTrending() {
    const grid = document.getElementById('explore-trending-grid');
    if (!grid) return;

    grid.innerHTML = `<div style="padding:20px;color:var(--ink3);font-size:13px;">Đang tải…</div>`;

    try {
        const data = await apiFetch('/posts/trending');
        const posts = Array.isArray(data) ? data : (data.posts || data.data || []);

        if (!posts.length) {
            grid.innerHTML = `<div style="padding:20px;color:var(--ink3);font-size:13px;">Chưa có nội dung trending.</div>`;
            return;
        }

        grid.innerHTML = posts.map((p, i) => exploreCardTemplate(p, i)).join('');
    } catch (err) {
        console.error('[fetchTrending]', err);
        grid.innerHTML = `<div style="padding:20px;color:var(--ink3);font-size:13px;">Lỗi tải dữ liệu.</div>`;
    }
}

/**
 * Fetch & render bài đã lưu (#page-saved)
 * Endpoint: GET /api/posts/saved
 */
async function fetchSavedPosts() {
    const grid = document.getElementById('saved-posts-grid');
    const tabsEl = document.getElementById('saved-tabs-header');
    if (!grid) return;

    grid.innerHTML = `<div style="padding:20px;color:var(--ink3);font-size:13px;">Đang tải…</div>`;

    try {
        const data = await apiFetch('/posts/saved');
        const posts = Array.isArray(data) ? data : (data.posts || data.data || []);

        // Cập nhật tabs count
        if (tabsEl) {
            const total = posts.length;
            const recipes = posts.filter(p => p.type === 'recipe').length;
            const reviews = posts.filter(p => p.type === 'review').length;
            const rest = total - recipes - reviews;
            tabsEl.innerHTML = `
        <div class="saved-tab active" data-filter="all">Tất cả (${total})</div>
        <div class="saved-tab" data-filter="recipe">Công thức (${recipes})</div>
        <div class="saved-tab" data-filter="review">Quán ăn (${reviews})</div>
        <div class="saved-tab" data-filter="other">Bài viết (${rest})</div>
      `;
            bindSavedTabs(posts);
        }

        if (!posts.length) {
            grid.innerHTML = `
        <div style="grid-column:1/-1;padding:48px 0;text-align:center;color:var(--ink3);">
          <div style="font-size:36px;margin-bottom:12px;">🔖</div>
          <div style="font-size:14px;">Bạn chưa lưu bài viết nào.</div>
        </div>`;
            return;
        }

        grid.innerHTML = posts.map((p, i) => savedCardTemplate(p, i)).join('');
        // Lưu cache để filter tab không cần gọi API thêm
        grid.dataset.savedCache = JSON.stringify(posts);
    } catch (err) {
        console.error('[fetchSavedPosts]', err);
        grid.innerHTML = `<div style="padding:20px;color:var(--ink3);font-size:13px;">Lỗi tải danh sách đã lưu.</div>`;
    }
}

/**
 * Tìm kiếm bài viết
 * Endpoint: GET /api/posts/search?q=...&type=...
 */
async function fetchSearch(query, type = '') {
    const grid = document.getElementById('explore-trending-grid');
    if (!grid || !query.trim()) return;

    grid.innerHTML = `<div style="padding:20px;color:var(--ink3);font-size:13px;">Đang tìm kiếm…</div>`;

    try {
        const params = new URLSearchParams({ q: query });
        if (type) params.set('type', type);
        const data = await apiFetch(`/posts/search?${params}`);
        const posts = Array.isArray(data) ? data : (data.posts || data.data || []);

        const section = document.querySelector('#page-explore .section-title');
        if (section) section.textContent = `Kết quả cho "${query}" (${posts.length})`;

        if (!posts.length) {
            grid.innerHTML = `
        <div style="grid-column:1/-1;padding:36px 0;text-align:center;color:var(--ink3);">
          <div style="font-size:32px;margin-bottom:10px;">🔍</div>
          <div>Không tìm thấy kết quả nào.</div>
        </div>`;
            return;
        }

        grid.innerHTML = posts.map((p, i) => exploreCardTemplate(p, i)).join('');
    } catch (err) {
        console.error('[fetchSearch]', err);
        grid.innerHTML = `<div style="padding:20px;color:var(--ink3);font-size:13px;">Lỗi tìm kiếm.</div>`;
    }
}


/* ─────────────────────────────────────────────
   3. TƯƠNG TÁC – Like / Save
───────────────────────────────────────────── */

/** Trạng thái optimistic cục bộ tránh double-click */
var _pendingLike = new Set();
var _pendingSave = new Set();

/**
 * Toggle like cho bài viết
 * POST /api/posts/:id/like   → like
 * DELETE /api/posts/:id/like → unlike
 */
async function toggleLike(postId) {
    if (_pendingLike.has(postId)) return;
    _pendingLike.add(postId);

    const btn = document.getElementById(`like-btn-${postId}`);
    const countEl = document.getElementById(`like-count-${postId}`);
    if (!btn || !countEl) { _pendingLike.delete(postId); return; }

    const isLiked = btn.classList.contains('liked');
    const oldCount = parseInt(countEl.textContent.replace('k', '')) || 0;

    // Optimistic UI
    btn.classList.toggle('liked');
    btn.innerHTML = isLiked
        ? `<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M3.172 5.172a4 4 0 015.656 0L10 6.343l1.172-1.171a4 4 0 115.656 5.656L10 17.657l-6.828-6.829a4 4 0 010-5.656z"/></svg><span id="like-count-${postId}">${fmtCount(Math.max(0, oldCount - 1))}</span>`
        : `<svg viewBox="0 0 20 20" fill="currentColor"><path d="M3.172 5.172a4 4 0 015.656 0L10 6.343l1.172-1.171a4 4 0 115.656 5.656L10 17.657l-6.828-6.829a4 4 0 010-5.656z"/></svg><span id="like-count-${postId}">${fmtCount(oldCount + 1)}</span>`;

    try {
        if (isLiked) {
            await apiFetch(`/posts/${postId}/like`, { method: 'DELETE' });
        } else {
            await apiFetch(`/posts/${postId}/like`, { method: 'POST' });
        }
    } catch (err) {
        // Rollback
        btn.classList.toggle('liked');
        const rollbackCountEl = document.getElementById(`like-count-${postId}`);
        if (rollbackCountEl) rollbackCountEl.textContent = fmtCount(oldCount);
        showToast('Không thể thực hiện. Vui lòng thử lại.', 'error');
        console.error('[toggleLike]', err);
    } finally {
        _pendingLike.delete(postId);
    }
}

/**
 * Toggle save cho bài viết
 * POST /api/posts/:id/save   → save
 * DELETE /api/posts/:id/save → unsave
 */
async function toggleSave(postId) {
    if (_pendingSave.has(postId)) return;
    _pendingSave.add(postId);

    const btn = document.getElementById(`save-btn-${postId}`);
    if (!btn) { _pendingSave.delete(postId); return; }

    const isSaved = btn.classList.contains('saved');

    // Optimistic UI
    btn.classList.toggle('saved');
    const savedIcon = isSaved
        ? `<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M5 4a2 2 0 012-2h6a2 2 0 012 2v14l-5-3-5 3V4z"/></svg> Lưu`
        : `<svg viewBox="0 0 20 20" fill="currentColor"><path d="M5 4a2 2 0 012-2h6a2 2 0 012 2v14l-5-3-5 3V4z"/></svg> Đã lưu`;
    btn.innerHTML = savedIcon;

    try {
        if (isSaved) {
            await apiFetch(`/posts/${postId}/save`, { method: 'DELETE' });
            showToast('Đã bỏ lưu bài viết');
        } else {
            await apiFetch(`/posts/${postId}/save`, { method: 'POST' });
            showToast('Đã lưu bài viết ✓');
        }
    } catch (err) {
        // Rollback
        btn.classList.toggle('saved');
        btn.innerHTML = isSaved
            ? `<svg viewBox="0 0 20 20" fill="currentColor"><path d="M5 4a2 2 0 012-2h6a2 2 0 012 2v14l-5-3-5 3V4z"/></svg> Đã lưu`
            : `<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M5 4a2 2 0 012-2h6a2 2 0 012 2v14l-5-3-5 3V4z"/></svg> Lưu`;
        showToast('Không thể thực hiện. Vui lòng thử lại.', 'error');
        console.error('[toggleSave]', err);
    } finally {
        _pendingSave.delete(postId);
    }
}

/**
 * Bỏ lưu từ trang Đã lưu (xóa card ngay lập tức)
 */
async function removeSaved(postId, el) {
    const card = el.closest('.saved-card');
    if (!card) return;
    card.style.opacity = '0.4';
    card.style.pointerEvents = 'none';
    try {
        await apiFetch(`/posts/${postId}/save`, { method: 'DELETE' });
        card.style.transition = 'all .25s';
        card.style.transform = 'scale(0.9)';
        setTimeout(() => card.remove(), 250);
        showToast('Đã bỏ lưu');
    } catch (err) {
        card.style.opacity = '1';
        card.style.pointerEvents = '';
        showToast('Không thể bỏ lưu.', 'error');
    }
}


/* ─────────────────────────────────────────────
   4. MODAL COMMENT – hiển thị bài viết + comments
───────────────────────────────────────────── */

/** Cache đơn giản để không fetch lại post đã load */
var _postCache = {};

/**
 * Mở modal và load chi tiết bài viết + comments
 * GET /api/posts/:id
 * GET /api/posts/:id/comments
 */
async function openModal(postId) {
    const modal = document.getElementById('modal');
    const body = document.getElementById('modal-body');
    if (!modal || !body) return;

    modal.classList.add('open');
    document.body.style.overflow = 'hidden';
    body.innerHTML = `<div style="padding:40px;text-align:center;color:var(--ink3);">
    <div style="font-size:24px;margin-bottom:10px;">⏳</div>
    <div style="font-size:13px;">Đang tải bài viết…</div>
  </div>`;

    try {
        // Tải song song post detail và comments
        const [post, commentsData] = await Promise.all([
            _postCache[postId] || apiFetch(`/posts/${postId}`).then(d => { _postCache[postId] = d; return d; }),
            apiFetch(`/posts/${postId}/comments`),
        ]);

        const comments = Array.isArray(commentsData) ? commentsData : (commentsData.comments || commentsData.data || []);

        body.innerHTML = renderModalContent(post, comments);

        // Gắn sự kiện gửi comment
        const sendBtn = document.getElementById(`modal-send-${postId}`);
        const input = document.getElementById(`modal-comment-input-${postId}`);
        const commentList = document.getElementById(`modal-comment-list-${postId}`);

        if (sendBtn && input && commentList) {
            sendBtn.addEventListener('click', () => submitComment(postId, input, commentList));
            input.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    submitComment(postId, input, commentList);
                }
            });
        }

    } catch (err) {
        console.error('[openModal]', err);
        body.innerHTML = `<div style="padding:32px;text-align:center;color:var(--ink3);">
      <div>Không tải được bài viết.</div>
      <button onclick="openModal('${postId}')" style="margin-top:12px;padding:8px 18px;border-radius:20px;background:var(--orange);color:white;border:none;font-family:var(--sans);font-size:13px;cursor:pointer;">Thử lại</button>
    </div>`;
    }
}

/**
 * Render nội dung đầy đủ của bài viết trong modal (post + comments)
 */
function renderModalContent(post, comments) {
    const liked = post.isLiked ? 'liked' : '';
    const saved = post.isSaved ? 'saved' : '';

    return `
    <div style="padding:8px 0 0;">
      <div class="post-header">
        <div class="post-avatar ${avClass(0)}">${initials(post.author?.name)}</div>
        <div class="post-meta">
          <div class="post-top">
            <span class="post-name">${post.author?.name || 'Ẩn danh'}</span>
            <span class="post-handle">@${post.author?.username || 'user'}</span>
            <span class="post-time">· ${timeAgo(post.createdAt)}</span>
            ${BADGE_MAP[post.type] || ''}
          </div>
        </div>
      </div>
      <p class="post-text">${post.content || ''}</p>
      ${renderImages(post.images)}
      ${renderLocation(post.location)}
      ${post.type === 'recipe' && post.recipe ? renderRecipeSteps(post.recipe) : ''}
      ${post.type === 'review' && post.review ? renderStarRating(post.review) : ''}
      <div class="post-actions" style="border-top:1px solid var(--border);padding-top:12px;margin-top:8px;">
        <button class="action ${liked}" id="like-btn-${post.id}" onclick="toggleLike('${post.id}')">
          <svg viewBox="0 0 20 20" fill="${post.isLiked ? 'currentColor' : 'none'}" stroke="currentColor" stroke-width="${post.isLiked ? 0 : 1.5}"><path d="M3.172 5.172a4 4 0 015.656 0L10 6.343l1.172-1.171a4 4 0 115.656 5.656L10 17.657l-6.828-6.829a4 4 0 010-5.656z"/></svg>
          <span id="like-count-${post.id}">${fmtCount(post.likesCount)}</span>
        </button>
        <div class="action-spacer"></div>
        <button class="action ${saved}" id="save-btn-${post.id}" onclick="toggleSave('${post.id}')">
          <svg viewBox="0 0 20 20" fill="${post.isSaved ? 'currentColor' : 'none'}" stroke="currentColor" stroke-width="${post.isSaved ? 0 : 1.5}"><path d="M5 4a2 2 0 012-2h6a2 2 0 012 2v14l-5-3-5 3V4z"/></svg>
          ${post.isSaved ? 'Đã lưu' : 'Lưu'}
        </button>
      </div>
      <div style="border-top:1px solid var(--border);padding-top:14px;margin-top:4px;">
        <div style="font-family:var(--serif);font-size:14px;font-weight:500;color:var(--ink);margin-bottom:10px;">
          Bình luận (${post.commentsCount || comments.length})
        </div>
        <div id="modal-comment-list-${post.id}">
          ${comments.length
            ? comments.map((c, i) => commentTemplate(c, i)).join('')
            : `<div style="padding:16px 0;color:var(--ink4);font-size:13px;">Chưa có bình luận nào. Hãy là người đầu tiên!</div>`
        }
        </div>
      </div>
    </div>
    <div class="comment-box">
      <div class="post-avatar ${avClass(99)}" style="width:34px;height:34px;font-size:12px;flex-shrink:0;">NT</div>
      <textarea class="comment-input" id="modal-comment-input-${post.id}"
        rows="1" placeholder="Viết bình luận…"></textarea>
      <button class="comment-send" id="modal-send-${post.id}">Gửi</button>
    </div>`;
}

/** Render recipe steps trong modal */
function renderRecipeSteps(recipe) {
    if (!recipe) return '';
    const ingredients = (recipe.ingredients || []).join(' · ');
    const steps = (recipe.steps || []).map((s, i) =>
        `<div class="step"><div class="step-n">${i + 1}</div><div class="step-t">${s}</div></div>`
    ).join('');
    return `
    ${ingredients ? `<div class="recipe-steps" style="margin-top:12px;">
      <div class="recipe-steps-title">Nguyên liệu</div>
      <div style="font-size:13px;color:var(--ink2);line-height:1.8;">${ingredients}</div>
    </div>` : ''}
    ${steps ? `<div class="recipe-steps">
      <div class="recipe-steps-title">Các bước nấu</div>
      ${steps}
    </div>` : ''}`;
}

/** Render sao đánh giá trong modal */
function renderStarRating(review) {
    const star = (n) => '★'.repeat(n) + '☆'.repeat(5 - n);
    const cats = review.categories || {};
    return `
    <div style="padding:12px 0;">
      <div class="stars-row" style="margin-bottom:10px;">
        <span class="star" style="font-size:18px;">${star(Math.round(review.rating || 5))}</span>
        <span style="font-size:14px;font-weight:500;color:var(--ink);margin-left:8px;">${(review.rating || 5).toFixed(1)}</span>
      </div>
      ${Object.entries(cats).length ? `
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-bottom:12px;">
        ${Object.entries(cats).map(([k, v]) => `
          <div style="background:var(--cream2);border-radius:10px;padding:10px 12px;">
            <div style="font-size:11px;color:var(--ink3);">${k}</div>
            <div style="font-size:14px;font-weight:500;color:var(--orange);">${star(Math.round(v))}</div>
          </div>`).join('')}
      </div>` : ''}
    </div>`;
}

/**
 * Gửi comment mới
 * POST /api/posts/:id/comments
 */
async function submitComment(postId, inputEl, listEl) {
    const content = inputEl.value.trim();
    if (!content) return;

    const sendBtn = document.getElementById(`modal-send-${postId}`);
    if (sendBtn) sendBtn.disabled = true;

    try {
        const newComment = await apiFetch(`/posts/${postId}/comments`, {
            method: 'POST',
            body: JSON.stringify({ content }),
        });

        // Render comment mới lên đầu danh sách
        const commentHTML = commentTemplate(newComment, 0);
        listEl.insertAdjacentHTML('afterbegin', commentHTML);
        inputEl.value = '';

        // Cập nhật số lượng comment trong feed (nếu có)
        const postArticle = document.querySelector(`[data-post-id="${postId}"]`);
        if (postArticle) {
            // Không fetch lại, chỉ tăng đếm local
            const commentBtns = postArticle.querySelectorAll('.action');
            commentBtns.forEach(btn => {
                if (btn.querySelector('path[d*="M18 10"]')) {
                    const text = btn.textContent.trim();
                    const num = parseInt(text) || 0;
                    btn.innerHTML = btn.innerHTML.replace(/>\d+(\.\d+k)?</, `>${fmtCount(num + 1)}<`);
                }
            });
        }
    } catch (err) {
        showToast('Không gửi được bình luận.', 'error');
        console.error('[submitComment]', err);
    } finally {
        if (sendBtn) sendBtn.disabled = false;
    }
}

/** Click lên article: mở modal (nhưng không kích hoạt khi click action buttons) */
function handlePostClick(event, postId) {
    if (event.target.closest('.action, .post-actions, .location-pill')) return;
    openModal(postId);
}


/* ─────────────────────────────────────────────
   5. SAVED TABS – filter theo loại
───────────────────────────────────────────── */

function bindSavedTabs(posts) {
    const tabsEl = document.getElementById('saved-tabs-header');
    const grid = document.getElementById('saved-posts-grid');
    if (!tabsEl || !grid) return;

    tabsEl.querySelectorAll('.saved-tab').forEach(tab => {
        tab.addEventListener('click', function () {
            tabsEl.querySelectorAll('.saved-tab').forEach(t => t.classList.remove('active'));
            this.classList.add('active');

            const filter = this.dataset.filter;
            const filtered = filter === 'all'
                ? posts
                : filter === 'other'
                    ? posts.filter(p => p.type !== 'recipe' && p.type !== 'review')
                    : posts.filter(p => p.type === filter);

            grid.innerHTML = filtered.length
                ? filtered.map((p, i) => savedCardTemplate(p, i)).join('')
                : `<div style="grid-column:1/-1;padding:36px 0;text-align:center;color:var(--ink3);">Không có mục nào trong danh mục này.</div>`;
        });
    });
}


/* ─────────────────────────────────────────────
   6. SEARCH (Explore) – debounce input
───────────────────────────────────────────── */

var _searchTimer;

function initExploreSearch() {
    const input = document.querySelector('#page-explore .big-search input');
    if (!input) return;

    input.addEventListener('input', function () {
        clearTimeout(_searchTimer);
        const q = this.value.trim();
        if (!q) {
            // Nếu xóa hết, load lại trending
            const section = document.querySelector('#page-explore .section-title');
            if (section) section.textContent = 'Đang thịnh hành hôm nay';
            fetchTrending();
            return;
        }
        _searchTimer = setTimeout(() => fetchSearch(q), 420);
    });
}


/* ─────────────────────────────────────────────
   7. FEED TAB (For You / Đang theo dõi)
───────────────────────────────────────────── */

function initFeedTabs() {
    document.querySelectorAll('.feed-tab').forEach(tab => {
        tab.addEventListener('click', function () {
            document.querySelectorAll('.feed-tab').forEach(t => t.classList.remove('active'));
            this.classList.add('active');

            // Đổi endpoint theo tab
            const isFollowing = this.textContent.includes('Đang theo dõi');
            loadFeedByType(isFollowing ? 'following' : 'foryou');
        });
    });
}

async function loadFeedByType(type = 'foryou') {
    const container = document.getElementById('feed-posts-container');
    if (!container) return;
    container.innerHTML = Array(3).fill(skeletonPost()).join('');
    try {
        const data = await apiFetch(`/posts/feed?type=${type}`);
        const posts = Array.isArray(data) ? data : (data.posts || data.data || []);
        container.innerHTML = posts.length
            ? posts.map((p, i) => postTemplate(p, i)).join('')
            : `<div style="padding:40px 26px;text-align:center;color:var(--ink3);">Không có bài viết nào.</div>`;
    } catch {
        container.innerHTML = `<div style="padding:32px 26px;text-align:center;color:var(--ink3);">Lỗi tải feed.</div>`;
    }
}


/* ─────────────────────────────────────────────
   8. ĐIỀU HƯỚNG – hook vào showPage()
   Bắt sự kiện điều hướng để load đúng dữ liệu
───────────────────────────────────────────── */

/**
 * Ghi đè showPage() để tự động gọi fetch khi chuyển trang
 * Giữ nguyên logic hiện tại, chỉ thêm fetch hooks
 */
(function patchShowPage() {
    const _original = window.showPage;
    window.showPage = function (id) {
        if (typeof _original === 'function') _original(id);

        switch (id) {
            case 'home':
                fetchFeedPosts();
                break;
            case 'explore':
                fetchTrending();
                break;
            case 'saved':
                fetchSavedPosts();
                break;
        }
    };
})();


/* ─────────────────────────────────────────────
   9. KHỞI TẠO
───────────────────────────────────────────── */
document.addEventListener('DOMContentLoaded', () => {
    // Thêm container id nếu chưa có (fallback an toàn)
    const feedPage = document.getElementById('page-home');
    if (feedPage && !document.getElementById('feed-posts-container')) {
        // Tìm vùng chứa bài viết (sau compose-box)
        const composeBox = feedPage.querySelector('.compose-box');
        if (composeBox) {
            const wrapper = document.createElement('div');
            wrapper.id = 'feed-posts-container';
            composeBox.insertAdjacentElement('afterend', wrapper);
            // Chuyển các post cũ (static) vào container hoặc xoá đi
            const staticPosts = feedPage.querySelectorAll('article.post');
            staticPosts.forEach(p => wrapper.appendChild(p));
        }
    }

    // Gắn id cho explore trending grid nếu chưa có
    const exploreTrending = document.querySelector('#page-explore .explore-grid');
    if (exploreTrending && !exploreTrending.id) {
        exploreTrending.id = 'explore-trending-grid';
    }

    // Gắn id cho saved grid nếu chưa có
    const savedGrid = document.querySelector('#page-saved .saved-grid');
    if (savedGrid && !savedGrid.id) {
        savedGrid.id = 'saved-posts-grid';
    }

    // Gắn id cho saved tabs nếu chưa có
    const savedTabs = document.querySelector('#page-saved .saved-tabs');
    if (savedTabs && !savedTabs.id) {
        savedTabs.id = 'saved-tabs-header';
    }

    // Khởi tạo các module
    initFeedTabs();
    initExploreSearch();

    // Load trang chủ ngay khi vào
    const activePage = document.querySelector('.page.active');
    const pageId = activePage?.id?.replace('page-', '');
    if (pageId === 'home' || !pageId) {
        fetchFeedPosts();
    }
});