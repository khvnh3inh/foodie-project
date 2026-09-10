/**
 * ============================================================
 *  FoodThread – Auth & Compose Module
 *  Xử lý: Đăng nhập, Đăng ký, Đăng xuất, Đăng bài, Session
 * ============================================================
 */

const AUTH_API = '/api/auth';
const TOKEN_KEY = 'ft_jwt';
const USER_KEY = 'ft_user';

/* ─── SESSION ─── */

function saveSession(data) {
    localStorage.setItem(TOKEN_KEY, data.token);
    localStorage.setItem(USER_KEY, JSON.stringify({
        userId: data.userId,
        username: data.username,
        fullName: data.fullName
        isAdmin: data.isAdmin
    }));
}

function getSession() {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try { return JSON.parse(raw); } catch { return null; }
}

function clearSession() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
}

function isLoggedIn() {
    return !!localStorage.getItem(TOKEN_KEY);
}

/* ─── AUTH API ─── */

async function apiLogin(username, password) {
    const res = await fetch(`${AUTH_API}/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password })
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || data.message || 'Đăng nhập thất bại');
    return data;
}

async function apiRegister(username, password, email, fullName) {
    const res = await fetch(`${AUTH_API}/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password, email, fullName })
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || data.message || 'Đăng ký thất bại');
    return data;
}

/* ─── UI HELPERS ─── */

function getInitials(name) {
    return name.split(' ').map(w => w[0]).join('').slice(0, 2).toUpperCase();
}

function showAuthError(formId, msg) {
    const el = document.getElementById(formId + '-error');
    if (el) {
        el.textContent = msg;
        el.style.display = 'block';
    }
}

function hideAuthError(formId) {
    const el = document.getElementById(formId + '-error');
    if (el) el.style.display = 'none';
}

function setLoading(btnId, loading) {
    const btn = document.getElementById(btnId);
    if (!btn) return;
    if (loading) {
        btn.dataset.originalText = btn.textContent;
        btn.textContent = 'Đang xử lý...';
        btn.disabled = true;
        btn.style.opacity = '0.7';
    } else {
        btn.textContent = btn.dataset.originalText || btn.textContent;
        btn.disabled = false;
        btn.style.opacity = '1';
    }
}

/* ─── AUTH SCREEN CONTROL ─── */

function showAuthScreen() {
    document.getElementById('auth-overlay').style.display = 'flex';
    document.getElementById('app-main').style.display = 'none';
    showLoginForm();
}

function hideAuthScreen() {
    document.getElementById('auth-overlay').style.display = 'none';
    document.getElementById('app-main').style.display = 'grid';
}

function showLoginForm() {
    document.getElementById('login-form').style.display = 'block';
    document.getElementById('register-form').style.display = 'none';
    hideAuthError('login');
    hideAuthError('register');
}

function showRegisterForm() {
    document.getElementById('login-form').style.display = 'none';
    document.getElementById('register-form').style.display = 'block';
    hideAuthError('login');
    hideAuthError('register');
}

/* ─── LOGIN ─── */

async function handleLogin(e) {
    if (e) e.preventDefault();
    hideAuthError('login');

    const username = document.getElementById('login-username').value.trim();
    const password = document.getElementById('login-password').value;

    if (!username || !password) {
        showAuthError('login', 'Vui lòng nhập đầy đủ thông tin.');
        return;
    }

    setLoading('login-btn', true);

    try {
        const data = await apiLogin(username, password);
        saveSession(data);
        hideAuthScreen();
        updateUIForUser();
        // Trigger feed load
        if (typeof fetchFeedPosts === 'function') fetchFeedPosts();
    } catch (err) {
        showAuthError('login', err.message);
    } finally {
        setLoading('login-btn', false);
    }
}

/* ─── REGISTER ─── */

async function handleRegister(e) {
    if (e) e.preventDefault();
    hideAuthError('register');

    const fullName = document.getElementById('reg-fullname').value.trim();
    const email = document.getElementById('reg-email').value.trim();
    const username = document.getElementById('reg-username').value.trim();
    const password = document.getElementById('reg-password').value;
    const confirm = document.getElementById('reg-confirm').value;

    if (!fullName || !email || !username || !password) {
        showAuthError('register', 'Vui lòng điền đầy đủ thông tin.');
        return;
    }

    if (username.length < 3) {
        showAuthError('register', 'Tên đăng nhập phải từ 3 ký tự.');
        return;
    }

    if (password.length < 6) {
        showAuthError('register', 'Mật khẩu phải từ 6 ký tự.');
        return;
    }

    if (password !== confirm) {
        showAuthError('register', 'Mật khẩu xác nhận không khớp.');
        return;
    }

    setLoading('register-btn', true);

    try {
        const data = await apiRegister(username, password, email, fullName);
        saveSession(data);
        hideAuthScreen();
        updateUIForUser();
        if (typeof fetchFeedPosts === 'function') fetchFeedPosts();
        if (typeof showToast === 'function') showToast('Đăng ký thành công! Chào mừng bạn 🎉');
    } catch (err) {
        showAuthError('register', err.message);
    } finally {
        setLoading('register-btn', false);
    }
}

