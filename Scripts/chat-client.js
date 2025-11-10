$(function () {
    const config = window.chatConfig || {};
    const currentUsername = config.currentUsername || '';
    const urls = config.urls || {};
    let antiForgeryToken = config.antiForgeryToken || '';
    antiForgeryToken = $('#logoutForm input[name="__RequestVerificationToken"]').val() || antiForgeryToken;

    window.chatHub = $.connection.chatHub;
    const chatHub = window.chatHub;

    window.currentChat = {
        mode: 'ai',
        partnerUsername: null,
        groupId: null,
        isOnline: false
    };
    let currentChat = window.currentChat;

    let onlineUsers = new Set();
    let userLastSeen = {};
    let chatNicknames = {};
    let chatBackgrounds = {};
    let tempFilesToSend = null; // Lưu files tạm thời

    // ========== TYPING INDICATOR VARIABLES ==========
    let typingTimer = null;
    let isTyping = false;
    const TYPING_TIMEOUT = 3000; // 3 giây không gõ sẽ tắt typing indicator

    // ========== CALL VARIABLES ==========
    let peerConnection = null;
    let localStream = null;
    let remoteStream = null;
    let currentCallType = null;
    let currentCallPartner = null;
    let callTimeout = null;
    let callStartTime = null;

    const configuration = {
        iceServers: [
            { urls: 'stun:stun.l.google.com:19302' },
            { urls: 'stun:stun1.l.google.com:19302' }
        ]
    };

    // ========== HELPER FUNCTIONS ==========
    function loadNicknames() {
        const stored = localStorage.getItem('chatNicknames');
        if (stored) {
            try {
                chatNicknames = JSON.parse(stored);
            } catch (e) {
                chatNicknames = {};
            }
        }
    }

    function saveNicknames() {
        localStorage.setItem('chatNicknames', JSON.stringify(chatNicknames));
    }

    function getConversationId() {
        if (currentChat.mode === 'private') {
            return `private_${currentUsername}_${currentChat.partnerUsername}`.split('_').sort().join('_');
        } else if (currentChat.mode === 'group') {
            return `group_${currentChat.groupId}`;
        }
        return 'ai';
    }

    function getNickname(username, conversationId) {
        conversationId = conversationId || getConversationId();
        if (chatNicknames[conversationId] && chatNicknames[conversationId][username]) {
            return chatNicknames[conversationId][username];
        }
        return null;
    }

    function loadBackgrounds() {
        const stored = localStorage.getItem('chatBackgrounds');
        if (stored) {
            try {
                chatBackgrounds = JSON.parse(stored);
            } catch (e) {
                chatBackgrounds = {};
            }
        }
    }

    function saveBackgrounds() {
        localStorage.setItem('chatBackgrounds', JSON.stringify(chatBackgrounds));
    }

    function applyBackground(backgroundUrl) {
        const $messageArea = $('.message-area');
        if (backgroundUrl && backgroundUrl !== 'none') {
            $messageArea.css('background-image', `url(${backgroundUrl})`);
        } else {
            $messageArea.css('background-image', 'none');
        }
    }

    function isUserOnline(username) {
        return onlineUsers.has(username);
    }

    function getLastSeenText(username) {
        if (isUserOnline(username)) {
            return 'Đang hoạt động';
        }
        if (userLastSeen[username]) {
            const minutesAgo = Math.floor((Date.now() - userLastSeen[username]) / 60000);
            if (minutesAgo < 1) return 'Vừa xong';
            if (minutesAgo < 60) return `Offline ${minutesAgo} phút trước`;
            const hoursAgo = Math.floor(minutesAgo / 60);
            if (hoursAgo < 24) return `Offline ${hoursAgo} giờ trước`;
            return `Offline ${Math.floor(hoursAgo / 24)} ngày trước`;
        }
        return 'Offline';
    }

    function updateStatusIndicator(username, isOnline) {
        $(`.status-indicator[data-username="${username}"]`).removeClass('online offline').addClass(isOnline ? 'online' : 'offline');

        if (currentChat.mode === 'private' && currentChat.partnerUsername === username) {
            $('#chat-header-status').text(getLastSeenText(username)).toggleClass('online', isOnline);
        }
    }

    function formatTimestamp(date) {
        return date.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    }

    // Parse timestamp từ nhiều format khác nhau
    function parseTimestamp(timestamp) {
        if (!timestamp) return null;

        // Nếu là string format ASP.NET: /Date(1234567890)/
        if (typeof timestamp === 'string' && timestamp.startsWith('/Date(')) {
            const ms = parseInt(timestamp.replace(/\/Date\((\d+)\)\//, '$1'));
            return new Date(ms);
        }

        // Nếu là ISO string hoặc format khác
        const date = new Date(timestamp);
        return isNaN(date.getTime()) ? null : date;
    }

    // ========== TYPING INDICATOR FUNCTIONS ==========
    function showTypingIndicator(username, avatarUrl) {
        const $indicator = $('#typing-indicator');
        const $avatar = $indicator.find('.typing-avatar');

        // Set avatar
        if (avatarUrl && avatarUrl !== '/Content/default-avatar.png') {
            $avatar.css('background-image', `url(${avatarUrl})`);
        } else {
            $avatar.css('background-image', 'none');
            $avatar.css('background-color', '#e0e0e0');
        }

        $indicator.fadeIn(200);

        // Auto scroll to bottom
        const messagesList = $('#messagesList');
        messagesList.scrollTop(messagesList[0].scrollHeight);
    }

    function hideTypingIndicator() {
        $('#typing-indicator').fadeOut(200);
    }

    function sendTypingSignal() {
        if (currentChat.mode === 'private' && currentChat.partnerUsername) {
            console.log('🔄 Sending typing signal to:', currentChat.partnerUsername);

            if (!chatHub.server.userTyping) {
                console.error('❌ userTyping method not found! Available:', Object.keys(chatHub.server));
                return;
            }

            if (!isTyping) {
                isTyping = true;
                chatHub.server.userTyping(currentChat.partnerUsername)
                    .done(() => console.log('✅ Typing signal sent'))
                    .fail(err => console.error('❌ Typing signal failed:', err));
            }

            clearTimeout(typingTimer);
            typingTimer = setTimeout(() => {
                isTyping = false;
                chatHub.server.userStoppedTyping(currentChat.partnerUsername);
                console.log('⏹ Stopped typing');
            }, TYPING_TIMEOUT);
        }
    }

    function playNotificationSound() {
        if (localStorage.getItem('playSounds') !== 'false') {
            const sound = document.getElementById('notification-sound');
            if (sound) sound.play().catch(e => { });
        }
    }

    function createCallLogHtml(contentObj) {
        const callType = contentObj.callType || 'voice';
        const duration = contentObj.duration || 0;
        const callStatus = contentObj.status || 'missed';

        let iconClass = callType === 'video' ? 'fa-video' : 'fa-phone-alt';
        let statusText = '';
        let statusColor = '';

        switch (callStatus) {
            case 'completed':
                const minutes = Math.floor(duration / 60);
                const seconds = duration % 60;
                const durationText = minutes > 0 ? `${minutes} phút ${seconds} giây` : `${seconds} giây`;
                statusText = `Cuộc gọi ${callType === 'video' ? 'video' : 'thoại'} - ${durationText}`;
                statusColor = '#4caf50';
                break;
            case 'missed':
                statusText = 'Cuộc gọi nhỡ';
                statusColor = '#f44336';
                break;
            case 'declined':
                statusText = 'Cuộc gọi bị từ chối';
                statusColor = '#ff9800';
                break;
            default:
                statusText = 'Cuộc gọi';
                statusColor = '#999';
        }

        return `
        <div style="display:flex; align-items:center; gap:10px;">
            <i class="fas ${iconClass}" style="font-size:1.2rem; color:${statusColor};"></i>
            <div style="font-weight:500; color:${statusColor};">${statusText}</div>
        </div>
        <button class="btn btn-light btn-sm w-100 mt-2 call-back-btn" data-call-type="${callType}">
            <i class="fas fa-phone-alt"></i> Gọi lại
        </button>`;
    }

    // ========== MESSAGE RENDERING - FIXED ==========
    function renderMessage(msgData) {
        $('#ai-welcome-screen').hide();
        $('.message-area').show();

        const isSelf = msgData.isSelf || msgData.senderUsername === currentUsername;

        let msgDate;
        try {
            if (msgData.timestamp) {
                msgDate = parseTimestamp(msgData.timestamp);
                if (!msgDate) {
                    console.warn('Invalid timestamp, using current time:', msgData.timestamp);
                    msgDate = new Date();
                }
            } else {
                msgDate = new Date();
            }
        } catch (e) {
            console.error('Error parsing timestamp:', e);
            msgDate = new Date();
        }

        const vietTime = formatTimestamp(msgDate);
        const uniqueId = `msg-${msgDate.getTime()}-${Math.random().toString(36).substr(2, 9)}`;

        let contentObj;
        try {
            contentObj = JSON.parse(msgData.content);
        } catch (e) {
            contentObj = { type: 'text', content: msgData.content };
        }

        const conversationId = getConversationId();
        const displayName = getNickname(msgData.senderUsername, conversationId) || msgData.senderUsername;

        let bubbleContentHtml = '';
        switch (contentObj.type) {
            case 'image':
                bubbleContentHtml = `<img src="${contentObj.content}" class="img-fluid rounded" style="max-width: 250px; cursor: pointer;" onclick="openImageLightbox('${contentObj.content}');" />`;
                break;
            case 'video':
                bubbleContentHtml = `<video controls src="${contentObj.content}" style="max-width: 300px; border-radius: 10px;"></video>`;
                break;
            case 'file':
                bubbleContentHtml = `
                    <a href="${contentObj.content}" target="_blank" style="display:flex; align-items:center; padding:8px 12px; background:#f0f0f0; border-radius:8px; text-decoration:none; color:#333;">
                        <i class="fas fa-file-alt" style="font-size:1.5rem; margin-right:10px; color:#007bff;"></i>
                        <div>
                            <div style="font-weight:600; font-size:0.9rem;">${contentObj.fileName || 'File'}</div>
                            <div style="font-size:0.75rem; color:#666;">${contentObj.fileSize || ''}</div>
                        </div>
                    </a>`;
                break;
            case 'call_log':
                bubbleContentHtml = createCallLogHtml(contentObj);
                break;
            default:
                const escaped = $('<div/>').text(contentObj.content).html();
                bubbleContentHtml = `<span>${escaped}</span>`;
        }

        let avatarHtml = '';
        if (!isSelf) {
            if (msgData.senderAvatar && msgData.senderAvatar !== '/Content/default-avatar.png') {
                avatarHtml = `<img src="${msgData.senderAvatar}" class="avatar" alt="${displayName}" />`;
            } else {
                const friendAvatar = $(`.friend-item[data-username="${msgData.senderUsername}"]`).data('avatar-url');
                if (friendAvatar && friendAvatar !== '/Content/default-avatar.png') {
                    avatarHtml = `<img src="${friendAvatar}" class="avatar" alt="${displayName}" />`;
                } else { 
                    const firstLetter = msgData.senderUsername.charAt(0).toUpperCase();
                    avatarHtml = `<div class="avatar" title="${msgData.senderUsername}">${firstLetter}</div>`;
                }
            }
        }

        let nicknameHtml = '';
        if (!isSelf && currentChat.mode === 'group') {
            nicknameHtml = `<div class="message-nickname">${displayName}</div>`;
        }

        const messageHtml = `
            <div class="chat-message ${isSelf ? 'self' : msgData.senderUsername === 'AI Assistant' ? 'ai-message' : 'other'}" 
                 data-timestamp="${msgDate.toISOString()}"  
                 data-message-id="${uniqueId}">
                ${avatarHtml}
                <div class="message-container">
                    ${nicknameHtml}
                    <div class="chat-bubble ${msgData.senderUsername === 'AI Assistant' ? 'ai-bubble' : ''}">
                        ${bubbleContentHtml}
                        <span class="bubble-time">${vietTime}</span>
                    </div>
                </div>
            </div>`;

        $('#messagesList').append(messageHtml);
        $('#messagesList').scrollTop($('#messagesList')[0].scrollHeight);
    }

    // ========== SIGNALR HANDLERS ==========
    chatHub.client.receiveMessage = function (senderUsername, senderAvatar, messageJson, timestamp) {
        if (senderUsername === currentUsername) return;

        if (senderUsername === 'AI Assistant') {
            // Ẩn typing indicator của AI
            hideTypingIndicator();

            if (currentChat.mode === 'ai') {
                renderMessage({
                    senderUsername: 'AI Assistant',
                    content: messageJson,
                    timestamp: timestamp, // Dùng timestamp từ server
                    isSelf: false
                });
            }
            playNotificationSound();
            return;
        }

        if (currentChat.mode === 'private' && currentChat.partnerUsername === senderUsername) {
            // Ẩn typing indicator khi nhận tin nhắn
            hideTypingIndicator();

            renderMessage({
                senderUsername: senderUsername,
                senderAvatar: senderAvatar,
                content: messageJson,
                timestamp: timestamp,
                isSelf: false
            });
            playNotificationSound();
        }
    };

    chatHub.client.userConnected = function (username) {
        onlineUsers.add(username);
        updateStatusIndicator(username, true);
    };

    chatHub.client.userDisconnected = function (username) {
        onlineUsers.delete(username);
        userLastSeen[username] = Date.now();
        updateStatusIndicator(username, false);
    };

    chatHub.client.updateOnlineUsers = function (users) {
        onlineUsers = new Set(users);
        users.forEach(username => {
            updateStatusIndicator(username, true);
        });
    };

    // ========== TYPING INDICATOR SIGNALR HANDLERS ==========
    chatHub.client.userTyping = function (username) {
        // Chỉ hiển thị nếu đang chat với người đó
        if (currentChat.mode === 'private' && currentChat.partnerUsername === username) {
            const $friendItem = $(`.friend-item[data-username="${username}"]`);
            const avatarUrl = $friendItem.data('avatar-url') || '/Content/default-avatar.png';
            showTypingIndicator(username, avatarUrl);
        }
    };

    chatHub.client.userStoppedTyping = function (username) {
        if (currentChat.mode === 'private' && currentChat.partnerUsername === username) {
            hideTypingIndicator();
        }
    };

    // ========== CALL SIGNALR HANDLERS ==========
    chatHub.client.receiveCallOffer = async function (fromUsername, offerSdp, callType) {
        console.log('📞 Incoming call from:', fromUsername);

        currentCallType = callType;
        currentCallPartner = fromUsername;

        const caller = $(`.friend-item[data-username="${fromUsername}"]`);
        const callerName = caller.find('strong').text().trim() || fromUsername;
        const callerAvatar = caller.data('avatar-url') || '/Content/default-avatar.png';

        $('#incoming-call-name').text(callerName);
        $('#incoming-call-avatar').attr('src', callerAvatar);
        $('#incoming-call-type').text(callType === 'video' ? 'Cuộc gọi video đến...' : 'Cuộc gọi thoại đến...');
        $('#incomingCallModal').modal('show');

        window.pendingCallOffer = offerSdp;

        callTimeout = setTimeout(() => {
            if ($('#incomingCallModal').is(':visible')) {
                $('#incomingCallModal').modal('hide');
                chatHub.server.declineCall(fromUsername, 'missed');

                const logMessage = {
                    type: "call_log",
                    status: "missed",
                    duration: 0,
                    callType: callType
                };
                renderMessage({
                    senderUsername: fromUsername,
                    content: JSON.stringify(logMessage),
                    timestamp: new Date().toISOString(),
                    isSelf: false
                });
            }
        }, 20000);
    };

    chatHub.client.receiveCallAnswer = async function (fromUsername, answerSdp) {
        console.log('📞 Call answer received from:', fromUsername);

        if (callTimeout) {
            clearTimeout(callTimeout);
            callTimeout = null;
        }

        try {
            const answer = JSON.parse(answerSdp);
            await peerConnection.setRemoteDescription(new RTCSessionDescription(answer));
            callStartTime = new Date();
            $('#call-view-status').text('Đã kết nối');
        } catch (error) {
            console.error('Error handling call answer:', error);
        }
    };

    chatHub.client.receiveIceCandidate = async function (fromUsername, candidateJson) {
        try {
            const candidate = JSON.parse(candidateJson);
            await peerConnection.addIceCandidate(new RTCIceCandidate(candidate));
        } catch (error) {
            console.error('Error adding ICE candidate:', error);
        }
    };

    chatHub.client.callEnded = function (fromUsername) {
        console.log('📞 Call ended by:', fromUsername);
        endCall(false);
    };

    chatHub.client.callDeclined = function (fromUsername, reason) {
        console.log('📞 Call declined by:', fromUsername, 'Reason:', reason);

        if (callTimeout) {
            clearTimeout(callTimeout);
            callTimeout = null;
        }

        endCall(false);

        const logMessage = {
            type: "call_log",
            status: reason || 'declined',
            duration: 0,
            callType: currentCallType
        };
        renderMessage({
            senderUsername: currentUsername,
            content: JSON.stringify(logMessage),
            timestamp: new Date().toISOString(),
            isSelf: true
        });
    };

    // ========== CALL FUNCTIONS ==========
    async function startCall(callType) {
        if (!currentChat.partnerUsername) {
            alert('Vui lòng chọn người để gọi!');
            return;
        }

        currentCallType = callType;
        currentCallPartner = currentChat.partnerUsername;

        try {
            const constraints = {
                audio: true,
                video: callType === 'video'
            };

            localStream = await navigator.mediaDevices.getUserMedia(constraints);

            $('#call-view').fadeIn(300);
            $('#call-view-name').text($('#chat-header-displayname').text());
            $('#call-view-status').text('Đang gọi...');
            $('#call-view-avatar').attr('src', $('#chat-header-avatar').attr('src'));

            const localVideo = document.getElementById('localVideo');
            if (localVideo) {
                localVideo.srcObject = localStream;
            }

            setupPeerConnection();

            const offer = await peerConnection.createOffer();
            await peerConnection.setLocalDescription(offer);

            chatHub.server.sendCallOffer(currentCallPartner, JSON.stringify(offer), callType);

            callTimeout = setTimeout(() => {
                console.log('⏰ Call timeout - no answer after 20s');
                endCall(true);

                const logMessage = {
                    type: "call_log",
                    status: "missed",
                    duration: 0,
                    callType: callType
                };
                renderMessage({
                    senderUsername: currentUsername,
                    content: JSON.stringify(logMessage),
                    timestamp: new Date().toISOString(),
                    isSelf: true
                });
            }, 20000);

        } catch (error) {
            console.error('Error starting call:', error);
            alert('Không thể truy cập camera/microphone!');
            endCall(false);
        }
    }

    function setupPeerConnection() {
        peerConnection = new RTCPeerConnection(configuration);

        if (localStream) {
            localStream.getTracks().forEach(track => {
                peerConnection.addTrack(track, localStream);
            });
        }

        peerConnection.ontrack = (event) => {
            console.log('📹 Received remote track');
            remoteStream = event.streams[0];
            const remoteVideo = document.getElementById('remoteVideo');
            if (remoteVideo) {
                remoteVideo.srcObject = remoteStream;
                $('#call-info-overlay').fadeOut();
                $('#call-view-status').text('Đã kết nối');
            }
        };

        peerConnection.onicecandidate = (event) => {
            if (event.candidate) {
                chatHub.server.sendIceCandidate(currentCallPartner, JSON.stringify(event.candidate));
            }
        };

        peerConnection.onconnectionstatechange = () => {
            console.log('Connection state:', peerConnection.connectionState);
            if (peerConnection.connectionState === 'connected') {
                if (callTimeout) {
                    clearTimeout(callTimeout);
                    callTimeout = null;
                }
                callStartTime = new Date();
                $('#call-view-status').text('Đã kết nối');
            } else if (peerConnection.connectionState === 'disconnected' || peerConnection.connectionState === 'failed') {
                endCall(false);
            }
        };
    }

    function endCall(sendLog = true) {
        if (callTimeout) {
            clearTimeout(callTimeout);
            callTimeout = null;
        }

        if (sendLog && currentCallPartner) {
            let status = 'missed';
            let durationSeconds = 0;

            if (callStartTime) {
                const durationMs = new Date() - callStartTime;
                durationSeconds = Math.round(durationMs / 1000);
                status = 'completed';
            }

            const logMessage = {
                type: "call_log",
                status: status,
                duration: durationSeconds,
                callType: currentCallType
            };

            chatHub.server.sendPrivateMessage(currentCallPartner, JSON.stringify(logMessage));
            chatHub.server.endCall(currentCallPartner);
        }

        if (peerConnection) {
            peerConnection.close();
            peerConnection = null;
        }

        if (localStream) {
            localStream.getTracks().forEach(track => track.stop());
            localStream = null;
        }

        remoteStream = null;

        $('#call-view').fadeOut(300);
        $('#call-info-overlay').fadeIn();
        $('#incomingCallModal').modal('hide');

        $('#toggle-video-btn, #toggle-mic-btn').removeClass('btn-danger').addClass('btn-light');
        $('#toggle-video-btn i').removeClass('fa-video-slash').addClass('fa-video');
        $('#toggle-mic-btn i').removeClass('fa-microphone-slash').addClass('fa-microphone');

        callStartTime = null;
        currentCallPartner = null;
        currentCallType = null;
    }

    // ========== SEARCH FUNCTIONALITY - FIXED ==========
    $('#toggle-search-sidebar-btn').on('click', function () {
        $('#conversation-info-sidebar').hide();
        $('#toggle-info-sidebar-btn').removeClass('active');

        const $sidebar = $('#search-sidebar');
        if ($sidebar.length === 0) {
            // Tạo sidebar nếu chưa có
            const sidebarHtml = `
                <div id="search-sidebar" style="display: none;">
                    <div class="search-sidebar-header">
                        <h5>Tìm kiếm tin nhắn</h5>
                        <button id="close-search-sidebar-btn" type="button" class="close">
                            <span>&times;</span>
                        </button>
                    </div>
                    <div class="search-input-wrapper">
                        <i class="fas fa-search search-icon"></i>
                        <input type="text" id="search-input" placeholder="Tìm kiếm..." />
                    </div>
                    <div class="search-sidebar-body">
                        <div id="search-welcome">
                            <div class="search-welcome-icon">
                                <i class="fas fa-search"></i>
                            </div>
                            <p>Nhập từ khóa để tìm kiếm tin nhắn</p>
                        </div>
                        <div id="search-results-list" style="display: none;"></div>
                    </div>
                </div>`;
            $('.chat-view-wrapper').append(sidebarHtml);
        }

        $('#search-sidebar').slideDown(300);
        $(this).addClass('active');
        $('#search-input').focus();
    });

    $('body').on('click', '#close-search-sidebar-btn', function () {
        $('#search-sidebar').slideUp(300);
        $('#toggle-search-sidebar-btn').removeClass('active');
        $('#messagesList .chat-message').show();
        clearHighlights();
    });

    $('body').on('keyup', '#search-input', function () {
        const searchTerm = $(this).val().toLowerCase().trim();
        const $resultsList = $('#search-results-list');
        const $welcomeScreen = $('#search-welcome');

        clearHighlights();
        $resultsList.empty();

        if (searchTerm === "") {
            $welcomeScreen.show();
            $resultsList.hide();
            $('#messagesList .chat-message').show();
            return;
        }

        $welcomeScreen.hide();
        $resultsList.show();

        let resultCount = 0;
        const partnerName = $('#chat-header-displayname').text();
        const partnerAvatar = $('#chat-header-avatar').attr('src');
        const selfAvatar = $('#layout-user-avatar-img').attr('src') || '/Content/default-avatar.png';

        $('#messagesList .chat-message').each(function () {
            const $message = $(this);
            const $bubble = $message.find('.chat-bubble span').first();
            if ($bubble.length === 0) return;

            const messageText = $bubble.text().toLowerCase();

            if (messageText.includes(searchTerm)) {
                resultCount++;
                $message.show();

                const originalText = $bubble.text();
                const highlightedText = originalText.replace(new RegExp(searchTerm, 'gi'), '<span class="highlight">$&</span>');
                $bubble.html(highlightedText);

                const senderName = $message.hasClass('self') ? "Bạn" : partnerName;
                const senderAvatar = $message.hasClass('self') ? selfAvatar : partnerAvatar;
                const timestamp = $message.data('timestamp');
                const timeText = $message.find('.bubble-time').text();

                const resultHtml = `
                    <a href="#" class="search-result-item" data-scroll-to="${timestamp}">
                        <img src="${senderAvatar}" class="search-result-avatar" />
                        <div class="search-result-content">
                            <div>
                                <span class="search-result-sender">${senderName}</span>
                                <span class="search-result-time">${timeText}</span>
                            </div>
                            <div class="search-result-text">${$bubble.html()}</div>
                        </div>
                    </a>`;
                $resultsList.append(resultHtml);
            } else {
                $message.hide();
            }
        });

        if (resultCount === 0) {
            $resultsList.html('<p class="text-muted text-center small p-3">Không tìm thấy kết quả nào.</p>');
        }
    });

    function clearHighlights() {
        $('#messagesList .chat-message').find('.highlight').each(function () {
            const $parent = $(this).parent();
            $parent.html($parent.text());
        });
    }

    $('body').on('click', '.search-result-item', function (e) {
        e.preventDefault();
        const targetTimestamp = $(this).data('scroll-to');
        const $targetMessage = $(`#messagesList .chat-message[data-timestamp="${targetTimestamp}"]`);

        if ($targetMessage.length > 0) {
            $('#messagesList').scrollTop($('#messagesList').scrollTop() + $targetMessage.position().top - 50);

            $targetMessage.css('transition', 'background-color 0.2s');
            $targetMessage.css('background-color', '#fffb8f');
            setTimeout(function () {
                $targetMessage.css('background-color', '');
            }, 1000);
        }
    });

    // ========== EVENT HANDLERS ==========
    $('#start-voice-call-btn').on('click', function () {
        startCall('voice');
    });

    $('#start-video-call-btn').on('click', function () {
        startCall('video');
    });

    $('#accept-call-btn').on('click', async function () {
        $('#incomingCallModal').modal('hide');

        if (callTimeout) {
            clearTimeout(callTimeout);
            callTimeout = null;
        }

        try {
            const constraints = {
                audio: true,
                video: currentCallType === 'video'
            };

            localStream = await navigator.mediaDevices.getUserMedia(constraints);

            $('#call-view').fadeIn(300);
            $('#call-view-name').text($('#incoming-call-name').text());
            $('#call-view-status').text('Đang kết nối...');
            $('#call-view-avatar').attr('src', $('#incoming-call-avatar').attr('src'));

            const localVideo = document.getElementById('localVideo');
            if (localVideo) {
                localVideo.srcObject = localStream;
            }

            setupPeerConnection();

            const offer = JSON.parse(window.pendingCallOffer);
            await peerConnection.setRemoteDescription(new RTCSessionDescription(offer));

            const answer = await peerConnection.createAnswer();
            await peerConnection.setLocalDescription(answer);

            chatHub.server.sendCallAnswer(currentCallPartner, JSON.stringify(answer));

        } catch (error) {
            console.error('Error accepting call:', error);
            alert('Không thể truy cập camera/microphone!');
            endCall(false);
        }
    });

    $('#decline-call-btn').on('click', function () {
        if (callTimeout) {
            clearTimeout(callTimeout);
            callTimeout = null;
        }

        $('#incomingCallModal').modal('hide');

        if (currentCallPartner) {
            chatHub.server.declineCall(currentCallPartner, 'declined');
        }

        currentCallPartner = null;
        currentCallType = null;
    });

    $('#hang-up-btn').on('click', function () {
        endCall(true);
    });

    $('#toggle-video-btn').on('click', function () {
        if (!localStream) return;

        const videoTrack = localStream.getVideoTracks()[0];
        if (videoTrack) {
            videoTrack.enabled = !videoTrack.enabled;
            $(this).toggleClass('btn-light btn-danger');
            $(this).find('i').toggleClass('fa-video fa-video-slash');
        }
    });

    $('#toggle-mic-btn').on('click', function () {
        if (!localStream) return;

        const audioTrack = localStream.getAudioTracks()[0];
        if (audioTrack) {
            audioTrack.enabled = !audioTrack.enabled;
            $(this).toggleClass('btn-light btn-danger');
            $(this).find('i').toggleClass('fa-microphone fa-microphone-slash');
        }
    });

    $('body').on('click', '.call-back-btn', function () {
        const callType = $(this).data('call-type');
        startCall(callType);
    });

    // ========== INFO SIDEBAR ==========
    $('#toggle-info-sidebar-btn').on('click', function () {
        const $sidebar = $('#conversation-info-sidebar');
        const isVisible = $sidebar.is(':visible');

        $('#search-sidebar').hide();
        $('#toggle-search-sidebar-btn').removeClass('active');

        if (isVisible) {
            $sidebar.slideUp(300);
            $(this).removeClass('active');
        } else {
            $sidebar.slideDown(300);
            $(this).addClass('active');
            loadConversationInfo();
            loadNicknameInputs();
        }
    });

    $('#close-info-sidebar-btn').on('click', function () {
        $('#conversation-info-sidebar').slideUp(300);
        $('#toggle-info-sidebar-btn').removeClass('active');
    });

    function loadConversationInfo() {
        if (currentChat.mode !== 'private' || !currentChat.partnerUsername) {
            return;
        }

        $('#info-sidebar-avatar').attr('src', $('#chat-header-avatar').attr('src'));
        $('#info-sidebar-displayname').text($('#chat-header-displayname').text());

        $.getJSON(urls.getConversationInfo, { partnerUsername: currentChat.partnerUsername }, function (response) {
            if (response.success) {
                const $imagesList = $('#info-sidebar-images-list');
                $imagesList.empty();

                if (response.images && response.images.length > 0) {
                    response.images.forEach(img => {
                        const $item = $(`
                        <a href="${img.Url}" target="_blank" class="info-image-item">
                            <img src="${img.Url}" alt="Ảnh" />
                        </a>`);
                        $imagesList.append($item);
                    });
                } else {
                    $imagesList.html('<p class="text-muted text-center small p-3">Chưa có ảnh/video nào.</p>');
                }

                const $filesList = $('#info-sidebar-files-list');
                $filesList.empty();

                if (response.files && response.files.length > 0) {
                    response.files.forEach(file => {
                        const $item = $(`
                        <a href="${file.Url}" target="_blank" class="info-file-item">
                            <i class="fas fa-file-word file-icon"></i>
                            <div class="file-info">
                                <div class="file-name" title="${file.FileName}">${file.FileName}</div>
                                <div class="file-meta">${file.Timestamp}</div>
                            </div>
                        </a>`);
                        $filesList.append($item);
                    });
                } else {
                    $filesList.html('<p class="text-muted text-center small p-3">Chưa có file nào.</p>');
                }
            }
        });
    }

    function loadNicknameInputs() {
        if (currentChat.mode !== 'private') {
            $('#nickname-section').hide();
            return;
        }

        $('#nickname-section').show();
        const conversationId = getConversationId();
        const nicks = chatNicknames[conversationId] || {};

        $('#my-nickname-input').val(nicks[currentUsername] || '');
        $('#partner-nickname-input').val(nicks[currentChat.partnerUsername] || '');

        const partnerDisplayName = $('#chat-header-displayname').text() || currentChat.partnerUsername;
        $('#partner-nickname-label').text(`Biệt danh của ${partnerDisplayName}`);

        updateNicknamePreview();
    }

    $('#my-nickname-input, #partner-nickname-input').on('input', function () {
        updateNicknamePreview();
    });

    function updateNicknamePreview() {
        const myNick = $('#my-nickname-input').val().trim();
        const partnerNick = $('#partner-nickname-input').val().trim();

        if (myNick) {
            $('#my-nickname-preview').text(`Hiển thị: "${myNick}"`).css('color', '#43a047');
        } else {
            $('#my-nickname-preview').text('Để trống nếu muốn dùng tên thật').css('color', '#999');
        }

        if (partnerNick) {
            $('#partner-nickname-preview').text(`Hiển thị: "${partnerNick}"`).css('color', '#43a047');
        } else {
            $('#partner-nickname-preview').text('Chỉ bạn nhìn thấy biệt danh này').css('color', '#999');
        }
    }

    $('#save-nicknames-btn').on('click', function () {
        if (currentChat.mode !== 'private') return;

        const conversationId = getConversationId();
        if (!chatNicknames[conversationId]) {
            chatNicknames[conversationId] = {};
        }

        const myNick = $('#my-nickname-input').val().trim();
        const partnerNick = $('#partner-nickname-input').val().trim();

        if (myNick) {
            chatNicknames[conversationId][currentUsername] = myNick;
        } else {
            delete chatNicknames[conversationId][currentUsername];
        }

        if (partnerNick) {
            chatNicknames[conversationId][currentChat.partnerUsername] = partnerNick;
            $('#chat-header-displayname').text(partnerNick);
            $('#info-sidebar-displayname').text(partnerNick);
        } else {
            delete chatNicknames[conversationId][currentChat.partnerUsername];
            const originalName = $(`.friend-item[data-username="${currentChat.partnerUsername}"]`).find('strong').text().trim();
            $('#chat-header-displayname').text(originalName);
            $('#info-sidebar-displayname').text(originalName);
        }

        saveNicknames();

        const $btn = $('#save-nicknames-btn');
        const originalText = $btn.html();
        $btn.html('<i class="fas fa-check-circle"></i> Đã lưu!').prop('disabled', true);

        setTimeout(() => {
            $btn.html(originalText).prop('disabled', false);
        }, 2000);

        $('#messagesList').empty();
        loadChatHistory(currentChat.partnerUsername);
    });

    // ========== BACKGROUND MANAGEMENT ==========
    $('#change-background-btn').on('click', function () {
        $('#backgroundModal').modal('show');
        loadDefaultBackgrounds();

        $.get('/Account/IsAdmin', function (isAdmin) {
            if (isAdmin) {
                $('#admin-upload-section').show();
                $('#user-upload-message').hide();
            }
        });
    });

    function loadDefaultBackgrounds() {
        const defaultBackgrounds = [
            '/Content/backgrounds/bg1.jpg',
            '/Content/backgrounds/bg2.jpg',
            '/Content/backgrounds/bg3.jpg',
            '/Content/backgrounds/bg4.jpg',
            '/Content/backgrounds/bg5.jpg',
            '/Content/backgrounds/bg6.jpg'
        ];

        const $grid = $('#default-themes-grid');
        $grid.empty();

        defaultBackgrounds.forEach(bgUrl => {
            const $item = $(`
                <div class="background-theme-item" data-bg-url="${bgUrl}" style="background-image: url(${bgUrl});"></div>
            `);
            $grid.append($item);
        });

        const conversationId = getConversationId();
        const currentBg = chatBackgrounds[conversationId];
        if (currentBg) {
            $(`.background-theme-item[data-bg-url="${currentBg}"]`).addClass('active');
        }
    }

    $('body').on('click', '.background-theme-item', function () {
        $('.background-theme-item').removeClass('active');
        $(this).addClass('active');

        const bgUrl = $(this).data('bg-url');
        const conversationId = getConversationId();
        chatBackgrounds[conversationId] = bgUrl;
        saveBackgrounds();
        applyBackground(bgUrl);
    });

    $('#removeBackgroundBtn').on('click', function () {
        const conversationId = getConversationId();
        delete chatBackgrounds[conversationId];
        saveBackgrounds();
        applyBackground(null);
        $('.background-theme-item').removeClass('active');
    });

    $('#uploadBackgroundBtn').on('click', function () {
        const file = $('#customBackgroundInput')[0].files[0];
        if (!file) {
            alert('Vui lòng chọn ảnh!');
            return;
        }

        const formData = new FormData();
        formData.append('file', file);

        $.ajax({
            url: '/Admin/UploadBackground',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {
                if (response.success) {
                    alert('Upload thành công!');
                    loadDefaultBackgrounds();
                } else {
                    alert('Lỗi: ' + response.message);
                }
            },
            error: function () {
                alert('Lỗi kết nối!');
            }
        });
    });

    // ========== CLEAR HISTORY ==========
    $('#info-action-clear-history').on('click', function (e) {
        e.preventDefault();

        if (!confirm('Bạn có chắc muốn xóa toàn bộ lịch sử trò chuyện? Hành động này không thể hoàn tác!')) {
            return;
        }

        $.ajax({
            url: urls.clearHistory,
            type: 'POST',
            data: {
                partnerUsername: currentChat.partnerUsername,
                __RequestVerificationToken: antiForgeryToken
            },
            success: function (response) {
                if (response.success) {
                    $('#messagesList').empty();
                    alert('Đã xóa lịch sử trò chuyện!');
                } else {
                    alert('Lỗi: ' + response.message);
                }
            },
            error: function () {
                alert('Lỗi kết nối!');
            }
        });
    });

    // ========== HIDE CHAT ==========
    $('#info-action-hide-chat').on('change', function () {
        const isHidden = $(this).prop('checked');
        const hiddenChats = JSON.parse(localStorage.getItem('hiddenChats') || '[]');

        if (isHidden) {
            if (!hiddenChats.includes(currentChat.partnerUsername)) {
                hiddenChats.push(currentChat.partnerUsername);
            }
        } else {
            const index = hiddenChats.indexOf(currentChat.partnerUsername);
            if (index > -1) {
                hiddenChats.splice(index, 1);
            }
        }

        localStorage.setItem('hiddenChats', JSON.stringify(hiddenChats));
        console.log('Hidden chats updated:', hiddenChats);

        const $friendItem = $(`.friend-item[data-username="${currentChat.partnerUsername}"]`);

        if (isHidden) {
            $friendItem.fadeOut(300);
        } else {
            $friendItem.fadeIn(300);
        }
    });

    // ========== BLOCK USER ==========
    $('#info-action-block').on('click', function (e) {
        e.preventDefault();
        const partnerName = $('#info-sidebar-displayname').text();
        if (confirm(`Bạn có chắc muốn chặn "${partnerName}"?`)) {
            alert('Chức năng chặn đang được phát triển.');
        }
    });

    // ========== REPORT USER ==========
    $('#info-action-report').on('click', function (e) {
        e.preventDefault();
        const partnerName = $('#info-sidebar-displayname').text();
        const reason = prompt(`Lý do báo cáo "${partnerName}":`);
        if (reason && reason.trim()) {
            alert('Cảm ơn bạn đã báo cáo. Chúng tôi sẽ xem xét!');
        }
    });

    // ========== IMAGE LIGHTBOX ==========
    window.openImageLightbox = function (imageUrl) {
        if ($('#imageLightbox').length === 0) {
            const lightboxHtml = `
                <div id="imageLightbox" class="image-lightbox">
                    <span class="image-lightbox-close">&times;</span>
                    <img src="" />
                </div>`;
            $('body').append(lightboxHtml);

            $('#imageLightbox').on('click', function (e) {
                if (e.target === this || $(e.target).hasClass('image-lightbox-close')) {
                    $(this).removeClass('active');
                }
            });
        }

        $('#imageLightbox img').attr('src', imageUrl);
        $('#imageLightbox').addClass('active');
    };

    // ========== SEND MESSAGE ==========
    function sendTextMessage(e) {
        if (e) e.preventDefault();
        const messageContent = $('#messageInput').val().trim();
        if (messageContent === '') return;

        const now = new Date().toISOString(); // Dùng ISO string để timestamp nhất quán
        renderMessage({
            senderUsername: currentUsername,
            content: JSON.stringify({ type: 'text', content: messageContent }),
            timestamp: now,
            isSelf: true
        });

        if (currentChat.mode === 'ai') {
            chatHub.server.sendMessageToAI(messageContent);
            // Hiển thị typing indicator cho AI (dùng default avatar nếu không có ai-avatar.png)
            showTypingIndicator('AI Assistant', '/Content/default-avatar.png');
        } else if (currentChat.mode === 'private') {
            const msgJson = JSON.stringify({ type: 'text', content: messageContent });
            chatHub.server.sendPrivateMessage(currentChat.partnerUsername, msgJson);
        } else if (currentChat.mode === 'group') {
            const msgJson = JSON.stringify({ type: 'text', content: messageContent });
            chatHub.server.sendGroupMessage(currentChat.groupId, msgJson);
        }

        $('#messageInput').val('').focus();
    }

    $('body').on('click', '#sendButton', sendTextMessage);
    $('body').on('keypress', '#messageInput', function (e) {
        if (e.which === 13 && !e.shiftKey) {
            e.preventDefault();
            sendTextMessage();
        }
    });

    // ========== TYPING INDICATOR DETECTION ==========
    $('body').on('input', '#messageInput', function () {
        sendTypingSignal();
    });

    // Stop typing khi gửi tin nhắn
    function originalSendTextMessage() {
        // Clear typing indicator khi gửi
        if (isTyping && currentChat.mode === 'private' && currentChat.partnerUsername) {
            clearTimeout(typingTimer);
            isTyping = false;
            if (chatHub.server.userStoppedTyping) {
                chatHub.server.userStoppedTyping(currentChat.partnerUsername);
            }
        }
    }

    // Wrap sendTextMessage
    const _sendTextMessage = sendTextMessage;
    sendTextMessage = function (e) {
        originalSendTextMessage();
        _sendTextMessage(e);
    };

    // ========== SWITCH CHAT ==========
    function switchChat(target) {
        // Clear typing indicator khi đổi chat
        hideTypingIndicator();
        if (isTyping && currentChat.mode === 'private' && currentChat.partnerUsername) {
            clearTimeout(typingTimer);
            isTyping = false;
            if (chatHub.server.userStoppedTyping) {
                chatHub.server.userStoppedTyping(currentChat.partnerUsername);
            }
        }

        $('#messagesList').empty();
        currentChat.mode = $(target).data('chat-mode');
        $('.conversation-list .list-group-item-action').removeClass('active');
        $(target).addClass('active');

        const conversationId = getConversationId();
        const savedBg = chatBackgrounds[conversationId];
        applyBackground(savedBg);

        if (currentChat.mode === 'ai') {
            $('#ai-chat-header').show();
            $('#private-chat-header').hide();
            $('#messageInput').attr('placeholder', 'Hỏi tôi bất cứ điều gì...?');
            $('#ai-welcome-screen').show();
            $('.message-area').hide();
            currentChat.partnerUsername = null;
            currentChat.groupId = null;

        } else if (currentChat.mode === 'private') {
            $('#private-chat-header').show();
            $('#ai-chat-header').hide();
            $('#messageInput').attr('placeholder', 'Nhập tin nhắn...');
            $('#user-chat-header').show();
            $('#user-chat-buttons').show();
            $('#ai-welcome-screen').hide();
            $('.message-area').show();

            currentChat.partnerUsername = $(target).data('username');
            const displayName = $(target).find('strong').text().trim();
            const avatarSrc = $(target).data('avatar-url') || '/Content/default-avatar.png';

            const conversationId = getConversationId();
            const partnerNickname = getNickname(currentChat.partnerUsername, conversationId);
            const displayNameToShow = partnerNickname || displayName;

            $('#chat-header-displayname').text(displayNameToShow);
            $('#chat-header-avatar').attr('src', avatarSrc);

            const isOnline = isUserOnline(currentChat.partnerUsername);
            const statusText = getLastSeenText(currentChat.partnerUsername);
            $('#chat-header-status').text(statusText).toggleClass('online', isOnline);

            loadChatHistory(currentChat.partnerUsername);
        }
    }

    function loadChatHistory(partnerUsername) {
        $.getJSON(urls.getChatHistory, { partnerUsername: currentChat.partnerUsername }, function (response) {
            if (response.success) {
                $('#messagesList').empty();
                response.messages.forEach(msg => {
                    const isSelf = msg.SenderUsername === currentUsername;

                    let vietTime;
                    try {
                        console.log('Original Timestamp:', msg.Timestamp); // DEBUG
                        const msgDate = parseTimestamp(msg.Timestamp);
                        console.log('Parsed Date:', msgDate, 'isValid:', msgDate !== null); // DEBUG

                        if (!msgDate) {
                            console.error('Failed to parse timestamp:', msg.Timestamp);
                            vietTime = 'Lỗi thời gian';
                        } else {
                            vietTime = msgDate.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
                        }
                    } catch (e) {
                        console.error('Timestamp parsing error:', e, msg.Timestamp);
                        vietTime = 'Lỗi thời gian';
                    }

                    let messageContent = "";
                    let bubbleContentHtml = "";

                    try {
                        const contentObj = JSON.parse(msg.Content);
                        messageContent = contentObj.content;

                        if (contentObj.type === "image") {
                            bubbleContentHtml = `<img src="${messageContent}" class="img-fluid rounded" style="max-width: 250px; cursor: pointer;" onclick="window.open(this.src, '_blank');" />`;
                        } else if (contentObj.type === "video") {
                            bubbleContentHtml = `<video controls src="${messageContent}" style="max-width: 300px; border-radius: 10px;"></video>`;
                        } else if (contentObj.type === "file") {
                            bubbleContentHtml = `
                        <a href="${contentObj.content}" target="_blank" style="display:flex; align-items:center; padding:8px 12px; background:#f0f0f0; border-radius:8px; text-decoration:none; color:#333;">
                            <i class="fas fa-file-alt" style="font-size:1.5rem; margin-right:10px; color:#007bff;"></i>
                            <div>
                                <div style="font-weight:600; font-size:0.9rem;">${contentObj.fileName || 'File'}</div>
                                <div style="font-size:0.75rem; color:#666;">${contentObj.fileSize || ''}</div>
                            </div>
                        </a>`;
                        } else if (contentObj.type === "call_log") {
                            bubbleContentHtml = createCallLogHtml(contentObj);
                        } else {
                            const escapedContent = $('<div/>').text(messageContent).html();
                            bubbleContentHtml = `<span>${escapedContent}</span>`;
                        }
                    } catch (e) {
                        const escapedContent = $('<div/>').text(msg.Content).html();
                        bubbleContentHtml = `<span>${escapedContent}</span>`;
                    }

                    let avatarHtml = '';
                    if (!isSelf) {
                        if (msg.SenderAvatar && msg.SenderAvatar !== '/Content/default-avatar.png') {
                            avatarHtml = `<img src="${msg.SenderAvatar}" class="avatar" alt="${msg.SenderUsername}" />`;
                        } else {
                            const firstLetter = msg.SenderUsername.charAt(0).toUpperCase();
                            avatarHtml = `<div class="avatar" title="${msg.SenderUsername}">${firstLetter}</div>`;
                        }
                    }

                    const messageHtml = `
                <div class="chat-message ${isSelf ? 'self' : 'other'}" data-timestamp="${msg.Timestamp}">
                    ${avatarHtml}
                    <div class="message-container">
                        <div class="chat-bubble">
                            ${bubbleContentHtml}
                            <span class="bubble-time">${vietTime}</span>
                        </div>
                    </div>
                </div>`;

                    $('#messagesList').append(messageHtml);
                });
                $('#messagesList').scrollTop($('#messagesList')[0].scrollHeight);
            }
        });
    }

    $('.conversation-list').on('click', '.list-group-item-action', function (e) {
        e.preventDefault();
        switchChat(this);
    });

    $('#toggle-conversations-btn').on('click', function () {
        $('.conversation-list').toggle();
    });

    // ========== FILE UPLOAD - FIXED ==========
    $('#imageUploadInput, #fileUploadInput').on('change', function () {
        const files = this.files;
        const container = $('#imagePreviewContainer');
        container.empty();

        if (files.length > 0) {
            // Lưu files vào biến tạm TRƯỚC KHI clear input
            tempFilesToSend = Array.from(files);

            tempFilesToSend.forEach(file => {
                const reader = new FileReader();
                reader.onload = function (e) {
                    let preview;
                    if (file.type.startsWith('image/')) {
                        preview = `<img src="${e.target.result}" class="img-fluid rounded" style="width:120px;height:120px;object-fit:cover;" />`;
                    } else if (file.type.startsWith('video/')) {
                        preview = `<video src="${e.target.result}" controls style="width:120px;height:120px;"></video>`;
                    } else {
                        const ext = file.name.split('.').pop().toLowerCase();
                        let icon = '📄';
                        if (['pdf'].includes(ext)) icon = '📕';
                        else if (['doc', 'docx'].includes(ext)) icon = '📘';
                        else if (['xls', 'xlsx'].includes(ext)) icon = '📗';
                        else if (['zip', 'rar', '7z'].includes(ext)) icon = '📦';

                        preview = `<div style="width:120px;height:120px;border:1px solid #ccc;display:flex;flex-direction:column;align-items:center;justify-content:center;border-radius:8px;">
                            <div style="font-size:2rem;">${icon}</div>
                            <div style="font-size:0.7rem;text-align:center;margin-top:5px;">${file.name}</div>
                        </div>`;
                    }
                    container.append(preview);
                };
                reader.readAsDataURL(file);
            });
            $('#imagePreviewModal').modal('show');
        }
        $(this).val(null);
    });

    $('#sendImageButton').off('click').on('click', function () {
        // Dùng biến tạm thay vì lấy từ input (vì input đã bị clear)
        if (!tempFilesToSend || tempFilesToSend.length === 0) {
            alert('Vui lòng chọn file để gửi.');
            return;
        }

        let filesToUpload = tempFilesToSend;

        const formData = new FormData();
        for (let i = 0; i < filesToUpload.length; i++) {
            formData.append('file' + i, filesToUpload[i]); 
        }

        $('#sendImageButton').prop('disabled', true).text('Đang gửi...');

        $.ajax({
            url: '/Upload/Multiple',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {
                console.log('✅ Upload response:', response); 

                if (response.success && response.files) {
                    response.files.forEach(fileData => {
                        const now = new Date();
                        const vietTime = now.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });

                        let bubbleContentHtml = "";

                        if (fileData.type === "image") {
                            bubbleContentHtml = `<img src="${fileData.filePath}" class="img-fluid rounded" style="max-width: 250px; cursor: pointer;" onclick="window.open(this.src, '_blank');" />`;
                        } else if (fileData.type === "video") {
                            bubbleContentHtml = `<video controls src="${fileData.filePath}" style="max-width: 300px; border-radius: 10px;"></video>`;
                        } else if (fileData.type === "file") {
                            bubbleContentHtml = `
                            <a href="${fileData.filePath}" target="_blank" style="display:flex; align-items:center; padding:8px 12px; background:#f0f0f0; border-radius:8px; text-decoration:none; color:#333;">
                                <i class="fas fa-file-alt" style="font-size:1.5rem; margin-right:10px; color:#007bff;"></i>
                                <div>
                                    <div style="font-weight:600; font-size:0.9rem;">${fileData.fileName}</div>
                                    <div style="font-size:0.75rem; color:#666;">${fileData.fileSize}</div>
                                </div>
                            </a>`;
                        }

                        const selfMessageHtml = `
                        <div class="chat-message self" data-timestamp="${now.toISOString()}">
                            <div class="message-container">
                                <div class="chat-bubble">
                                    ${bubbleContentHtml}
                                    <span class="bubble-time">${vietTime}</span>
                                </div>
                            </div>
                        </div>`;

                        const messagesList = $('#messagesList');
                        messagesList.append(selfMessageHtml);
                        messagesList.scrollTop(messagesList[0].scrollHeight);

                        const msgJson = JSON.stringify({
                            type: fileData.type,
                            content: fileData.filePath,
                            fileName: fileData.fileName || '',
                            fileSize: fileData.fileSize || ''
                        });

                        if (window.currentChat.mode === 'private') {
                            window.chatHub.server.sendPrivateMessage(window.currentChat.partnerUsername, msgJson);
                        } else if (window.currentChat.mode === 'ai') {
                            window.chatHub.server.sendMessageToAI(`[${fileData.type}] ${fileData.filePath}`);
                        }
                    });

                    $('#imagePreviewModal').modal('hide');
                    $('#imageUploadInput').val('');
                    $('#fileUploadInput').val('');

                    if (window.currentChat.mode === 'ai') {
                        $('#ai-welcome-screen').hide();
                        $('.message-area').show();
                    }
                } else {
                    alert('Lỗi upload: ' + (response.message || 'Không rõ nguyên nhân'));
                }
            },
            error: function (xhr, status, error) {
                console.error('❌ Upload error:', xhr.responseText);
                alert('Lỗi kết nối: ' + error + '\n\nChi tiết: ' + xhr.responseText);
            },
            complete: function () {
                $('#sendImageButton').prop('disabled', false).text('Gửi');
                tempFilesToSend = null; // Clear files tạm sau khi gửi xong
            }
        });
    });

    // Clear files tạm khi đóng modal mà không gửi
    $('#imagePreviewModal').on('hidden.bs.modal', function () {
        if (tempFilesToSend !== null) {
            tempFilesToSend = null;
            $('#imagePreviewContainer').empty();
        }
    });

    $('#quick-image-btn').on('click', function () {
        $('#imageUploadInput').click();
    });

    $('#toggle-attach-menu').on('click', function (e) {
        e.stopPropagation();

        if ($('#attachment-menu').length === 0) {
            const menuHtml = `
                <div id="attachment-menu" style="display:none; position:fixed; background:white; border:1px solid #ddd; border-radius:8px; box-shadow:0 4px 12px rgba(0,0,0,0.15); padding:8px 0; z-index:1000; min-width:180px;">
                    <a href="#" id="send-image-btn" style="display:block; padding:10px 16px; color:#333; text-decoration:none;">
                        <i class="fas fa-image" style="width:20px; margin-right:8px; color:#007bff;"></i> Gửi ảnh
                    </a>
                    <a href="#" id="send-video-btn" style="display:block; padding:10px 16px; color:#333; text-decoration:none;">
                        <i class="fas fa-video" style="width:20px; margin-right:8px; color:#dc3545;"></i> Gửi video
                    </a>
                    <a href="#" id="send-file-btn" style="display:block; padding:10px 16px; color:#333; text-decoration:none;">
                        <i class="fas fa-file-alt" style="width:20px; margin-right:8px; color:#28a745;"></i> Gửi file
                    </a>
                </div>`;
            $('body').append(menuHtml);

            $('#attachment-menu a').hover(
                function () { $(this).css('background', '#f8f9fa'); },
                function () { $(this).css('background', 'transparent'); }
            );
        }

        const btnRect = $(this)[0].getBoundingClientRect();
        $('#attachment-menu').css({
            display: 'block',
            bottom: (window.innerHeight - btnRect.top) + 'px',
            left: (btnRect.left) + 'px'
        });
    });

    $(document).on('click', function (e) {
        if (!$(e.target).closest('#toggle-attach-menu, #attachment-menu').length) {
            $('#attachment-menu').hide();
        }
    });

    $('body').on('click', '#send-image-btn', function (e) {
        e.preventDefault();
        $('#attachment-menu').hide();
        $('#imageUploadInput').attr('accept', 'image/*').click();
    });

    $('body').on('click', '#send-video-btn', function (e) {
        e.preventDefault();
        $('#attachment-menu').hide();
        $('#imageUploadInput').attr('accept', 'video/*').click();
    });

    $('body').on('click', '#send-file-btn', function (e) {
        e.preventDefault();
        $('#attachment-menu').hide();
        $('#fileUploadInput').click();
    });

    // ========== EMOJI PICKER ==========
    (function () {
        const $emojiBtn = $('#emoji-button');
        const $messageInput = $('#messageInput');

        if ($emojiBtn.length && $messageInput.length) {
            const emojis = ['😀', '😃', '😄', '😁', '😆', '😅', '🤣', '😂', '😊', '😇', '🙂', '🙃', '😉', '😌', '😍', '🥰', '😘', '😗', '😙', '😚', '❤️', '💕', '💖', '💗', '👍', '👎', '👏', '🙏', '💪', '🎉', '🎊', '🎁', '🔥', '⭐', '✨', '💯', '✅', '❌'];

            $emojiBtn.on('click', function (e) {
                e.stopPropagation();

                if ($('#simple-emoji-picker').length === 0) {
                    const pickerHtml = `
                        <div id="simple-emoji-picker" style="display:none; position:fixed; background:white; border:1px solid #ddd; border-radius:12px; box-shadow:0 4px 16px rgba(0,0,0,0.2); padding:12px; z-index:1000; max-width:300px;">
                            <div style="display:grid; grid-template-columns:repeat(8, 1fr); gap:8px;">
                                ${emojis.map(emoji => `<button class="emoji-btn" style="border:none; background:transparent; font-size:1.5rem; cursor:pointer; padding:4px; border-radius:4px;">${emoji}</button>`).join('')}
                            </div>
                        </div>`;
                    $('body').append(pickerHtml);

                    $(document).on('mouseenter', '.emoji-btn', function () {
                        $(this).css('background', '#f0f0f0');
                    }).on('mouseleave', '.emoji-btn', function () {
                        $(this).css('background', 'transparent');
                    });

                    $(document).on('click', '.emoji-btn', function () {
                        $messageInput.val($messageInput.val() + $(this).text());
                        $('#simple-emoji-picker').hide();
                        $messageInput.focus();
                    });
                }

                const btnRect = $emojiBtn[0].getBoundingClientRect();
                $('#simple-emoji-picker').css({
                    bottom: (window.innerHeight - btnRect.top + 10) + 'px',
                    left: (btnRect.left) + 'px'
                }).toggle();
            });

            $(document).on('click', function (e) {
                if (!$(e.target).closest('#emoji-button, #simple-emoji-picker').length) {
                    $('#simple-emoji-picker').hide();
                }
            });
        }
    })();
    $('body').on('click', '#user-chat-header', function () {
        if (window.currentChat.mode === 'private' && window.currentChat.partnerUsername) {
            const partnerUsername = window.currentChat.partnerUsername;

            // Hiển thị loading
            $('#partner-modal-display-name').text('Đang tải...');
            $('#partner-modal-username').text('');
            $('#partner-modal-avatar').attr('src', '/Content/default-avatar.png');
            $('#partner-modal-gender').text('Đang tải...');
            $('#partner-modal-dob').text('Đang tải...');
            $('#partner-modal-phone').text('Đang tải...');
            $('#partner-modal-email').text('Đang tải...');
            $('#partner-modal-bio').text('Đang tải...');
            $('#partner-unfriend-form').hide();

            // Hiển thị modal
            $('#partnerProfileModal').modal('show');

            // Gọi API
            $.getJSON(`/Profile/GetUserPublicProfile?username=${partnerUsername}`, function (response) {
                if (response.success && response.user) {
                    const user = response.user;

                    $('#partner-modal-display-name').text(user.DisplayName || 'Không có tên');
                    $('#partner-modal-username').text(`@${user.Username}`);

                    const avatarUrl = user.AvatarUrl || '/Content/default-avatar.png';
                    $('#partner-modal-avatar').attr('src', avatarUrl);

                    if (user.CoverUrl) {
                        $('#partner-modal-cover').css('background-image', `url(${user.CoverUrl})`);
                    }

                    $('#partner-modal-gender').text(user.Gender || 'Chưa cập nhật');
                    $('#partner-modal-phone').text(user.PhoneNumber || 'Chưa cập nhật');
                    $('#partner-modal-email').text(user.Email || 'Chưa cập nhật');
                    $('#partner-modal-bio').text(user.Bio || 'Không có tiểu sử.');

                    if (user.DateOfBirth) {
                        try {
                            const dob = new Date(user.DateOfBirth);
                            const day = dob.getDate();
                            const month = dob.getMonth() + 1;
                            const year = dob.getFullYear();
                            $('#partner-modal-dob').text(`${day} tháng ${month < 10 ? '0' + month : month}, ${year}`);
                        } catch (e) {
                            $('#partner-modal-dob').text('Chưa cập nhật');
                        }
                    } else {
                        $('#partner-modal-dob').text('Chưa cập nhật');
                    }

                    if (user.FriendshipId) {
                        $('#partner-unfriend-id').val(user.FriendshipId);
                        $('#partner-unfriend-form').show();
                    }
                } else {
                    $('#partner-modal-display-name').text(response.message || 'Không tìm thấy người dùng');
                    setTimeout(() => $('#partnerProfileModal').modal('hide'), 2000);
                }
            }).fail(function () {
                $('#partner-modal-display-name').text('Lỗi kết nối máy chủ');
                setTimeout(() => $('#partnerProfileModal').modal('hide'), 2000);
            });
        }
    });
    // ========== AI PROMPTS ==========
    $('body').on('click', '.ai-prompt-btn', function () {
        const promptText = $(this).data('prompt');
        $('#messageInput').val(promptText);
        sendTextMessage(null);
    });

    $('#ai-back-btn').on('click', function () {
        $('.conversation-list').toggle();
    });

    $('#ai-info-btn').on('click', function () {
        alert('Info về Meta AI: Powered by Llama 4!');
    });

    // ========== LOAD FRIENDS ==========
    function loadFriendsList() {
        const container = $('#conversation-list-ul');
        container.find('.friend-item').remove();

        $.getJSON(urls.getFriendsList, function (friends) {
            if (!friends || friends.length === 0) return;

            friends.forEach(function (friend) {
                if (friend.Username === currentUsername) return;

                const isOnline = onlineUsers.has(friend.Username);
                const statusIndicator = `<span class="status-indicator ${isOnline ? 'online' : 'offline'}" data-username="${friend.Username}"></span>`;

                const friendHtml = `
                    <a href="#" class="list-group-item list-group-item-action friend-item no-silhouette-icon"
                        data-chat-mode="private"
                        data-username="${friend.Username}"
                        data-userid="${friend.Id}"
                        data-avatar-url="${friend.AvatarUrl || ''}">
                        <strong>
                            <div class="chat-header-avatar-wrapper">
                                <img src="${friend.AvatarUrl || '/Content/default-avatar.png'}"
                                     class="chat-header-avatar no-default-icon" 
                                     style="width: 40px; height: 40px;" 
                                     alt="${friend.DisplayName}" />
                                ${statusIndicator} <!-- Chỉ dot online/offline, không phải silhouette -->
                            </div>
                            ${friend.DisplayName}
                        </strong>
                    </a>`;
                container.append(friendHtml);
            });

            const lastPartner = localStorage.getItem('lastChatPartner');
            let target = lastPartner ? container.find(`.list-group-item-action[data-username='${lastPartner}']`) : null;
            if (!target || target.length === 0) {
                target = $('#ai-chat-btn');
            }
            switchChat(target);
        });
    }

    loadNicknames();
    loadBackgrounds();

    // Manually add typing methods if SignalR proxy doesn't auto-generate
    // This happens when methods are added after initial hub creation
    if (typeof chatHub.server.userTyping === 'undefined') {
        console.log("⚠️ Creating userTyping method manually");

    }

    if (typeof chatHub.server.userStoppedTyping === 'undefined') {
        console.log("⚠️ Creating userStoppedTyping method manually");
        chatHub.server.userStoppedTyping = function (partnerUsername) {
            return $.connection.hub.invoke('ChatHub', 'UserStoppedTyping', partnerUsername);
        };
    }
    if (typeof chatHub.server.userStoppedTyping === 'undefined') {
        console.log("⚠️ Creating userStoppedTyping method manually");
        Object.defineProperty(chatHub.server, 'userStoppedTyping', {
            value: function (partnerUsername) {
                var args = [].slice.call(arguments, 0);
                return $.connection.hub.proxies['chatHub'].invoke.apply($.connection.hub.proxies['chatHub'], ['UserStoppedTyping'].concat(args));
            },
            enumerable: true
        });
    }

    $.connection.hub.url = window.location.origin + "/signalr";

    $.connection.hub.start()
        .done(function () {
            console.log("✅ SignalR connected successfully");
            console.log("📋 Available SignalR methods:", Object.keys(chatHub.server));

            // Join private group cho mỗi friend
            loadFriendsList();

            if (chatHub.server.getOnlineUsers) {
                chatHub.server.getOnlineUsers();
            }
        })
        .fail(function (err) {
            console.error("❌ SignalR connection failed:", err);
        });

    setInterval(function () {
        if ($.connection.hub.state === $.signalR.connectionState.connected) {
            chatHub.server.ping();
        }
    }, 30000);
});