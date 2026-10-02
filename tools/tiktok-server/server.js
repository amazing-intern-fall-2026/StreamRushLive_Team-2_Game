const http = require('http');
const { Server } = require('socket.io');
const { TikTokLiveConnection } = require('tiktok-live-connector');

const PORT = process.env.PORT || 9090;
const DEFAULT_USERNAME = process.env.TIKTOK_USER || 'chickmangames';

// Tạo HTTP Server
const server = http.createServer((req, res) => {
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');

    const url = new URL(req.url, `http://${req.headers.host}`);

    if (url.pathname === '/' || url.pathname === '/status') {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({
            status: 'online',
            connectedRoom: currentUniqueId || null,
            isLiveConnected: tiktokLive ? tiktokLive.isConnected : false,
            activeClients: io.engine.clientsCount
        }));
        return;
    }

    // Endpoint test chat trực tiếp: /test-chat?cmd=red&user=Tester
    if (url.pathname === '/test-chat') {
        const cmd = url.searchParams.get('cmd') || 'blue';
        const user = url.searchParams.get('user') || 'TestUser';
        const testPayload = formatPayload({
            comment: cmd,
            content: cmd,
            text: cmd,
            userId: 'test_' + Date.now(),
            uniqueId: user,
            nickname: user
        });
        io.emit('chat', testPayload);
        console.log(`[TEST-CHAT] Đã phát sóng test comment: "${cmd}" từ "${user}"`);
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: true, sent: testPayload }));
        return;
    }

    res.writeHead(404, { 'Content-Type': 'text/plain' });
    res.end('Not Found');
});

// Khởi tạo Socket.IO
const io = new Server(server, {
    cors: {
        origin: "*",
        methods: ["GET", "POST"]
    }
});

let tiktokLive = null;
let currentUniqueId = '';

function formatPayload(data) {
    const userId = String(data.userId || data.user?.id || data.user?.displayId || data.uniqueId || 'unknown_user');
    const uniqueId = String(data.uniqueId || data.user?.displayId || data.user?.id || userId);
    const nickname = String(data.nickname || data.user?.nickname || uniqueId);
    const comment = String(data.comment || data.content || data.text || '');
    const avatar = String(data.avatarUrl || data.user?.avatarThumb?.urlList?.[0] || data.profilePictureUrl || '');

    return {
        ...data,
        userId: userId,
        uniqueId: uniqueId,
        nickname: nickname,
        comment: comment,
        content: comment,
        text: comment,
        avatarUrl: avatar,
        data: {
            user: {
                userId: userId,
                uniqueId: uniqueId,
                nickname: nickname,
                profilePictureUrl: avatar
            }
        }
    };
}