/* ─── LOGOUT ─── */

function handleLogout() {
    if (!confirm('Bạn muốn đăng xuất?')) return;
    clearSession();
    showAuthScreen();
}

/* ─── UPDATE UI ─── */

function updateUIForUser() {
    const user = getSession();
    if (!user) return;

    const ini = getInitials(user.fullName);

    // Sidebar user chip
    const chipName = document.querySelector('.user-name');
    const chipHandle = document.querySelector('.user-handle');
    const chipAvatar = document.querySelector('.user-avatar');
    if (chipName) chipName.textContent = user.fullName;
    if (chipHandle) chipHandle.textContent = '@' + user.username;
    if (chipAvatar) chipAvatar.textContent = ini;

    // Compose avatar
    document.querySelectorAll('.compose-avatar').forEach(el => {
        el.textContent = ini;
    });

    // Profile page
    const profileName = document.querySelector('.profile-name');
    const profileHandle = document.querySelector('.profile-handle');
    const profileBigAvatar = document.querySelector('.profile-big-avatar');
    if (profileName) profileName.textContent = user.fullName;
    if (profileHandle) profileHandle.textContent = '@' + user.username;
    if (profileBigAvatar) profileBigAvatar.textContent = ini;
}

/* ─── COMPOSE / ĐĂNG BÀI ─── */

async function handleSubmitPost() {
    const user = getSession();
    if (!user) {
        if (typeof showAuthScreen === 'function') showAuthScreen();
        return;
    }

    // 1. Xác định loại bài viết
    const activeTypeBtn = document.querySelector('.write-type-btn.active');
    const postType = activeTypeBtn?.dataset.type || 'recipe';

    // 2. Lấy Form container tương ứng
    const formId = postType === 'review' ? 'write-form-review' : 'write-form-recipe';
    const formContainer = document.getElementById(formId);

    if (!formContainer) return;

    // 3. Thu thập dữ liệu (Sử dụng class cụ thể thay vì số thứ tự)
    const getValue = (selector) => formContainer.querySelector(selector)?.value?.trim() || '';

    const title = getValue('.input-title');
    const content = getValue('.input-content');
    let locationName = null;
    let locationAddress = null;
    let recipeData = null;

    // Xử lý riêng cho Review
    if (postType === 'review') {
        locationAddress = getValue('.input-address');
        locationName = title; // Review thường lấy tên quán làm tiêu đề
    }

    // Xử lý riêng cho Recipe
    if (postType === 'recipe') {
        const ingredientsRaw = getValue('.input-ingredients');
        const stepsRaw = getValue('.input-steps');

        const ingredients = ingredientsRaw.split('\n').map(s => s.trim()).filter(Boolean);
        const steps = stepsRaw.split('\n').map(s => s.trim()).filter(Boolean);

        if (ingredients.length > 0 || steps.length > 0) {
            recipeData = JSON.stringify({ ingredients, steps });
        }
    }

    // 4. Kiểm tra hợp lệ (Validation)
    if (!title && !content) {
        if (typeof showToast === 'function') showToast('Vui lòng nhập tiêu đề hoặc nội dung.', 'error');
        return;
    }

    // 5. Trạng thái Loading
    const submitBtn = document.querySelector('#page-write .btn-submit');
    const originalBtnText = submitBtn?.innerHTML;
    if (submitBtn) {
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang đăng...';
        submitBtn.disabled = true;
    }

    try {
        const token = localStorage.getItem(TOKEN_KEY);

        // Payload chuẩn gửi lên API
        const payload = {
            title,
            content, // Giữ nguyên content sạch, không nên gộp title vào đây ở bước này
            postType,
            locationName,
            locationAddress,
            recipeData,
            // Thêm mảng ảnh nếu bạn có input file (để trống nếu không có ảnh)
            images: typeof getSelectedImages === 'function' ? getSelectedImages() : []
        };

        const res = await fetch('/api/posts', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify(payload)
        });

        if (!res.ok) {
            const errData = await res.json().catch(() => ({}));
            throw new Error(errData.message || 'Lỗi server khi đăng bài');
        }

        // 6. Xử lý thành công
        if (typeof showToast === 'function') showToast('Đăng bài thành công! 🎉', 'success');

        resetWriteForm(); // Hàm reset riêng cho sạch sẽ

        if (typeof showPage === 'function') showPage('home');
        if (typeof fetchFeedPosts === 'function') setTimeout(fetchFeedPosts, 500);

    } catch (err) {
        console.error("Submit Post Error:", err);
        if (typeof showToast === 'function') showToast(err.message, 'error');
    } finally {
        if (submitBtn) {
            submitBtn.innerHTML = originalBtnText;
            submitBtn.disabled = false;
        }
    }
}

