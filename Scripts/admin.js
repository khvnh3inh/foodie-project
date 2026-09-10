const ADMIN_API = '/api/admin';

function getAdminHeaders() {
    return {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${localStorage.getItem(TOKEN_KEY)}`
    };
}

// Hàm ghi đè lên showPage có sẵn trong dự án của bạn để xử lý ẩn/hiện trang Admin
const originalShowPage = window.showPage;
window.showPage = function (pageName) {
    if (typeof originalShowPage === 'function') originalShowPage(pageName);

    const adminPage = document.getElementById('admin-page');
    if (adminPage) {
        if (pageName === 'admin') {
            adminPage.style.display = 'block';
            switchAdminTab('users'); // Mặc định hiển thị tab quản lý user

            // Ẩn các view feed chính nếu có trong giao diện của bạn
            const mainFeed = document.querySelector('.main-feed') || document.getElementById('main-feed');
            if (mainFeed) mainFeed.style.display = 'none';
        } else {
            adminPage.style.display = 'none';
        }
    }
};

function switchAdminTab(tabName) {
    if (tabName === 'users') {
        adminLoadAllUsers();
    } else if (tabName === 'restaurants') {
        adminLoadRestaurants();
    } else if (tabName === 'posts') {
        adminLoadAllPosts();
    } else if (tabName === 'stats') {
        adminLoadStats();
    }
}

async function adminLoadAllUsers() {
    try {
        const res = await fetch(`${ADMIN_API}/users`, { headers: getAdminHeaders() });
        const result = await res.json();
        if (!res.ok) throw new Error(result.message || 'Lỗi lấy danh sách user');

        let html = `
            <table style="width:100%; border-collapse: collapse; background: white; border-radius: 8px; overflow: hidden; border: 1px solid var(--cream3);">
                <tr style="background: var(--cream2); text-align: left;">
                    <th style="padding:12px;">Họ tên</th>
                    <th style="padding:12px;">Username</th>
                    <th style="padding:12px;">Email</th>
                    <th style="padding:12px;">Hành động</th>
                </tr>`;

        result.data.forEach(u => {
            html += `
                <tr style="border-bottom: 1px solid var(--cream3);">
                    <td style="padding:12px;"><b>${u.fullName}</b></td>
                    <td style="padding:12px;">@${u.username}</td>
                    <td style="padding:12px;">${u.email}</td>
                    <td style="padding:12px;">
                        <button onclick="adminBanUser(${u.userId})" style="background: var(--red, #c81e1e); color: white; border: none; padding: 6px 12px; border-radius: 4px; cursor: pointer;">🚨 Khóa</button>
                    </td>
                </tr>`;
        });
        html += `</table>`;
        document.getElementById('admin-main-content').innerHTML = html;
    } catch (err) {
        document.getElementById('admin-main-content').innerHTML = `<p style="color:var(--red);">Lỗi: ${err.message}</p>`;
    }
}

async function adminBanUser(userId) {
    if (!confirm('Xác nhận khóa vĩnh viễn tài khoản này?')) return;
    try {
        const res = await fetch(`${ADMIN_API}/users/${userId}`, { method: 'DELETE', headers: getAdminHeaders() });
        if (res.ok) { alert('Đã khóa thành công!'); adminLoadAllUsers(); }
    } catch (err) { alert(err.message); }
}

async function adminSubmitRestaurant() {
    const name = document.getElementById('res-name').value;
    const address = document.getElementById('res-address').value;
    if (!name) return alert('Vui lòng điền tên nhà hàng');

    try {
        const res = await fetch(`${ADMIN_API}/restaurants`, {
            method: 'POST',
            headers: getAdminHeaders(),
            body: JSON.stringify({ Name: name, Address: address })
        });
        if (res.ok) { alert('Thêm địa điểm thành công!'); switchAdminTab('restaurants'); }
    } catch (err) { alert(err.message); }
}
async function adminLoadRestaurants() {
    const content = document.getElementById('admin-main-content');
    content.innerHTML = '<div style="padding:32px;text-align:center;color:var(--ink3);">Đang tải…</div>';
    try {
        const res = await fetch(`${ADMIN_API}/restaurants`, { headers: getAdminHeaders() });
        const result = await res.json();
        if (!res.ok) throw new Error(result.message || 'Lỗi');

        let html = `
            <div style="background:white;padding:16px;border-radius:12px;border:1px solid var(--cream3);margin-bottom:16px;">
                <b>➕ Thêm nhà hàng mới</b><br><br>
                <input id="res-name"    placeholder="Tên nhà hàng" style="width:100%;padding:8px;margin-bottom:8px;border:1px solid var(--cream3);border-radius:6px;" />
                <input id="res-address" placeholder="Địa chỉ"      style="width:100%;padding:8px;margin-bottom:8px;border:1px solid var(--cream3);border-radius:6px;" />
                <button onclick="adminSubmitRestaurant()" style="background:var(--orange);color:white;border:none;padding:8px 16px;border-radius:6px;cursor:pointer;">Lưu</button>
            </div>
            <table style="width:100%;border-collapse:collapse;background:white;border-radius:8px;overflow:hidden;border:1px solid var(--cream3);">
                <tr style="background:var(--cream2);">
                    <th style="padding:12px;text-align:left;">Tên</th>
                    <th style="padding:12px;">Địa chỉ</th>
                    <th style="padding:12px;">Đánh giá</th>
                    <th style="padding:12px;">Hành động</th>
                </tr>`;
        result.data.forEach(r => {
            html += `<tr style="border-bottom:1px solid var(--cream3);">
                <td style="padding:12px;"><b>${r.name || r.Name}</b></td>
                <td style="padding:12px;">${r.address || r.Address || '—'}</td>
                <td style="padding:12px;">⭐ ${r.avgRating || r.AvgRating || 0}</td>
                <td style="padding:12px;">
                    <button onclick="adminDeleteRestaurant(${r.restaurantId || r.RestaurantId})" style="background:#FEE2E2;color:#DC2626;border:none;padding:5px 10px;border-radius:4px;cursor:pointer;">🗑️ Xóa</button>
                </td>
            </tr>`;
        });
        html += `</table>`;
        content.innerHTML = html;
    } catch (err) {
        content.innerHTML = `<p style="color:var(--red);">Lỗi: ${err.message}</p>`;
    }
}

async function adminDeleteRestaurant(id) {
    if (!confirm('Xóa nhà hàng này?')) return;
    try {
        const res = await fetch(`${ADMIN_API}/restaurants/${id}`, { method: 'DELETE', headers: getAdminHeaders() });
        if (res.ok) { alert('Đã xóa!'); adminLoadRestaurants(); }
    } catch (err) { alert(err.message); }
}