function connectToTikTok(uniqueId) {
    if (!uniqueId) return;

    if (tiktokLive && currentUniqueId === uniqueId && tiktokLive.isConnected) {
        console.log(`[TikTok] Đang giữ kết nối tốt với @${uniqueId}`);
        return;
    }

    if (tiktokLive) {
        try {
            tiktokLive.disconnect();
        } catch (e) {}
    }

    currentUniqueId = uniqueId;
    console.log(`[TikTok] Bắt đầu kết nối tới phòng Live: @${uniqueId}...`);

    // KHÔNG bật enableExtendedGiftInfo để tránh đòi hỏi token EulerStream
    tiktokLive = new TikTokLiveConnection(uniqueId, {
        processInitialData: false
    });

    tiktokLive.connect().then(state => {
        console.log(`\n=======================================================`);
        console.log(`🎉 [TikTok] ĐÃ KẾT NỐI THÀNH CÔNG VỚI LIVE CỦA @${uniqueId}!`);
        console.log(`📺 Room ID: ${state.roomId}`);
        console.log(`=======================================================\n`);

        const roomInfo = {
            roomId: state.roomId,
            uniqueId: uniqueId,
            nickname: state.roomInfo?.owner?.nickname || uniqueId,
            avatarUrl: state.roomInfo?.owner?.avatarThumb?.urlList?.[0] || ''
        };
        io.emit('roomInfo', roomInfo);
        io.emit('connected', roomInfo);
        io.emit('streamerInfo', roomInfo);
    }).catch(err => {
        console.error(`[TikTok] Lỗi kết nối tới @${uniqueId}:`, err.message || err);
        io.emit('tiktok_error', { error: err.message || String(err) });
    });

    // 1. SỰ KIỆN CHAT / COMMENT
    tiktokLive.on('chat', data => {
        const comment = data.content || data.comment || data.text || '';
        const user = data.user?.nickname || data.user?.displayId || data.nickname || 'Viewer';
        console.log(`💬 [CHAT] [${user}]: "${comment}"`);

        const payload = formatPayload({
            comment: comment,
            content: comment,
            text: comment,
            userId: data.user?.id || data.userId,
            uniqueId: data.user?.displayId || data.uniqueId,
            nickname: data.user?.nickname || data.nickname,
            avatarUrl: data.user?.avatarThumb?.urlList?.[0] || ''
        });
        io.emit('chat', payload);
    });

    // 2. SỰ KIỆN GIFT / QUÀ TẶNG
    tiktokLive.on('gift', data => {
        const giftName = data.giftName || data.giftDetails?.giftName || data.gift?.name || 'Gift';
        const user = data.user?.nickname || data.user?.displayId || data.nickname || 'Viewer';
        const repeatCount = data.repeatCount || data.comboCount || 1;
        const diamondCount = data.diamondCount || 0;

        console.log(`🎁 [GIFT] [${user}] tặng ${giftName} x${repeatCount} (${diamondCount} kim cương)`);

        const payload = formatPayload({
            giftName: giftName,
            diamondCount: diamondCount,
            coinCount: diamondCount,
            repeatCount: repeatCount,
            userId: data.user?.id || data.userId,
            uniqueId: data.user?.displayId || data.uniqueId,
            nickname: data.user?.nickname || data.nickname,
            avatarUrl: data.user?.avatarThumb?.urlList?.[0] || ''
        });
        io.emit('gift', payload);
    });

    // 3. SỰ KIỆN LIKE / THẢ TIM
    tiktokLive.on('like', data => {
        const user = data.user?.nickname || data.user?.displayId || data.nickname || 'Viewer';
        const likeCount = data.likeCount || data.count || 1;
        const totalLike = data.totalLikeCount || data.totalLike || likeCount;

        console.log(`❤️ [LIKE] [${user}] thả ${likeCount} tim (Tổng: ${totalLike})`);

        const payload = formatPayload({
            likeCount: likeCount,
            totalLike: totalLike,
            totalLikeCount: totalLike,
            userId: data.user?.id || data.userId,
            uniqueId: data.user?.displayId || data.uniqueId,
            nickname: data.user?.nickname || data.nickname,
            avatarUrl: data.user?.avatarThumb?.urlList?.[0] || ''
        });
        io.emit('like', payload);
    });

    // 4. SỰ KIỆN FOLLOW / THEO DÕI
    tiktokLive.on('follow', data => {
        const user = data.user?.nickname || data.user?.displayId || data.nickname || 'Viewer';
        console.log(`➕ [FOLLOW] [${user}] vừa follow kênh!`);

        const payload = formatPayload({
            userId: data.user?.id || data.userId,
            uniqueId: data.user?.displayId || data.uniqueId,
            nickname: data.user?.nickname || data.nickname,
            avatarUrl: data.user?.avatarThumb?.urlList?.[0] || ''
        });
        io.emit('follow', payload);
    });

    // 5. SỰ KIỆN THÀNH VIÊN VÀO PHÒNG
    tiktokLive.on('member', data => {
        const payload = formatPayload({
            userId: data.user?.id || data.userId,
            uniqueId: data.user?.displayId || data.uniqueId,
            nickname: data.user?.nickname || data.nickname,
            avatarUrl: data.user?.avatarThumb?.urlList?.[0] || ''
        });
        io.emit('member', payload);
    });

    tiktokLive.on('disconnected', () => {
        console.log(`[TikTok] Phòng Live @${uniqueId} đã ngắt kết nối.`);
        io.emit('streamEnd', {});
    });

    tiktokLive.on('error', err => {
        console.warn(`[TikTok Warning]:`, err.message || err);
    });
}

// Xử lý Client (Unity) kết nối qua Socket.IO
io.on('connection', socket => {
    console.log(`🔌 [Socket.IO] Unity Client đã kết nối thành công! SocketID=${socket.id}`);

    // Nhận username từ Unity TikTokLiveClient
    socket.on('setUniqueID', (uniqueId) => {
        const cleanId = String(uniqueId || '').replace(/^@/, '').trim();
        console.log(`[Socket.IO] Nhận yêu cầu setUniqueID từ Unity: "${cleanId}"`);
        if (cleanId) {
            connectToTikTok(cleanId);
        }
    });

    // Nếu đã có kết nối với TikTok từ trước, gửi ngay thông tin room cho Unity
    if (tiktokLive && tiktokLive.isConnected) {
        socket.emit('connected', { uniqueId: currentUniqueId });
    }

    socket.on('disconnect', (reason) => {
        console.log(`🔌 [Socket.IO] Unity Client ngắt kết nối: ${reason}`);
    });
});

// Khởi động server
server.listen(PORT, () => {
    console.log(`=======================================================`);
    console.log(`🚀 TikTok Live Connector Server đang chạy tại: http://localhost:${PORT}`);
    console.log(`🎮 Cấu hình Unity TikTokLiveClient Server URL: http://localhost:${PORT}`);
    console.log(`📡 Kênh live mục tiêu: @${DEFAULT_USERNAME}`);
    console.log(`=======================================================`);

    // Tự động kết nối tới kênh mặc định
    if (DEFAULT_USERNAME) {
        connectToTikTok(DEFAULT_USERNAME);
    }
});