// Hàm bổ trợ để reset form
function resetWriteForm() {
    const textFields = document.querySelectorAll('#page-write input, #page-write textarea');
    textFields.forEach(el => el.value = '');

    // Nếu bạn có preview ảnh, hãy xóa nó ở đây
    const previewContainer = document.getElementById('image-preview-container');
    if (previewContainer) previewContainer.innerHTML = '';
}   

/* ─── QUICK COMPOSE (compose box ở trang chủ) ─── */

function handleQuickCompose() {
    const user = getSession();
    if (!user) {
        showAuthScreen();
        return;
    }
    if (typeof showPage === 'function') showPage('write');
}

/* ─── INIT ─── */

document.addEventListener('DOMContentLoaded', () => {
    // Kiểm tra session
    if (!isLoggedIn()) {
        showAuthScreen();
    } else {
        hideAuthScreen();
        updateUIForUser();
    }

    // Gắn sự kiện login form
    const loginForm = document.getElementById('login-form-el');
    if (loginForm) loginForm.addEventListener('submit', handleLogin);

    // Gắn sự kiện register form
    const regForm = document.getElementById('register-form-el');
    if (regForm) regForm.addEventListener('submit', handleRegister);

    // Gắn sự kiện nút Đăng bài
    document.querySelectorAll('#page-write .btn-submit').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            handleSubmitPost();
        });
    });

    // Gắn sự kiện compose box → chuyển sang trang write
    const composeInput = document.querySelector('.compose-box .compose-input input');
    if (composeInput) {
        composeInput.addEventListener('focus', () => {
            handleQuickCompose();
        });
    }
    document.querySelectorAll('.compose-box .compose-btn-icon, .compose-box .compose-post').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            handleQuickCompose();
        });
    });

    // Gắn data-type cho write type buttons
    const typeLabels = { 'Công thức': 'recipe', 'Ảnh/Video': 'photo', 'Review quán': 'review', 'Địa điểm': 'location' };
    document.querySelectorAll('.write-type-btn').forEach(btn => {
        const label = btn.querySelector('.write-type-label')?.textContent?.trim();
        if (label && typeLabels[label]) {
            btn.dataset.type = typeLabels[label];
        }
    });

    // Logout button (thêm vào user-chip)
    const userChip = document.querySelector('.user-chip');
    if (userChip) {
        userChip.removeAttribute('onclick');
        userChip.addEventListener('click', (e) => {
            e.stopPropagation();
            // Toggle dropdown
            let dropdown = document.getElementById('user-dropdown');
            if (dropdown) {
                dropdown.remove();
                return;
            }
            dropdown = document.createElement('div');
            dropdown.id = 'user-dropdown';
            dropdown.style.cssText = `
        position:absolute;bottom:70px;left:16px;right:16px;
        background:white;border:1px solid var(--border);border-radius:12px;
        box-shadow:0 4px 20px rgba(0,0,0,.12);z-index:50;overflow:hidden;
      `;
            const userSession = getSession();
            const isAdmin = userSession?.isAdmin === true;

            dropdown.innerHTML = `
        <div style="padding:10px 14px;cursor:pointer;font-size:13px;color:var(--ink2);transition:background .1s;"
             onmouseover="this.style.background='var(--cream2)'"
             onmouseout="this.style.background=''"
             onclick="showPage('profile');this.parentElement.remove();">
          👤 Hồ sơ của tôi
        </div>
        
        ${isAdmin ? `
        <div style="padding:10px 14px;cursor:pointer;font-size:13px;color:var(--orange);font-weight:bold;transition:background .1s;"
             onmouseover="this.style.background='var(--orange-pale)'"
             onmouseout="this.style.background=''"
             onclick="showPage('admin');this.parentElement.remove();">
          🛡️ Trang Quản Trị
        </div>
        ` : ''}

        <div style="height:1px;background:var(--border);"></div>
        <div style="padding:10px 14px;cursor:pointer;font-size:13px;color:var(--red);transition:background .1s;"
             onmouseover="this.style.background='var(--red-pale)'"
             onmouseout="this.style.background=''"
             onclick="handleLogout();">
          🚪 Đăng xuất
        </div>
      `;
            userChip.parentElement.style.position = 'relative';
            userChip.parentElement.appendChild(dropdown);

            // Close khi click ra ngoài
            setTimeout(() => {
                document.addEventListener('click', function handler(ev) {
                    if (!dropdown.contains(ev.target) && ev.target !== userChip) {
                        dropdown.remove();
                        document.removeEventListener('click', handler);
                    }
                });
            }, 10);
        });
    }
});
