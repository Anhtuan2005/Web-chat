$(function () {
    const config = window.chatConfig || {};
    const currentUsername = config.currentUsername || '';
    const urls = config.urls || {};
    let antiForgeryToken = config.antiForgeryToken || '';
    antiForgeryToken = $('#logoutForm input[name="__RequestVerificationToken"]').val() || antiForgeryToken;

    window.chatHub = $.connection.chatHub;
    const chatHub = window.chatHub;

    chatHub.client.updateAvatar = function (newAvatarUrl) {
        const avatarImg = $('#layout-user-avatar-img');
        if (avatarImg.length) {
            avatarImg.attr('src', newAvatarUrl);
        }
        const modalAvatar = $('#modal-avatar');
        if (modalAvatar.length) {
            modalAvatar.attr('src', newAvatarUrl);
        }
    };

    chatHub.client.userTyping = function (username) {
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

    chatHub.client.showError = function (message) {
        alert(message);
    };

    chatHub.client.leftGroupSuccess = function (groupId) {
        console.log('Client received leftGroupSuccess for groupId:', groupId);
        const $groupItem = $(`.group-item[data-id="${groupId}"]`);
        if ($groupItem.length > 0) {
            $groupItem.css('border', '2px solid red'); // Visual debug
            $groupItem.fadeOut(400, function () {
                $(this).remove();
                // Ensure conversation list is reloaded from server to reflect the change
                loadConversations('all'); 
            });
        } else {
            console.error('leftGroupSuccess: Could not find .group-item with data-id=' + groupId + '. Forcing conversation list reload.');
            // If the item wasn't found, still reload the conversations to be safe
            loadConversations('all'); 
        }
        alert('Bạn đã rời khỏi nhóm.');
        if (currentChat.mode === 'group' && currentChat.groupId === groupId) {
            $('#ai-chat-btn').click();
        }
    };

    chatHub.client.groupDisbanded = function (groupId) {
        console.log('Client received groupDisbanded for groupId:', groupId);
        const $groupItem = $(`.group-item[data-id="${groupId}"]`);
        if ($groupItem.length > 0) {
            $groupItem.css('border', '2px solid red'); // Visual debug
            $groupItem.fadeOut(400, function () {
                $(this).remove();
            });
        } else {
            console.error('groupDisbanded: Could not find .group-item with data-id=' + groupId);
        }
        alert('Một nhóm bạn tham gia đã được giải tán.');
        if (currentChat.mode === 'group' && currentChat.groupId === groupId) {
            $('#ai-chat-btn').click();
        }
    };

    chatHub.client.userLeftGroup = function (groupId, username, message) {
        if (currentChat.mode === 'group' && currentChat.groupId === groupId) {
            // Placeholder for showing a system message in the chat window
        }
        console.log(message); // Log for now, can be a toast notification
    };

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
    let currentReplyInfo = null; // Holds info about the message being replied to
    let emojiPicker = null;
    let messageToForwardId = null;
    let typingTimer = null;
    let isTyping = false;
    const TYPING_TIMEOUT = 3000; // 3 giây không gõ sẽ tắt typing indicator

    // Voice Recording variables
    let mediaRecorder;
    let audioChunks = [];
    let isRecording = false;
    let recordingInterval;
    let recordingStartTime;


    chatHub.client.onMessageReacted = function (messageId, userId, username, emoji, isRemoving) {
        const $message = $(`.chat-message[data-message-id="${messageId}"]`);
        if (!$message.length) return;

        let $reactionsContainer = $message.find('.reactions-container');

        if (!$reactionsContainer.length && !isRemoving) {
            $reactionsContainer = $('<div class="reactions-container"></div>');
            $message.find('.message-container').append($reactionsContainer);
        }

        const reactionId = `reaction-${messageId}-${emoji.replace(/[^a-zA-Z0-9]/g, '')}`;
        let $existingReaction = $reactionsContainer.find(`[data-reaction-id="${reactionId}"]`);

        if (isRemoving) {
            // Remove user from this reaction
            const currentUsers = ($existingReaction.data('users') || '').split(',').filter(u => u);
            const newUsers = currentUsers.filter(u => u !== username);

            if (newUsers.length === 0) {
                $existingReaction.fadeOut(200, function () {
                    $(this).remove();
                    if ($reactionsContainer.children().length === 0) {
                        $reactionsContainer.remove();
                    }
                });
            } else {
                $existingReaction.data('users', newUsers.join(','));
                $existingReaction.find('.reaction-count').text(newUsers.length);

                // Remove user-reacted class if current user removed their reaction
                if (username === currentUsername) {
                    $existingReaction.removeClass('user-reacted');
                }
            }
        } else {
            // Add reaction
            if ($existingReaction.length === 0) {
                const reactionHtml = `
                    <div class="reaction-item ${username === currentUsername ? 'user-reacted' : ''}" 
                        data-reaction-id="${reactionId}"
                        data-users="${username}"
                        title="${username}">
                        <span class="reaction-emoji">${emoji}</span>
                        <span class="reaction-count">1</span>
                    </div>
                `;
                $reactionsContainer.append(reactionHtml);
            } else {
                const currentUsers = ($existingReaction.data('users') || '').split(',').filter(u => u);

                if (!currentUsers.includes(username)) {
                    currentUsers.push(username);
                    $existingReaction.data('users', currentUsers.join(','));
                    $existingReaction.find('.reaction-count').text(currentUsers.length);
                    $existingReaction.attr('title', currentUsers.join(', '));

                    if (username === currentUsername) {
                        $existingReaction.addClass('user-reacted');
                    }
                }
            }
        }
    };

    function loadFriendList() {
        console.log('🔄 Loading friend list...');

        $.ajax({
            url: '/Friend/GetFriends', // ← API đúng
            type: 'GET',
            dataType: 'json',
            success: function (response) {
                console.log('✅ Friend list loaded:', response);

                if (response.success && response.friends) {
                    const $conversationList = $('#conversation-list-ul');
                    $conversationList.find('.list-group-item:not(#ai-chat-btn)').remove();

                    const hiddenChats = JSON.parse(localStorage.getItem('hiddenChats') || '[]');

                    response.friends.forEach(friend => {
                        if (hiddenChats.includes(friend.Username)) return;

                        const avatarUrl = friend.AvatarUrl || '/Content/default-avatar.png';
                        const displayName = friend.DisplayName || friend.Username;
                        const isOnline = isUserOnline(friend.Username);
                        const statusClass = isOnline ? 'online' : 'offline';

                        const unreadBadge = friend.UnreadCount > 0
                            ? `<span class="unread-badge">${friend.UnreadCount}</span>`
                            : '';

                        const friendHtml = `
                    <a href="#" 
                       class="list-group-item list-group-item-action friend-item" 
                       data-chat-mode="private" 
                       data-username="${friend.Username}"
                       data-avatar-url="${avatarUrl}">
                        <div class="d-flex align-items-center justify-content-between w-100">
                            <div class="d-flex align-items-center">
                                <div class="conv-avatar-wrapper">
                                    <img src="${avatarUrl}" 
                                         alt="${displayName}" 
                                         class="conv-avatar-img"
                                         onerror="this.src='/Content/default-avatar.png';" />
                                    <span class="status-indicator ${statusClass}" 
                                          data-username="${friend.Username}"></span>
                                </div>
                                <div>
                                    <strong style="display: block; margin-bottom: 2px;">${displayName}</strong>
                                    <small style="color: #6c757d; font-size: 0.85rem;">
                                        ${friend.LastMessage || 'Chưa có tin nhắn'}
                                    </small>
                                </div>
                            </div>
                            ${unreadBadge}
                        </div>
                    </a>`;

                        $conversationList.append(friendHtml);
                    });

                    console.log(`✅ Loaded ${response.friends.length} friends`);
                }
            },
            error: function (xhr, status, error) {
                console.error('❌ Error loading friend list:', error);
            }
        });
    }

    function loadConversations(filter = 'all') {
        console.log(`🔄 Loading conversations with filter: ${filter}...`);

        function formatLastMessage(message) {
            try {
                const parsed = JSON.parse(message);
                if (parsed && typeof parsed === 'object' && parsed.type) {
                    switch (parsed.type) {
                        case 'text': return parsed.content;
                        case 'image': return '<i class="fas fa-image"></i> Hình ảnh';
                        case 'video': return '<i class="fas fa-video"></i> Video';
                        case 'file': return `<i class="fas fa-file-alt"></i> ${parsed.fileName || 'Tệp'}`;
                        case 'voice': return '<i class="fas fa-microphone"></i> Tin nhắn thoại';
                        case 'call_log':
                            if (parsed.status === 'missed') return '<i class="fas fa-phone-slash text-danger"></i> Cuộc gọi nhỡ';
                            if (parsed.status === 'completed') return `<i class="fas fa-phone-alt text-success"></i> Cuộc gọi ${parsed.callType === 'video' ? 'video' : 'thoại'}`;
                            return '<i class="fas fa-phone-alt"></i> Cuộc gọi';
                        default: return message;
                    }
                }
            } catch (e) {
                // Not a JSON string, or invalid JSON
            }
            return message;
        }

        $.ajax({
            url: urls.getConversations,
            type: 'GET',
            data: { filter: filter },
            dataType: 'json',
            cache: false, // Prevent browser from caching this GET request
            success: function (response) {
                console.log('✅ Conversations loaded:', response);

                if (response && response.length > 0) {
                    const $conversationList = $('#conversation-list-ul');
                    $conversationList.find('.list-group-item:not(#ai-chat-btn)').remove();

                    response.forEach(conv => {
                        const displayName = conv.DisplayName || conv.Name || conv.Username;
                        const isOnline = conv.Type === 'Private' ? isUserOnline(conv.Username) : false;
                        const statusClass = isOnline ? 'online' : 'offline';
                        const lastMessageText = formatLastMessage(conv.LastMessage || (conv.Type === 'Group' ? 'Chưa có tin nhắn nhóm' : 'Chưa có tin nhắn'));

                        const unreadBadge = conv.UnreadCount > 0
                            ? `<span class="unread-badge">${conv.UnreadCount}</span>`
                            : '';

                        const pinIcon = conv.IsPinned ? 'fa-thumbtack' : 'fa-thumbtack';
                        const pinText = conv.IsPinned ? 'Bỏ ghim' : 'Ghim';
                        
                        const isGroup = conv.Type === 'Group';

                        let avatarHtml;
                        // Check for composite avatar feature
                        if (isGroup && conv.MemberAvatarUrls && conv.MemberAvatarUrls.length > 0) {
                            const count = conv.MemberAvatarUrls.length;
                            const memberAvatarsHtml = conv.MemberAvatarUrls.map((url, index) =>
                                `<img src="${url}" class="member-avatar member-avatar-${index + 1}" onerror="this.src='/Content/default-avatar.png';" />`
                            ).join('');
                            avatarHtml = `<div class="composite-avatar count-${count}">${memberAvatarsHtml}</div>`;
                        } else {
                            const avatarUrl = conv.AvatarUrl || '/Content/default-avatar.png';
                            avatarHtml = `<img src="${avatarUrl}" alt="${displayName}" class="conv-avatar-img" onerror="this.src='/Content/default-avatar.png';" />`;
                        }

                        const deleteOrLeaveButton = isGroup
                            ? `<a href="#" class="conv-menu-item conv-leave-group-btn" data-id="${conv.Id}" data-name="${displayName}">
                                   <i class="fas fa-sign-out-alt"></i> Rời nhóm
                               </a>`
                            : `<a href="#" class="conv-menu-item conv-delete-btn" data-username="${conv.Username || ''}" data-type="${conv.Type}">
                                   <i class="fas fa-trash-alt"></i> Xóa hội thoại
                               </a>`;

                        const conversationHtml = `
                            <div class="list-group-item list-group-item-action ${isGroup ? 'group-item' : 'friend-item'}"
                               data-chat-mode="${conv.Type.toLowerCase()}"
                               data-id="${conv.Id}"
                               data-username="${conv.Username || ''}"
                               data-avatar-url="${conv.AvatarUrl}"
                               style="position: relative;">
                                <div class="conversation-content-wrapper">
                                    <div class="d-flex align-items-center">
                                        <div class="conv-avatar-wrapper">
                                            ${avatarHtml}
                                            ${!isGroup ? `<span class="status-indicator ${statusClass}" data-username="${conv.Username}"></span>` : ''}
                                        </div>
                                        <div>
                                            <strong style="display: block; margin-bottom: 2px;">${displayName}</strong>
                                            <small style="color: #6c757d; font-size: 0.85rem;">
                                                ${lastMessageText}
                                            </small>
                                        </div>
                                    </div>
                                    ${unreadBadge}
                                </div>
                                <button class="conversation-menu-btn"><i class="fas fa-ellipsis-h"></i></button>
                                <div class="conversation-menu">
                                    <a href="#" class="conv-menu-item conv-pin-btn" data-id="${conv.Id}" data-type="${conv.Type}">
                                        <i class="fas fa-thumbtack"></i> ${pinText}
                                    </a>
                                    <a href="#" class="conv-menu-item conv-mark-unread-btn" data-id="${conv.Id}">
                                        <i class="fas fa-envelope-open"></i> Đánh dấu chưa đọc
                                    </a>
                                    <a href="#" class="conv-menu-item conv-hide-btn" data-id="${conv.Id}">
                                        <i class="fas fa-eye-slash"></i> Ẩn trò chuyện
                                    </a>
                                     <div class="conv-menu-divider"></div>
                                    <a href="#" class="conv-menu-item conv-mute-btn" data-id="${conv.Id}">
                                        <i class="fas fa-bell-slash"></i> Tắt thông báo
                                    </a>
                                    ${!isGroup ? `
                                    <a href="#" class="conv-menu-item conv-report-btn" data-username="${conv.Username || ''}">
                                        <i class="fas fa-flag"></i> Báo xấu
                                    </a>` : ''}
                                    <div class="conv-menu-divider"></div>
                                    ${deleteOrLeaveButton}
                                </div>
                            </div>`;
                        $conversationList.append(conversationHtml);
                    });
                    console.log(`✅ Loaded ${response.length} conversations`);
                } else {
                    const $conversationList = $('#conversation-list-ul');
                    $conversationList.find('.list-group-item:not(#ai-chat-btn)').remove();
                    $conversationList.append('<li class="list-group-item text-center text-muted">Chưa có cuộc trò chuyện nào.</li>');
                }
            },
            error: function (xhr, status, error) {
                console.error('❌ Error loading conversations:', error);
                $('#conversation-list-ul').html('<li class="list-group-item text-center text-danger">Lỗi khi tải cuộc trò chuyện.</li>');
            }
        });
    }


    $('.filter-tab').on('click', function () {
        $('.filter-tab').removeClass('active');
        $(this).addClass('active');

        const filter = $(this).data('filter');
        loadConversations(filter);
    });



    console.log('✅ Enhanced message actions initialized');

    // ========== TYPING INDICATOR VARIABLES ========== 

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

    console.log("📋 Available SignalR methods:", Object.keys(chatHub.server));
    function sendTypingSignal() {
        if (currentChat.mode === 'private' && currentChat.partnerUsername) {
            console.log('🔄 Sending typing signal to:', currentChat.partnerUsername);

            if (!chatHub.server.userTyping) {
                console.error('❌ userTyping method not found!');
                console.log('Available methods:', Object.keys(chatHub.server));
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
                if (chatHub.server.userStoppedTyping) {
                    chatHub.server.userStoppedTyping(currentChat.partnerUsername);
                }
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

    function sendTypingSignal() {
        if (currentChat.mode === 'private' && currentChat.partnerUsername) {
            if (!isTyping) {
                isTyping = true;
                chatHub.server.userTyping(currentChat.partnerUsername);
            }
            clearTimeout(typingTimer);
            typingTimer = setTimeout(() => {
                isTyping = false;
                if (chatHub.server.userStoppedTyping) {
                    chatHub.server.userStoppedTyping(currentChat.partnerUsername);
                }
            }, TYPING_TIMEOUT);
        }
    }

    $('body').on('input', '#messageInput', function () {
        sendTypingSignal();
    });

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
            <i class="fas fa-phone-alt"></i>Gọi lại
        </button>`;
    }

    // ========== MESSAGE RENDERING - FIXED ========== 
    function renderMessage(msgData) {
        $('#ai-welcome-screen').hide();
        $('.message-area').show();

        // Handle deleted messages first
        if (msgData.isDeleted) {
            const $existingMsg = $(`#messagesList .chat-message[data-message-id="${msgData.messageId}"]`);
            const deletedHtml = `<div class="deleted-message" style="font-style: italic; color: white;"><i class="fas fa-ban"></i> Tin nhắn đã được thu hồi</div>`;

            if ($existingMsg.length) {
                // This is for real-time recall. Modify the existing bubble in-place.
                const $bubble = $existingMsg.find('.chat-bubble');
                $bubble.html(deletedHtml);
                $bubble.css({ 'background-color': 'transparent', 'border': '1px solid #f0f0f0' });
                $existingMsg.find('.message-options').remove();
                $existingMsg.find('.reactions-container').remove();
            } else {
                // This is for loading from history (F5 refresh). Create a new bubble
                // that looks the same as the in-place modified one.
                const isSelf = msgData.isSelf || (msgData.senderUsername && msgData.senderUsername === currentUsername);
                const recalledMessageHtml = `
                    <div class="chat-message ${isSelf ? 'self' : 'other'}" data-message-id="${msgData.messageId}">
                        <div class="message-container">
                            <div class="chat-bubble" style="background-color: transparent; border: 1px solid #f0f0f0;">
                                ${deletedHtml}
                            </div>
                        </div>
                    </div>
                `;
                $('#messagesList').append(recalledMessageHtml);
            }
            return;
        }

        const isSelf = msgData.isSelf || msgData.senderUsername === currentUsername;
        const msgDate = parseTimestamp(msgData.timestamp) || new Date();
        const vietTime = formatTimestamp(msgDate);
        const messageId = msgData.messageId || `temp_${Date.now()}`;

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
                        <div style="font-weight:600; font-size:0.9rem;">${contentObj.fileName}</div>
                        <div style="font-size:0.75rem; color:#666;">${contentObj.fileSize}</div>
                    </div>
                </a>`;
                break;
            case 'call_log':
                bubbleContentHtml = createCallLogHtml(contentObj);
                break;
            case 'voice':
                bubbleContentHtml = `<audio controls src="${contentObj.content}" style="width: 250px;"></audio>`;
                break;
            default:
                const escaped = $('<div/>').text(contentObj.content).html();
                bubbleContentHtml = `<span>${escaped}</span>`;
        }

        let replyHtml = '';
        if (msgData.parentMessage) {
            let parentContentPreview = '';
            try {
                const parentContentObj = JSON.parse(msgData.parentMessage.Content);
                parentContentPreview = parentContentObj.type === 'text' ? parentContentObj.content : `[${parentContentObj.type}]`;
            } catch {
                parentContentPreview = msgData.parentMessage.Content;
            }
            const parentSender = msgData.parentMessage.SenderUsername === currentUsername ? 'Bạn' : (getNickname(msgData.parentMessage.SenderUsername, conversationId) || msgData.parentMessage.SenderUsername);
            replyHtml = `
                <div class="reply-to-snippet">
                    <div class="reply-to-header"><i class="fas fa-reply"></i> Trả lời <strong>${$('<div/>').text(parentSender).html()}</strong></div>
                    <div class="reply-to-content">${$('<div/>').text(parentContentPreview).html()}</div>
                </div>`;
        }
        bubbleContentHtml = replyHtml + bubbleContentHtml;

        let forwardHtml = '';
        if (msgData.forwardedFrom) {
            forwardHtml = `<div class="forwarded-indicator"><i class="fas fa-share"></i> Tin nhắn được chuyển tiếp</div>`;
        }
        bubbleContentHtml = forwardHtml + bubbleContentHtml;

        let avatarHtml = '';
        if (!isSelf) {
            const avatarUrl = msgData.senderAvatar || $(`.friend-item[data-username="${msgData.senderUsername}"]`).data('avatar-url') || '/Content/default-avatar.png';
            avatarHtml = `<img src="${avatarUrl}" class="avatar" alt="${displayName}">`;
        }

        let nicknameHtml = !isSelf && currentChat.mode === 'group' ? `<div class="message-nickname">${displayName}</div>` : '';
        let statusHtml = isSelf && currentChat.mode === 'private' ? renderMessageStatus(msgData.status || 'Pending') : '';
        let editedHtml = msgData.editedAt ? `<span class="edited-indicator">(đã chỉnh sửa)</span>` : '';
        const canHaveActions = !msgData.isDeleted; // Actions can be shown on any message type now

        let reactionsHtml = '';
        if (msgData.reactions && msgData.reactions.length > 0) {
            const reactionsGrouped = msgData.reactions.reduce((acc, r) => {
                if (!acc[r.Emoji]) {
                    acc[r.Emoji] = { users: [], userIds: [] };
                }
                acc[r.Emoji].users.push(r.Username);
                acc[r.Emoji].userIds.push(r.UserId);
                return acc;
            }, {});

            let reactionsItemsHtml = '';
            for (const emoji in reactionsGrouped) {
                const group = reactionsGrouped[emoji];
                const userList = group.users.join(', ');
                const hasReacted = group.users.includes(currentUsername);
                const reactionId = `reaction-${messageId}-${emoji.replace(/[^a-zA-Z0-9]/g, '')}`;

                reactionsItemsHtml += `
                    <div class="reaction-item ${hasReacted ? 'user-reacted' : ''}" 
                         data-reaction-id="${reactionId}"
                         data-users="${userList}"
                         title="${userList}">
                        <span class="reaction-emoji">${emoji}</span>
                        <span class="reaction-count">${group.users.length}</span>
                    </div>
                `;
            }
            reactionsHtml = `<div class="reactions-container">${reactionsItemsHtml}</div>`;
        }

        const messageHtml = `
        <div class="chat-message ${isSelf ? 'self' : 'other'}" data-timestamp="${msgDate.toISOString()}" data-message-id="${messageId}">
            ${avatarHtml}
            <div class="message-container">
                ${nicknameHtml}
                <div class="chat-bubble">
                    ${bubbleContentHtml}
                    <div class="message-meta">
                        <span class="bubble-time">${vietTime}</span>
                        ${statusHtml}
                        ${editedHtml}
                    </div>
                </div>
                ${reactionsHtml}
                ${canHaveActions ? `
                <div class="message-options">
                    <button class="btn btn-sm btn-light message-options-btn" type="button">
                        <i class="fas fa-ellipsis-h"></i>
                    </button>
                    <div class="message-options-menu">
                        <a href="#" class="message-option-item react-message-btn">
                            <i class="far fa-smile"></i> Thả cảm xúc
                        </a>
                        <a href="#" class="message-option-item reply-message-btn">
                            <i class="fas fa-reply"></i> Trả lời
                        </a>
                        <a href="#" class="message-option-item forward-message-btn">
                            <i class="fas fa-share"></i> Chuyển tiếp
                        </a>
                        ${isSelf && contentObj.type === 'text' ? `
                        <a href="#" class="message-option-item edit-message-btn">
                            <i class="fas fa-pen"></i> Chỉnh sửa
                        </a>
                        ` : ''}
                        ${isSelf ? `
                        <a href="#" class="message-option-item delete-message-btn">
                            <i class="fas fa-trash"></i> Thu hồi
                        </a>
                        ` : ''}
                    </div>
                </div>` : ''}
            </div>
        </div>`;
        $('#messagesList').append(messageHtml);
        $('#messagesList').scrollTop($('#messagesList')[0].scrollHeight);
        return messageId;
    }

    function renderMessageStatus(status) {
        let statusText = 'Đang gửi...';
        let statusKey = 'Pending';

        if (status === 'Sent' || status === 0) {
            statusText = 'Đã gửi';
            statusKey = 'Sent';
        } else if (status === 'Delivered' || status === 1) {
            statusText = 'Đã nhận';
            statusKey = 'Delivered';
        } else if (status === 'Read' || status === 2) {
            statusText = 'Đã xem';
            statusKey = 'Read';
        }
        return `<span class="message-status status-${statusKey.toLowerCase()}" data-status="${statusKey}">${statusText}</span>`;
    }

    // ========== MESSAGE ACTIONS EVENT HANDLERS ========== 


    const QUICK_EMOJIS = ['❤️', '😂', '😮', '😢', '😡', '👍'];
    $(document).on('click', '.message-options-btn', function (e) {
        e.stopPropagation();
        const $menu = $(this).next('.message-options-menu');
        const $button = $(this);

        $('.message-options-menu').not($menu).removeClass('show fixed below').hide();
        $('.emoji-picker-popup').removeClass('show');

        $menu.toggleClass('show');

        if ($menu.hasClass('show')) {
            adjustMenuPosition($menu, $button);
            setTimeout(() => adjustMenuPosition($menu, $button), 10);
        } else {
            $menu.removeClass('fixed below').css({ position: 'absolute', top: '', bottom: '' });
        }
    });

    // Close menus when clicking anywhere else
    $(document).on('click', function (e) {
        if (!$(e.target).closest('.message-options, .emoji-picker-popup').length) {
            $('.message-options-menu').removeClass('show');
            $('.emoji-picker-popup').removeClass('show');
        }
    });

    // Handle delete button click
    $('body').on('click', '.delete-message-btn', function (e) {
        e.preventDefault();
        const $message = $(this).closest('.chat-message');
        const messageId = $message.data('message-id');
        const isSelf = $message.hasClass('self');

        let confirmText = isSelf
            ? 'Bạn có chắc muốn thu hồi tin nhắn này? Tin nhắn sẽ bị xóa ở cả hai phía.'
            : 'Bạn có chắc muốn xóa tin nhắn này? Tin nhắn chỉ bị xóa ở phía bạn.';

        if (messageId && confirm(confirmText)) {
            const deleteForEveryone = isSelf; 

            chatHub.server.deleteMessage(messageId, deleteForEveryone)
                .done(function () {
                    if (!deleteForEveryone) {
                        $message.fadeOut(300, function () {
                            $(this).remove();
                        });
                    }
                })
                .fail(function (err) {
                    console.error('Error deleting message:', err);
                    alert('Không thể xóa tin nhắn.');
                });
        }

        $(this).closest('.message-options-menu').removeClass('show');
    });

    $('body').on('click', '.edit-message-btn', function (e) {
        e.preventDefault();
        const $message = $(this).closest('.chat-message');
        const $bubble = $message.find('.chat-bubble');
        const $replySnippet = $bubble.find('.reply-to-snippet');
        const $forwardedIndicator = $bubble.find('.forwarded-indicator');

        // Prevent editing non-text messages
        if ($bubble.find('img, video, a[target="_blank"]').length > 0) {
            alert('Chỉ có thể chỉnh sửa tin nhắn văn bản.');
            return;
        }

        // Get original text (skip reply snippet and forwarded indicator)
        let $contentSpan = $bubble.contents().filter(function () {
            return this.nodeType === Node.TEXT_NODE ||
                (this.nodeType === Node.ELEMENT_NODE &&
                    !$(this).is('.reply-to-snippet, .forwarded-indicator, .message-meta, .edited-indicator'));
        }).first();

        if ($contentSpan.length === 0) {
            $contentSpan = $bubble.find('span').not('.bubble-time, .message-status, .edited-indicator').first();
        }

        const originalText = $contentSpan.text().trim();

        // Save original HTML for cancel
        $bubble.data('original-html', $bubble.html());

        // Create edit interface
        const editHtml = `
        ${$replySnippet.length ? $replySnippet[0].outerHTML : ''}
        ${$forwardedIndicator.length ? $forwardedIndicator[0].outerHTML : ''}
        <div class="edit-container">
            <textarea class="form-control edit-textarea" rows="3">${originalText}</textarea>
            <div class="edit-actions">
                <button class="btn btn-sm btn-light cancel-edit-btn">
                    <i class="fas fa-times"></i> Hủy
                </button>
                <button class="btn btn-sm btn-primary save-edit-btn">
                    <i class="fas fa-check"></i> Lưu
                </button>
            </div>
        </div>
    `;

        $bubble.html(editHtml);
        $bubble.find('.edit-textarea').focus().select();

        $(this).closest('.message-options-menu').removeClass('show');
    });

    // Handle cancel edit
    $('body').on('click', '.cancel-edit-btn', function (e) {
        e.stopPropagation();
        const $bubble = $(this).closest('.chat-bubble');
        $bubble.html($bubble.data('original-html'));
    });

    $('body').on('click', '.save-edit-btn', function (e) {
        e.stopPropagation();
        const $bubble = $(this).closest('.chat-bubble');
        const $message = $bubble.closest('.chat-message');
        const messageId = $message.data('message-id');
        const newText = $bubble.find('.edit-textarea').val().trim();

        if (!newText) {
            alert('Tin nhắn không được để trống.');
            return;
        }

        if (messageId) {
            const newContentJson = JSON.stringify({ type: 'text', content: newText });

            chatHub.server.editMessage(messageId, newContentJson)
                .done(function () {
                    // Server will send onMessageEdited, so just restore temporarily
                    $bubble.html($bubble.data('original-html'));
                })
                .fail(function (err) {
                    console.error('Error editing message:', err);
                    alert('Không thể sửa tin nhắn.');
                    $bubble.html($bubble.data('original-html'));
                });
        }
    });


    $('body').on('click', '.react-message-btn', function (e) {
        e.preventDefault();
        e.stopPropagation(); // Stop propagation to prevent immediate closing by the document click handler

        const $button = $(this);
        const $message = $button.closest('.chat-message');
        const messageId = $message.data('message-id');

        // Close other popups
        $('.message-options-menu').removeClass('show');
        $('.emoji-picker-popup').not($message.find('.emoji-picker-popup')).removeClass('show');

        // Create or toggle the associated emoji picker
        let $picker = $message.find('.emoji-picker-popup');
        if ($picker.length === 0) {
            const pickerHtml = `
            <div class="emoji-picker-popup">
                ${QUICK_EMOJIS.map(emoji =>
                `<span class="emoji-option" data-emoji="${emoji}">${emoji}</span>`
            ).join('')}
            </div>
        `;
            $message.find('.message-container').append(pickerHtml);
            $picker = $message.find('.emoji-picker-popup');
        }

        $picker.toggleClass('show');
        $picker.data('message-id', messageId);

        // NEW: Adjust picker position if it's being shown
        if ($picker.hasClass('show')) {
            const $anchor = $message.find('.message-options-btn'); // Position relative to the main options button
            const pickerWidth = $picker.outerWidth();
            const pickerHeight = $picker.outerHeight();
            const anchorRect = $anchor[0].getBoundingClientRect();
            const windowHeight = $(window).height();

            // Default position is above the button, centered
            let top = anchorRect.top - pickerHeight - 8; // 8px spacing
            let left = anchorRect.left + (anchorRect.width / 2) - (pickerWidth / 2);

            // If it goes off the top of the screen, show it below
            if (top < 10) {
                top = anchorRect.bottom + 8;
            }

            // Basic horizontal boundary checks
            if (left < 10) left = 10;
            if (left + pickerWidth > $(window).width() - 10) {
                left = $(window).width() - pickerWidth - 10;
            }

            $picker.css({
                position: 'fixed',
                top: top + 'px',
                left: left + 'px',
                right: 'auto',
                bottom: 'auto',
                margin: 0,
                'z-index': 10000 
            });
        }
    });

    $('body').on('click', '.emoji-option', function (e) {
        e.stopPropagation();
        const emoji = $(this).data('emoji');
        const $picker = $(this).closest('.emoji-picker-popup');
        const messageId = $picker.data('message-id');

        if (messageId) {
            chatHub.server.reactToMessage(messageId, emoji, false)
                .fail(function (err) {
                    console.error('Error reacting to message:', err);
                });
        }

        $picker.removeClass('show');
    });

    $('body').on('click', '.reaction-item.user-reacted', function (e) {
        e.stopPropagation();
        const $message = $(this).closest('.chat-message');
        const messageId = $message.data('message-id');
        const emoji = $(this).find('.reaction-emoji').text();

        chatHub.server.reactToMessage(messageId, emoji, true) 
            .fail(function (err) {
                console.error('Error removing reaction:', err);
            });
    });

    $('body').on('click', '.reply-message-btn', function (e) {
        e.preventDefault();
        const $message = $(this).closest('.chat-message');
        const messageId = $message.data('message-id');
        const $bubble = $message.find('.chat-bubble');

        // Determine sender
        let sender;
        if ($message.hasClass('self')) {
            sender = currentUsername;
        } else if (currentChat.mode === 'private') {
            sender = currentChat.partnerUsername;
        } else {
            sender = $message.find('.message-nickname').text() || 'Unknown';
        }

        // Get content preview
        let content = '';
        const $contentSpan = $bubble.find('span').not('.bubble-time, .message-status, .edited-indicator').first();

        if ($contentSpan.length > 0 && $contentSpan.text().trim()) {
            content = $contentSpan.text().trim();
        } else if ($bubble.find('img').length > 0) {
            content = '📷Hình ảnh';
        } else if ($bubble.find('video').length > 0) {
            content = '🎥Video';
        } else if ($bubble.find('a[target="_blank"]').length > 0) {
            content = '📎Tệp đính kèm';
        } else {
            content = 'Tin nhắn';
        }

        currentReplyInfo = {
            messageId: messageId,
            sender: sender,
            content: content
        };

        showReplyBanner();
        $(this).closest('.message-options-menu').removeClass('show');
    });



    function showReplyBanner() {
        if (currentReplyInfo) {
            const conversationId = getConversationId();
            const senderNickname = getNickname(currentReplyInfo.sender, conversationId);
            const senderName = currentReplyInfo.sender === currentUsername
                ? 'Bạn'
                : (senderNickname || currentReplyInfo.sender);

            $('#reply-banner-text').html(`Đang trả lời <strong>${$('<div/>').text(senderName).html()}</strong>`);
            $('#reply-banner-preview').text(currentReplyInfo.content);
            $('#reply-banner').slideDown(200);
            $('#messageInput').focus();
        }
    }

    function hideReplyBanner() {
        currentReplyInfo = null;
        $('#reply-banner').slideUp(150);
    }

    $('#close-reply-banner').on('click', hideReplyBanner);

    $('body').on('click', '.forward-message-btn', function (e) {
        e.preventDefault();
        const $message = $(this).closest('.chat-message');
        messageToForwardId = $message.data('message-id');

        const $list = $('#forward-conversations-list');
        $list.html('<p class="text-center text-muted"><i class="fas fa-spinner fa-spin"></i> Đang tải danh sách...</p>');
        $('#forwardMessageModal').modal('show');

        // Fetch ALL conversations (private and group)
        $.getJSON(urls.getConversations, { filter: 'all' }, function (conversations) {
            $list.empty();
            if (conversations && conversations.length > 0) {
                const currentConversationId = getConversationId();

                conversations.forEach(conv => {
                    // Exclude the current conversation from the list
                    const conversationIdentifier = conv.Type === 'Group' ? `group_${conv.Id}` : `private_${currentUsername}_${conv.Username}`.split('_').sort().join('_');
                    if (conversationIdentifier === currentConversationId) {
                        return; // Skip current conversation
                    }

                    const displayName = conv.DisplayName || conv.Name;
                    const avatarUrl = conv.AvatarUrl || '/Content/default-avatar.png';
                    const isGroup = conv.Type === 'Group';

                    const conversationHtml = `
                    <div class="custom-control custom-checkbox p-2 border-bottom">
                        <input type="checkbox" 
                               class="custom-control-input" 
                               id="forward-conv-${conv.Id}" 
                               data-id="${isGroup ? conv.Id : conv.Username}"
                               data-type="${conv.Type}">
                        <label class="custom-control-label d-flex align-items-center" 
                               for="forward-conv-${conv.Id}">
                            <img src="${avatarUrl}" 
                                 style="width: 32px; height: 32px; border-radius: 50%; margin-right: 10px;"
                                 onerror="this.src='/Content/default-avatar.png';"
                                 />
                            <span>${displayName} ${isGroup ? '(Nhóm)' : ''}</span>
                        </label>
                    </div>`;
                    $list.append(conversationHtml);
                });
            } else {
                $list.html('<p class="text-center text-danger">Không có cuộc trò chuyện nào để chuyển tiếp.</p>');
            }
        }).fail(function () {
            $list.html('<p class="text-center text-danger">Lỗi kết nối.</p>');
        });

        $(this).closest('.message-options-menu').removeClass('show');
    });

    $('#confirm-forward-btn').on('click', function () {
        const selectedTargets = $('#forward-conversations-list input:checked').map(function () {
            return {
                Id: $(this).data('id'),
                Type: $(this).data('type')
            };
        }).get();

        if (selectedTargets.length === 0) {
            alert('Vui lòng chọn ít nhất một cuộc trò chuyện để chuyển tiếp.');
            return;
        }

        if (messageToForwardId) {
            // Serialize the targets array to a JSON string
            const targetsJson = JSON.stringify(selectedTargets);

            // Call the new hub method with the JSON string
            chatHub.server.forwardMessageToTargets(messageToForwardId, targetsJson)
                .done(function () {
                    $('#forwardMessageModal').modal('hide');
                    alert(`Đã chuyển tiếp đến ${selectedTargets.length} cuộc trò chuyện.`);
                })
                .fail(function (err) {
                    console.error('Error forwarding message:', err);
                    alert('Lỗi khi chuyển tiếp tin nhắn.');
                });
        }

        messageToForwardId = null;
    });

    $('#forward-search-input').on('keyup', function () {
        const searchTerm = $(this).val().toLowerCase();
        $('#forward-friends-list .custom-control').each(function () {
            const friendName = $(this).find('label span').text().toLowerCase();
            $(this).toggle(friendName.includes(searchTerm));
        });
    });


    // ========== SIGNALR HANDLERS ========== 
    chatHub.client.receiveMessage = function (senderUsername, senderAvatar, messageJson, timestamp, messageId, parentInfo, forwarderInfo) {
        renderMessage({
            senderUsername, senderAvatar, content: messageJson, timestamp, messageId,
            isSelf: false, parentMessage: parentInfo, forwardedFrom: forwarderInfo
        });
        playNotificationSound();
        if (currentChat.mode === 'private') {
            loadConversations('all');
        }
    };

    chatHub.client.receiveAIMessage = function (senderUsername, senderAvatar, messageJson, timestamp, messageId) {
        renderMessage({
            senderUsername: senderUsername,
            senderAvatar: senderAvatar,
            content: messageJson,
            timestamp: timestamp,
            messageId: messageId,
            isSelf: false
        });
        hideTypingIndicator();
    };

    console.log('✅ Friend list loader initialized');

    // ========== MESSAGE STATUS HANDLERS ========== 
    chatHub.client.messageSent = function (tempId, finalId, timestamp) {
        const $tempMessage = $(`.chat-message[data-message-id="${tempId}"]`);
        if ($tempMessage.length) {
            $tempMessage.attr('data-message-id', finalId);
            $tempMessage.attr('data-timestamp', timestamp);
            $tempMessage.find('.message-status').replaceWith(renderMessageStatus('Sent'));
        }
    };

    chatHub.client.messageDelivered = function (messageId, targetUsername) {
        if (currentChat.partnerUsername === targetUsername) {
            const $message = $(`.chat-message[data-message-id="${messageId}"]`);
            if ($message.length && $message.find('.message-status').data('status') === 'Sent') {
                $message.find('.message-status').replaceWith(renderMessageStatus('Delivered'));
            }
        }
    };

    chatHub.client.messagesMarkedAsRead = function (readerUsername) {
        if (currentChat.partnerUsername === readerUsername) {
            $('#messagesList .chat-message.self').each(function () {
                const $status = $(this).find('.message-status');
                if ($status.data('status') !== 'Read') {
                    $status.replaceWith(renderMessageStatus('Read'));
                }
            });
        }
        console.log(`📖 ${readerUsername} has read your messages.`);
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

    // ========== MESSAGE ACTIONS HANDLERS ========== 
    chatHub.client.onMessageDeleted = function (messageId, deletedForEveryone) {
        if (deletedForEveryone) {
            // Re-render the message with the 'isDeleted' flag to ensure a consistent look
            // with how recalled messages are displayed when loading chat history.
            renderMessage({
                messageId: messageId,
                isDeleted: true
            });
        } else {
            // This is for "delete for me" functionality, which just removes the message locally.
            const $message = $(`.chat-message[data-message-id="${messageId}"]`);
            if ($message.length) {
                $message.fadeOut(300, function () {
                    $(this).remove();
                });
            }
        }
    };

    chatHub.client.onMessageEdited = function (messageId, newContentJson, editedAt) {
        const $message = $(`.chat-message[data-message-id="${messageId}"]`);

        if ($message.length) {
            const $bubble = $message.find('.chat-bubble');
            const contentObj = JSON.parse(newContentJson);

            // Preserve reply snippet and forwarded indicator
            const $replySnippet = $bubble.find('.reply-to-snippet').clone();
            const $forwardedIndicator = $bubble.find('.forwarded-indicator').clone();
            const $messageMeta = $bubble.find('.message-meta').clone();

            // Update content
            const escaped = $('<div/>').text(contentObj.content).html();

            $bubble.empty();
            if ($replySnippet.length) $bubble.append($replySnippet);
            if ($forwardedIndicator.length) $bubble.append($forwardedIndicator);
            $bubble.append(`<span>${escaped}</span>`);
            $bubble.append($messageMeta);

            // Add/update edited indicator
            if ($bubble.find('.edited-indicator').length === 0) {
                $messageMeta.append('<span class="edited-indicator">(đã chỉnh sửa)</span>');
            }
        }
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
            const logMessageJson = JSON.stringify(logMessage);
            const tempId = `temp_call_${Date.now()}`;

            // Gửi lên server
            chatHub.server.sendPrivateMessage(currentCallPartner, logMessageJson, tempId, null);
            chatHub.server.endCall(currentCallPartner);

            // Hiển thị ngay cho chính mình
            renderMessage({
                senderUsername: currentUsername,
                content: logMessageJson,
                timestamp: new Date().toISOString(),
                isSelf: true,
                messageId: tempId,
                status: 'Pending'
            });
        } else if (currentCallPartner) {
            // Nếu không gửi log, vẫn thông báo cho người kia kết thúc
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

    // ========== INFO & SEARCH SIDEBARS ==========
    $('#toggle-info-sidebar-btn').on('click', function () {
        const $sidebar = $('#conversation-info-sidebar');
        const isVisible = $sidebar.is(':visible');

        $('#search-sidebar').hide(); // Hide search if open
        $('#toggle-search-sidebar-btn').removeClass('active');

        if (isVisible) {
            $sidebar.slideUp(300);
            $(this).removeClass('active');
        } else {
            $sidebar.slideDown(300);
            $(this).addClass('active');
            loadConversationInfo();
        }
    });

    $('#close-info-sidebar-btn').on('click', function () {
        $('#conversation-info-sidebar').slideUp(300);
        $('#toggle-info-sidebar-btn').removeClass('active');
    });

    $('#toggle-search-sidebar-btn').on('click', function () {
        const $sidebar = $('#search-sidebar');
        const isVisible = $sidebar.is(':visible');

        $('#conversation-info-sidebar').hide(); // Hide info if open
        $('#toggle-info-sidebar-btn').removeClass('active');

        if (isVisible) {
            $sidebar.slideUp(300);
            $(this).removeClass('active');
        } else {
            $sidebar.slideDown(300);
            $(this).addClass('active');
        }
    });

    $('#close-search-sidebar-btn').on('click', function () {
        $('#search-sidebar').slideUp(300);
        $('#toggle-search-sidebar-btn').removeClass('active');
    });

    // Conversation Context Menu Handlers
    let menuHideTimeout;

    // Show menu on hover
    $('body').on('mouseenter', '.list-group-item', function () {
        // Close any other open menus immediately
        $('.conversation-menu').hide();
        const $menu = $(this).find('.conversation-menu');
        $menu.show();
    });

    // Hide menu on leave, with a delay
    $('body').on('mouseleave', '.list-group-item', function () {
        const $menu = $(this).find('.conversation-menu');
        menuHideTimeout = setTimeout(function () {
            $menu.hide();
        }, 300);
    });

    // Keep menu open if mouse enters it
    $('body').on('mouseenter', '.conversation-menu', function () {
        clearTimeout(menuHideTimeout);
    });

    // Hide menu immediately if mouse leaves it
    $('body').on('mouseleave', '.conversation-menu', function () {
        $(this).hide();
    });

    // Handle clicking on the conversation item itself (not the menu)
    $('body').on('click', '.list-group-item', function(e) {
        // Only open chat if the click was not on a menu item
        if (!$(e.target).closest('.conversation-menu').length) {
            // This is where the original logic to open a chat window would go.
            // For now, we assume it's handled by other parts of the code that listen to a click on this item.
        }
    });

    // Pin Conversation
    $('body').on('click', '.conv-pin-btn', function (e) {
        e.preventDefault();
        e.stopPropagation();
        const $btn = $(this);
        const conversationId = $btn.data('id');
        const conversationType = $btn.data('type');

        $.ajax({
            url: urls.pinConversation,
            type: 'POST',
            data: {
                __RequestVerificationToken: antiForgeryToken,
                conversationId: conversationId,
                conversationType: conversationType
            },
            success: function (response) {
                if (response.success) {
                    loadConversations('all'); // Reload to reflect pinned status
                } else {
                    alert('Lỗi: ' + response.message);
                }
            },
            error: function () {
                alert('Lỗi kết nối khi ghim hội thoại.');
            }
        });
    });

    // MODIFIED: This handler now ONLY handles private chat deletion.
    $('body').on('click', '.conv-delete-btn', function (e) {
        e.preventDefault();
        e.stopPropagation();
        const $btn = $(this);
        const partnerUsername = $btn.data('username');
        const $item = $btn.closest('.list-group-item');

        if (confirm(`Bạn có chắc chắn muốn xóa toàn bộ lịch sử trò chuyện với ${partnerUsername} không? Hành động này không thể hoàn tác.`)) {
            $.ajax({
                url: urls.clearHistory,
                type: 'POST',
                data: {
                    __RequestVerificationToken: antiForgeryToken,
                    partnerUsername: partnerUsername
                },
                success: function (response) {
                    if (response.success) {
                        $item.fadeOut(400, function() {
                            $(this).remove();
                        });
                        alert('Đã xóa lịch sử trò chuyện.');
                        if (currentChat.partnerUsername === partnerUsername) {
                            $('#messagesList').empty();
                            $('#private-chat-header').hide();
                            $('#ai-chat-btn').click(); // Switch to default view
                        }
                    } else {
                        alert('Lỗi khi xóa hội thoại: ' + response.message);
                    }
                },
                error: function () {
                    alert('Lỗi kết nối khi xóa hội thoại.');
                }
            });
        }
    });

    // NEW: Handler for leaving a group.
    $('body').on('click', '.conv-leave-group-btn', function(e) {
        e.preventDefault();
        e.stopPropagation();
        const $btn = $(this);
        const groupId = $btn.data('id');
        const groupName = $btn.data('name');

        if (confirm(`Bạn có chắc chắn muốn rời khỏi nhóm "${groupName}" không?`)) {
            chatHub.server.leaveGroup(groupId).fail(function(err) {
                alert('Không thể rời nhóm: ' + err);
            });
        }
    });

    // Report Conversation
    $('body').on('click', '.conv-report-btn', function (e) {
        e.preventDefault();
        e.stopPropagation();
        const username = $(this).data('username');
        const reason = prompt(`Vui lòng nhập lý do báo cáo ${username}:`);

        if (reason && reason.trim()) {
            $.ajax({
                url: urls.reportConversation,
                type: 'POST',
                data: {
                    __RequestVerificationToken: antiForgeryToken,
                    reportedUsername: username,
                    reason: reason.trim()
                },
                success: function (response) {
                    if (response.success) {
                        alert('Cảm ơn bạn đã báo cáo. Chúng tôi sẽ xem xét trường hợp này.');
                    } else {
                        alert('Không thể gửi báo cáo: ' + (response.message || 'Lỗi không xác định.'));
                    }
                },
                error: function () {
                    alert('Lỗi kết nối khi gửi báo cáo.');
                }
            });
        }
    });

    // ========== MESSAGE SEARCH ==========
    function searchMessages() {
        const term = $('#search-message-input').val().trim();
        const resultsContainer = $('#search-results-container');

        if (!term) {
            resultsContainer.html('<p class="text-muted text-center">Vui lòng nhập nội dung tìm kiếm.</p>');
            return;
        }

        if (currentChat.mode !== 'private' || !currentChat.partnerUsername) {
            resultsContainer.html('<p class="text-danger text-center">Chức năng này chỉ hoạt động trong cuộc trò chuyện riêng tư.</p>');
            return;
        }

        resultsContainer.html('<p class="text-muted text-center"><i class="fas fa-spinner fa-spin"></i> Đang tìm kiếm...</p>');

        $.ajax({
            url: urls.searchMessages,
            type: 'GET',
            data: {
                term: term,
                partnerUsername: currentChat.partnerUsername
            },
            success: function (response) {
                resultsContainer.empty();
                if (response.success && response.results.length > 0) {
                    response.results.forEach(msg => {
                        // FIX: Parse content to get actual text
                        let contentText = 'Tin nhắn không thể hiển thị';
                        try {
                            const contentObj = JSON.parse(msg.Content);
                            if (contentObj.type === 'text') {
                                contentText = contentObj.content;
                            } else {
                                // Provide a placeholder for non-text content
                                contentText = `[${contentObj.type}]`;
                            }
                        } catch (e) {
                            // Fallback for old messages that might not be JSON
                            contentText = msg.Content;
                        }

                        // FIX: Use the parseTimestamp helper for correct date formatting
                        const msgDate = parseTimestamp(msg.Timestamp);
                        const timeString = msgDate ? msgDate.toLocaleString('vi-VN') : 'Thời gian không hợp lệ';

                        const resultHtml = `
                            <div class="search-result-item" data-message-id="${msg.Id}">
                                <img src="${msg.SenderAvatar}" class="search-result-avatar" />
                                <div class="search-result-content">
                                    <div>
                                        <span class="search-result-sender">${msg.SenderUsername}</span>
                                        <span class="search-result-time">${timeString}</span>
                                    </div>
                                    <div class="search-result-text">${$('<div/>').text(contentText).html()}</div>
                                </div>
                            </div>`;
                        resultsContainer.append(resultHtml);
                    });
                } else {
                    resultsContainer.html('<p class="text-muted text-center">Không tìm thấy kết quả nào.</p>');
                }
            },
            error: function () {
                resultsContainer.html('<p class="text-danger text-center">Lỗi khi tìm kiếm tin nhắn.</p>');
            }
        });
    }

    $('#execute-search-btn').on('click', searchMessages);
    $('#search-message-input').on('keypress', function (e) {
        if (e.which === 13) { // Enter key
            searchMessages();
        }
    });

    // ========== VOICE, VIDEO, GROUP BUTTONS ==========
    $('body').on('click', '#start-voice-call-btn', function() {
        startCall('voice');
    });

    $('body').on('click', '#start-video-call-btn', function() {
        startCall('video');
    });

    $('body').on('click', '#create-group-btn', function() {
        $('#createGroupModal').modal('show');
    });

    // Load friends when Create Group modal is shown
    $('#createGroupModal').on('show.bs.modal', function () {
        const $list = $('#groupMembersList');
        $list.html('<p class="text-center text-muted"><i class="fas fa-spinner fa-spin"></i> Đang tải danh sách bạn bè...</p>');

        $.getJSON(urls.getFriends, function (response) {
            if (response.success && response.friends) {
                $list.empty();
                if (response.friends.length === 0) {
                    $list.html('<p class="text-center text-muted">Bạn chưa có bạn bè nào để tạo nhóm.</p>');
                    return;
                }
                response.friends.forEach(friend => {
                    const friendHtml = `
                        <div class="list-group-item">
                            <div class="custom-control custom-checkbox">
                                <input type="checkbox" class="custom-control-input" id="group-member-${friend.Id}" value="${friend.Username}">
                                <label class="custom-control-label" for="group-member-${friend.Id}">
                                    <img src="${friend.AvatarUrl || '/Content/default-avatar.png'}" class="avatar-sm me-2" />
                                    <span>${friend.DisplayName}</span>
                                </label>
                            </div>
                        </div>`;
                    $list.append(friendHtml);
                });
            } else {
                $list.html('<p class="text-center text-danger">Không thể tải danh sách bạn bè.</p>');
            }
        }).fail(function () {
            $list.html('<p class="text-center text-danger">Lỗi kết nối khi tải danh sách bạn bè.</p>');
        });
    });

    // Handle confirm create group button click
    $('#confirmCreateGroupBtn').off('click').on('click', function () {
        const groupName = $('#groupNameInput').val().trim();
        const memberUsernames = $('#groupMembersList input:checked').map(function () {
            return $(this).val();
        }).get();

        if (!groupName) {
            alert('Vui lòng nhập tên nhóm.');
            return;
        }

        if (memberUsernames.length === 0) {
            alert('Vui lòng chọn ít nhất một thành viên.');
            return;
        }

        const $btn = $(this);
        $btn.prop('disabled', true).text('Đang tạo...');

        $.ajax({
            url: '/Chat/CreateGroup',
            type: 'POST',
            data: {
                __RequestVerificationToken: antiForgeryToken,
                groupName: groupName,
                memberUsernames: memberUsernames
            },
            success: function (response) {
                if (response.success) {
                    $('#createGroupModal').modal('hide');
                    // Reset form
                    $('#groupNameInput').val('');
                    $('#groupMembersList input:checked').prop('checked', false);
                    alert(`Đã tạo nhóm "${response.groupName}" thành công!`);

                    // Workaround for server-side race condition:
                    // Programmatically switch to the 'group' tab and then back to the 'all' tab.
                    // This mimics the user's manual fix and forces a reliable refresh.
                    $('.filter-tab[data-filter="group"]').click();
                    setTimeout(function() {
                        $('.filter-tab[data-filter="all"]').click();
                    }, 150); // Delay to allow the 'group' filter to load before switching back.
                } else {
                    alert('Lỗi khi tạo nhóm: ' + response.message);
                }
            },
            error: function (xhr, status, error) {
                alert('Đã xảy ra lỗi khi tạo nhóm. Vui lòng kiểm tra console (F12) để biết chi tiết.');
                console.error("Create Group Error:", {
                    status: status,
                    error: error,
                    responseText: xhr.responseText
                });
            },
            complete: function () {
                $btn.prop('disabled', false).text('Tạo nhóm');
            }
        });
    });

    $('body').on('click', '#hang-up-btn', function () {
        endCall(true);
    });

    $('body').on('click', '#decline-call-btn', function () {
        if (currentCallPartner) {
            chatHub.server.declineCall(currentCallPartner, 'declined');
        }
        endCall(false);
        $('#incomingCallModal').modal('hide');
    });

    $('body').on('click', '#accept-call-btn', async function () {
        if (window.pendingCallOffer && currentCallPartner) {
            $('#incomingCallModal').modal('hide');
            clearTimeout(callTimeout);

            try {
                const constraints = { audio: true, video: currentCallType === 'video' };
                localStream = await navigator.mediaDevices.getUserMedia(constraints);

                $('#call-view').fadeIn(300);
                $('#call-view-name').text($('#incoming-call-name').text());
                $('#call-view-avatar').attr('src', $('#incoming-call-avatar').attr('src'));
                $('#call-view-status').text('Đang kết nối...');

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
                callStartTime = new Date();

            } catch (error) {
                console.error('Error accepting call:', error);
                alert('Không thể truy cập camera/microphone!');
                endCall(false);
            }

            window.pendingCallOffer = null;
        }
    });



    chatHub.client.onGroupAvatarChanged = function (groupId, newAvatarUrl) {
        // Update avatar in conversation list
        const $groupItem = $(`.group-item[data-id="${groupId}"]`);
        if ($groupItem.length) {
            // Replace with a simple img, assuming composite avatars are not used after a custom one is set.
            const $avatarWrapper = $groupItem.find('.conv-avatar-wrapper');
            $avatarWrapper.html(`<img src="${newAvatarUrl}" alt="Group Avatar" class="conv-avatar-img" onerror="this.src='/Content/default-avatar.png';" />`);
        }

        // Update avatar in chat header if it's the current chat
        if (currentChat.mode === 'group' && currentChat.groupId === groupId) {
            $('#chat-header-avatar-wrapper').html(`<img id="chat-header-avatar" src="${newAvatarUrl}" alt="Avatar" class="chat-header-avatar me-2" />`);
        }

        // Update avatar in info sidebar if it's open
        if ($('#conversation-info-sidebar').is(':visible') && currentChat.mode === 'group' && currentChat.groupId === groupId) {
            $('#info-sidebar-avatar').attr('src', newAvatarUrl);
        }

        alert('Một nhóm đã cập nhật ảnh đại diện.');
    };

    function loadConversationInfo() {
        // This function now handles ALL info sidebar content, including nicknames and avatar sections.

        if (currentChat.mode === 'private') {
            $('#nickname-section').show();
            $('#change-group-avatar-btn').hide();

            // Populate nicknames
            const conversationId = getConversationId();
            const nicks = chatNicknames[conversationId] || {};
            $('#my-nickname-input').val(nicks[currentUsername] || '');
            $('#partner-nickname-input').val(nicks[currentChat.partnerUsername] || '');
            const partnerDisplayName = $('#chat-header-displayname').text() || currentChat.partnerUsername;
            $('#partner-nickname-label').text(`Biệt danh của ${partnerDisplayName}`);
            updateNicknamePreview();

            // Populate avatar
            const avatarSrc = $('#chat-header-avatar').attr('src');
            // Ensure wrapper contains a simple img tag for private chat
            $('#info-sidebar-avatar-wrapper').html(`<img id="info-sidebar-avatar" src="${avatarSrc}" />`);
            $('#info-sidebar-displayname').text($('#chat-header-displayname').text());

            // Fetch media
            $.getJSON(urls.getConversationInfo, { partnerUsername: currentChat.partnerUsername }, function (response) {
                if (response.success) {
                    const $imagesList = $('#info-sidebar-images-list');
                    $imagesList.empty();

                    if (response.images && response.images.length > 0) {
                        response.images.forEach(img => {
                            $imagesList.append(`<a href="${img.Url}" target="_blank" class="info-image-item"><img src="${img.Url}" alt="Ảnh" /></a>`);
                        });
                    } else {
                        $imagesList.html('<p class="text-muted text-center small p-3">Chưa có ảnh/video nào.</p>');
                    }

                    const $filesList = $('#info-sidebar-files-list');
                    $filesList.empty();

                    if (response.files && response.files.length > 0) {
                        response.files.forEach(file => {
                            $filesList.append(`<a href="${file.Url}" target="_blank" class="info-file-item"><i class="fas fa-file-alt" style="font-size:1.5rem; margin-right:10px; color:#007bff;"></i><div><div style="font-weight:600; font-size:0.9rem;">${file.FileName}</div><div style="font-size:0.75rem; color:#666;">${file.FileSize}</div></div></a>`);
                        });
                    } else {
                        $filesList.html('<p class="text-muted text-center small p-3">Chưa có file nào.</p>');
                    }
                }
            });

        } else if (currentChat.mode === 'group') {
            $('#nickname-section').hide();
            $('#change-group-avatar-btn').show();

            const groupName = $('#chat-header-displayname').text();
            const groupAvatarHtml = $('#chat-header-avatar-wrapper').html();

            // Use the new wrapper for the sidebar avatar
            $('#info-sidebar-avatar-wrapper').html(groupAvatarHtml);
            // The actual image inside might not have the ID, so let's fix that
            $('#info-sidebar-avatar-wrapper').find('img').attr('id', 'info-sidebar-avatar');

            $('#info-sidebar-displayname').text(groupName);

        } else {
            // AI chat or other modes
            $('#nickname-section').hide();
            $('#change-group-avatar-btn').hide();
        }
    }
    
    // Handler for triggering file input
    $('body').on('click', '#change-group-avatar-btn', function() {
        if (currentChat.mode === 'group' && currentChat.groupId) {
            $('#groupAvatarInput').click();
        }
    });

    // Handler for file selection and upload
    $('body').on('change', '#groupAvatarInput', function() {
        const file = this.files[0];
        if (!file) return;

        const groupId = currentChat.groupId;
        if (!groupId) {
            alert('Lỗi: Không tìm thấy ID của nhóm.');
            return;
        }

        const formData = new FormData();
        formData.append('groupId', groupId);
        formData.append('file', file);

        $.ajax({
            url: urls.changeGroupAvatar,
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            headers: {
                '__RequestVerificationToken': antiForgeryToken
            },
            success: function(response) {
                if (response.success) {
                    alert('Đổi ảnh đại diện nhóm thành công!');
                    // The UI update will be handled by the SignalR broadcast (onGroupAvatarChanged)
                } else {
                    alert('Lỗi: ' + response.message);
                }
            },
            error: function() {
                alert('Đã có lỗi xảy ra khi upload ảnh.');
            },
            complete: function() {
                // Clear the file input
                $('#groupAvatarInput').val('');
            }
        });
    });
    

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
            url: '/Account/UploadBackground',
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

        if (!confirm('Bạn có chắc muốn thu hồi tin nhắn này? Tin nhắn sẽ bị xóa ở cả hai phía.')) {
            return;
        }

        $.ajax({
            url: urls.clearHistory,
            type: 'POST',
            data: {
                __RequestVerificationToken: antiForgeryToken,
                partnerUsername: currentChat.partnerUsername
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

    // ========== BLOCK USER (NEW) ========== 
    $('body').on('click', '#info-action-block-user', function (e) {
        e.preventDefault();

        if (currentChat.mode !== 'private' || !currentChat.partnerId) {
            alert("Chức năng này chỉ khả dụng trong cuộc trò chuyện riêng tư.");
            return;
        }

        const partnerName = $('#chat-header-displayname').text();
        if (!confirm(`Bạn có chắc chắn muốn chặn ${partnerName} không?`)) {
            return;
        }

        $.ajax({
            url: urls.blockUser,
            type: 'POST',
            data: {
                __RequestVerificationToken: antiForgeryToken,
                friendId: currentChat.partnerId
            },
            success: function (response) {
                if (response.success) {
                    alert(response.message || `Đã chặn ${partnerName}.`);
                    
                    // Close the chat window for the blocked user
                    $('#ai-chat-btn').click(); 

                    // Refresh the conversation list to remove the blocked user
                    loadConversations('all');
                } else {
                    alert('Lỗi: ' + (response.message || 'Không thể chặn người dùng này.'));
                }
            },
            error: function () {
                alert('Đã có lỗi kết nối xảy ra. Vui lòng thử lại.');
            }
        });
    });

    $('#info-action-block').on('click', function (e) {
        e.preventDefault();
        const $blockBtn = $(this);
        const partnerUsername = $blockBtn.data('partner-username');
        const isBlocked = $blockBtn.hasClass('btn-danger');

        chatHub.server.blockUser(partnerUsername, !isBlocked)
            .done(function (response) {
                if (response.success) {
                    if (!isBlocked) {
                        $blockBtn.removeClass('btn-light').addClass('btn-danger').text('Bỏ chặn');
                        showToast('success', `Đã chặn ${response.partnerName}.`);
                    } else {
                        $blockBtn.removeClass('btn-danger').addClass('btn-light').text('Chặn');
                        showToast('success', `Đã bỏ chặn ${response.partnerName}.`);
                    }
                } else {
                    showToast('error', response.message);
                }
            })
            .fail(function (err) {
                showToast('error', 'Lỗi kết nối.');
            });
    });

    // ========== REPORT USER ========== 
    $('body').on('click', '.report-conversation-btn', function (e) {
        e.preventDefault();
        e.stopPropagation();
        const username = $(this).closest('.friend-item').data('username');
        const reason = prompt(`Vui lòng nhập lý do báo cáo ${username}:`);

        if (reason && reason.trim()) {
            $.ajax({
                url: urls.reportConversation,
                type: 'POST',
                data: {
                    __RequestVerificationToken: antiForgeryToken,
                    reportedUsername: username,
                    reason: reason.trim()
                },
                success: function (response) {
                    if (response.success) {
                        showToast('success', 'Cảm ơn bạn đã báo cáo. Chúng tôi sẽ xem xét trường hợp này.');
                    } else {
                        showToast('error', response.message || 'Không thể gửi báo cáo.');
                    }
                },
                error: function () {
                    showToast('error', 'Lỗi kết nối khi gửi báo cáo.');
                }
            });
        }
        $('.conversation-options-menu').removeClass('show');
    });

    function adjustMenuPosition($menu, $button) {
        const menuWidth = $menu.outerWidth();
        const menuHeight = $menu.outerHeight();
        const buttonRect = $button[0].getBoundingClientRect(); // Lấy vị trí chính xác so với màn hình
        const windowWidth = $(window).width();
        const windowHeight = $(window).height();

        let top = buttonRect.bottom + 5; // Mặc định hiện bên dưới nút
        let left = buttonRect.right - menuWidth; // Mặc định căn lề phải với nút (để menu mở sang trái)

        if (top + menuHeight > windowHeight - 10) {
            top = buttonRect.top - menuHeight - 5;
        }

        if (left < 10) {
            left = 10; 
        }

        if (left + menuWidth > windowWidth) {
            left = windowWidth - menuWidth - 10;
        }

        $menu.addClass('fixed').css({
            position: 'fixed',
            top: top + 'px',
            left: left + 'px',
            right: 'auto',
            bottom: 'auto',
            margin: 0
        });
    }

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

        if (isTyping && currentChat.mode === 'private' && currentChat.partnerUsername) {
            clearTimeout(typingTimer);
            isTyping = false;
            if (chatHub.server.userStoppedTyping) {
                chatHub.server.userStoppedTyping(currentChat.partnerUsername);
            }
        }

        const now = new Date();
        const tempId = `temp_${Date.now()}`;

        const contentToSend = messageContent;
        $('#messageInput').val('').focus();

        const parentMessageId = currentReplyInfo ? currentReplyInfo.messageId : null;

        renderMessage({
            senderUsername: currentUsername,
            content: JSON.stringify({ type: 'text', content: contentToSend }),
            timestamp: now.toISOString(),
            isSelf: true,
            status: 'Pending',
            messageId: tempId,
            parentMessage: currentReplyInfo ? {
                SenderUsername: currentReplyInfo.sender,
                Content: currentReplyInfo.content
            } : null
        });

        if (currentChat.mode === 'ai') {
            chatHub.server.sendMessageToAI(contentToSend);
            showTypingIndicator('AI Assistant', '/Content/default-avatar.png');
        } else if (currentChat.mode === 'private') {
            const msgJson = JSON.stringify({ type: 'text', content: contentToSend });
            chatHub.server.sendPrivateMessage(
                currentChat.partnerUsername,
                msgJson,
                tempId,
                parentMessageId
            );
        } else if (currentChat.mode === 'group') {
            const msgJson = JSON.stringify({ type: 'text', content: contentToSend });
            chatHub.server.sendGroupMessage(currentChat.groupId, msgJson);
        }

        if (currentReplyInfo) {
            hideReplyBanner();
        }
    }

    $('body').on('click', '#sendButton', sendTextMessage);
    $('body').on('keypress', '#messageInput', function (e) {
        if (e.which === 13 && !e.shiftKey) {
            e.preventDefault();
            sendTextMessage();
        }
    });

    // ========== VOICE RECORDING ==========
    async function startRecording() {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            isRecording = true;
            audioChunks = [];
            mediaRecorder = new MediaRecorder(stream);

            mediaRecorder.ondataavailable = event => {
                audioChunks.push(event.data);
            };

            mediaRecorder.onstop = () => {
                const audioBlob = new Blob(audioChunks, { type: 'audio/wav' });
                uploadVoiceMessage(audioBlob);
                stream.getTracks().forEach(track => track.stop()); // Stop microphone access
            };

            mediaRecorder.start();
            updateRecordingUI(true);

        } catch (err) {
            console.error("Error accessing microphone:", err);
            alert("Không thể truy cập microphone. Vui lòng cấp quyền truy cập.");
        }
    }

    function stopRecording() {
        if (mediaRecorder && isRecording) {
            mediaRecorder.stop();
            isRecording = false;
            updateRecordingUI(false);
        }
    }

    function updateRecordingUI(isRecordingActive) {
        const $recordBtn = $('#record-voice-btn');
        const $timer = $('#recording-timer');
        const $messageInput = $('#messageInput');
        const $otherButtons = $('#emoji-button, #quick-image-btn, #toggle-attach-menu, #sendButton');

        if (isRecordingActive) {
            $recordBtn.find('i').removeClass('fa-microphone').addClass('fa-stop-circle').css('color', '#d9534f');
            $timer.show();
            $messageInput.hide();
            $otherButtons.hide();

            recordingStartTime = Date.now();
            recordingInterval = setInterval(() => {
                const elapsedSeconds = Math.floor((Date.now() - recordingStartTime) / 1000);
                const minutes = Math.floor(elapsedSeconds / 60).toString().padStart(2, '0');
                const seconds = (elapsedSeconds % 60).toString().padStart(2, '0');
                $timer.find('span').text(`${minutes}:${seconds}`);
            }, 1000);
        } else {
            $recordBtn.find('i').removeClass('fa-stop-circle').addClass('fa-microphone').css('color', '');
            $timer.hide();
            $messageInput.show();
            $otherButtons.show();
            clearInterval(recordingInterval);
            $timer.find('span').text('00:00');
        }
    }

    function uploadVoiceMessage(audioBlob) {
        const formData = new FormData();
        formData.append('voice', audioBlob, `voice-message-${Date.now()}.wav`);

        // Show a temporary "uploading" message
        const tempId = `temp_voice_${Date.now()}`;
        renderMessage({
            senderUsername: currentUsername,
            content: JSON.stringify({ type: 'text', content: 'Đang gửi tin nhắn thoại...' }),
            timestamp: new Date().toISOString(),
            isSelf: true,
            status: 'Pending',
            messageId: tempId
        });

        $.ajax({
            url: '/Upload/Voice', // New endpoint
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {
                // Remove temporary message
                $(`.chat-message[data-message-id="${tempId}"]`).remove();

                if (response.success) {
                    const contentObj = {
                        type: 'voice',
                        content: response.filePath,
                        fileName: 'voice_message.wav',
                        fileSize: response.fileSize
                    };
                    const messageJson = JSON.stringify(contentObj);
                    const finalTempId = `temp_final_voice_${Date.now()}`;

                    renderMessage({
                        senderUsername: currentUsername,
                        content: messageJson,
                        timestamp: new Date().toISOString(),
                        isSelf: true,
                        status: 'Pending',
                        messageId: finalTempId
                    });

                    chatHub.server.sendPrivateMessage(currentChat.partnerUsername, messageJson, finalTempId, null);
                } else {
                    alert('Lỗi khi tải tệp thoại lên: ' + (response.message || 'Không có phản hồi từ server.'));
                }
            },
            error: function (xhr, status, error) {
                $(`.chat-message[data-message-id="${tempId}"]`).remove();
                alert('Đã xảy ra lỗi mạng khi tải tệp thoại lên. Kiểm tra console (F12) để biết thêm chi tiết.');
                console.error("Voice upload error:", {
                    status: status,
                    error: error,
                    responseText: xhr.responseText
                });
            }
        });
    }

    $('#record-voice-btn').on('click', function () {
        if (isRecording) {
            stopRecording();
        } else {
            startRecording();
        }
    });

    // ========== FILE & IMAGE UPLOAD ==========

    // ========== FILE UPLOAD - FIXED VERSION ========== 

    // Trigger file input when quick image button is clicked
    $('body').on('click', '#quick-image-btn', function () {
        if (currentChat.mode === 'ai') {
            alert('Bạn không thể gửi ảnh cho AI.');
            return;
        }
        $('#imageUploadInput').click();
    });


    $('#imageUploadInput').on('change', function (e) {
        const files = e.target.files;
        if (!files || files.length === 0) return;

        tempFilesToSend = Array.from(files);
        const previewContainer = $('#imagePreviewContainer');
        previewContainer.empty();

        tempFilesToSend.forEach(file => {
            const reader = new FileReader();
            reader.onload = function (event) {
                const isVideo = file.type.startsWith('video/');

                if (isVideo) {
                    previewContainer.append(`
                    <video controls style="width:120px;height:120px;object-fit:cover;margin:5px;border-radius:8px;">
                        <source src="${event.target.result}" type="${file.type}">
                    </video>
                `);
                } else {
                    previewContainer.append(`
                    <img src="${event.target.result}" 
                         class="img-fluid rounded"
                         style="width:120px;height:120px;object-fit:cover;margin:5px;" />
                `);
                }
            };
            reader.readAsDataURL(file);
        });

        $('#imagePreviewModal').modal('show');
    });


    // ✅ Handle the actual image sending - FIXED VERSION
    $('#sendImageButton').off('click').on('click', function () {
        // ✅ Kiểm tra tempFilesToSend thay vì input
        if (!tempFilesToSend || tempFilesToSend.length === 0) {
            alert('Vui lòng chọn file để gửi.');
            return;
        }

        // ✅ Dùng trực tiếp tempFilesToSend
        const formData = new FormData();
        tempFilesToSend.forEach(file => {
            formData.append('files', file);
        });

        const $btn = $(this);
        $btn.prop('disabled', true).text('Đang gửi...');

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
                        const contentObj = {
                            type: fileData.type,
                            content: fileData.filePath,
                            fileName: fileData.fileName,
                            fileSize: fileData.fileSize
                        };
                        const messageJson = JSON.stringify(contentObj);
                        const tempId = `temp_file_${Date.now()}_${Math.random()}`;

                        // Render message locally
                        renderMessage({
                            senderUsername: currentUsername,
                            content: messageJson,
                            timestamp: new Date().toISOString(),
                            isSelf: true,
                            status: 'Pending',
                            messageId: tempId
                        });

                        // Send via SignalR
                        if (currentChat.mode === 'private') {
                            chatHub.server.sendPrivateMessage(
                                currentChat.partnerUsername,
                                messageJson,
                                tempId,
                                null
                            );
                        } else if (currentChat.mode === 'group') {
                            chatHub.server.sendGroupMessage(currentChat.groupId, messageJson);
                        }
                    });

                    // Hide AI welcome screen if in AI mode
                    if (currentChat.mode === 'ai') {
                        $('#ai-welcome-screen').hide();
                        $('.message-area').show();
                    }

                    // --- FIX: Refresh conversation info if sidebar is open ---
                    if ($('#conversation-info-sidebar').is(':visible')) {
                        loadConversationInfo();
                    }
                    // --- END FIX ---

                } else {
                    alert('Lỗi upload: ' + (response.message || 'Không rõ nguyên nhân'));
                }
            },
            error: function (xhr, status, error) {
                console.error('❌ Upload error:', xhr.responseText);
                alert('Lỗi kết nối: ' + error);
            },
            complete: function () {
                // ✅ Reset everything
                $btn.prop('disabled', false).text('Gửi');
                $('#imagePreviewModal').modal('hide');
                $('#imagePreviewContainer').empty();
                tempFilesToSend = null;
                $('#imageUploadInput').val('');
            }
        });
    });

    // ✅ Clear temp files when modal is closed without sending
    $('#imagePreviewModal').on('hidden.bs.modal', function () {
        if (tempFilesToSend !== null) {
            tempFilesToSend = null;
            $('#imagePreviewContainer').empty();
        }
    });

    // Generic file upload (for the paperclip button)
    $('body').on('click', '#toggle-attach-menu', function () {
        if (currentChat.mode === 'ai') {
            alert('Bạn không thể gửi tệp cho AI.');
            return;
        }
        $('#fileUploadInput').click();
    });

    // Handle generic file upload - MODIFIED TO USE PREVIEW MODAL
    $('#fileUploadInput').on('change', function (e) {
        const files = e.target.files;
        if (!files || files.length === 0) return;

        tempFilesToSend = Array.from(files);
        const previewContainer = $('#imagePreviewContainer');
        previewContainer.empty();

        // Helper function to format file size
        function formatBytes(bytes, decimals = 2) {
            if (bytes === 0) return '0 Bytes';
            const k = 1024;
            const dm = decimals < 0 ? 0 : decimals;
            const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
            const i = Math.floor(Math.log(bytes) / Math.log(k));
            return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
        }

        tempFilesToSend.forEach(file => {
            const isImage = file.type.startsWith('image/');
            const isVideo = file.type.startsWith('video/');

            if (isImage || isVideo) {
                const reader = new FileReader();
                reader.onload = function (event) {
                    if (isVideo) {
                        previewContainer.append(`
                        <div class="file-preview-item" style="width: 120px; height: 120px;">
                            <video controls style="width:100%; height:100%; object-fit:cover; border-radius:8px;">
                                <source src="${event.target.result}" type="${file.type}">
                            </video>
                        </div>
                        `);
                    } else {
                         previewContainer.append(`
                        <div class="file-preview-item" style="width: 120px; height: 120px;">
                             <img src="${event.target.result}" 
                                 class="img-fluid rounded"
                                 style="width:100%; height:100%; object-fit:cover;" />
                        </div>
                        `);
                    }
                };
                reader.readAsDataURL(file);
            } else {
                // Generic file preview
                const fileSize = formatBytes(file.size);
                previewContainer.append(`
                    <div class="file-preview-item text-center p-2" style="background:#f0f0f0; border-radius:8px; width: 120px; height: 120px; display: flex; flex-direction: column; align-items: center; justify-content: center;">
                        <i class="fas fa-file-alt" style="font-size:2.5rem; color:#007bff; margin-bottom: 8px;"></i>
                        <div style="font-weight:600; font-size:0.8rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; width: 100%;" title="${file.name}">${file.name}</div>
                        <div style="font-size:0.7rem; color:#666;">${fileSize}</div>
                    </div>
                `);
            }
        });

        $('#imagePreviewModal').modal('show');
        // Clear the input value so the user can select the same file again if they cancel
        $(this).val('');
    });


    $('body').on('input', '#messageInput', function () {
        sendTypingSignal();
    });

    function loadChatHistory(partnerUsername) {
        if (!partnerUsername) return;
    
        console.log('🔄 Loading chat history for:', partnerUsername);
    
        $.getJSON(urls.getChatHistory, { partnerUsername: partnerUsername }, function (response) {
            if (response.success) {
                $('#messagesList').empty();
                response.messages.forEach(msg => {
                    renderMessage({
                        senderUsername: msg.SenderUsername,
                        senderAvatar: msg.SenderAvatar,
                        content: msg.Content,
                        timestamp: msg.Timestamp,
                        isSelf: msg.SenderUsername === currentUsername,
                        status: msg.Status,
                        messageId: msg.Id,
                        parentMessage: msg.ParentMessage,
                        reactions: msg.Reactions,
                        forwardedFrom: msg.ForwardedFrom,
                        editedAt: msg.EditedAt,
                        isDeleted: msg.IsDeleted
                    });
                });
                $('#messagesList').scrollTop($('#messagesList')[0].scrollHeight);
                console.log('✅ Chat history loaded:', response.messages.length, 'messages');
            } else {
                console.error('❌ Failed to load chat history:', response.message);
            }
        }).fail(function (xhr, status, error) {
            console.error('❌ AJAX error loading chat history:', error);
        });
    }
    
    function loadGroupChatHistory(groupId) {
        if (!groupId) return;
    
        console.log('🔄 Loading group chat history for:', groupId);
    
        // Assuming an endpoint `/Chat/GetGroupChatHistory` exists.
        $.getJSON(urls.getGroupChatHistory, { groupId: groupId }, function (response) {
            if (response.success) {
                $('#messagesList').empty();
                response.messages.forEach(msg => {
                    renderMessage({
                        senderUsername: msg.SenderUsername,
                        senderAvatar: msg.SenderAvatar,
                        content: msg.Content,
                        timestamp: msg.Timestamp,
                        isSelf: msg.SenderUsername === currentUsername,
                        status: msg.Status,
                        messageId: msg.Id,
                        parentMessage: msg.ParentMessage,
                        reactions: msg.Reactions,
                        forwardedFrom: msg.ForwardedFrom,
                        editedAt: msg.EditedAt,
                        isDeleted: msg.IsDeleted
                    });
                });
                $('#messagesList').scrollTop($('#messagesList')[0].scrollHeight);
                console.log('✅ Group chat history loaded:', response.messages.length, 'messages');
            } else {
                $('#messagesList').html('<div class="text-center text-muted p-3">Không thể tải lịch sử nhóm.</div>');
                console.error('❌ Failed to load group chat history:', response.message);
            }
        }).fail(function (xhr, status, error) {
            $('#messagesList').html('<div class="text-center text-danger p-3">Lỗi máy chủ khi tải lịch sử nhóm.</div>');
            console.error('❌ AJAX error loading group chat history:', error);
        });
    }

    function updateUnreadBadge(username, count) {
        var $conversationItem = $(`.conversation-item[data-username="${username}"]`);
        var $badge = $conversationItem.find('.unread-badge');

        if (count > 0) {
            if ($badge.length === 0) {
                $conversationItem.append(`<span class="unread-badge">${count}</span>`);
            } else {
                $badge.text(count);
            }
            $conversationItem.addClass('has-unread');
        } else {
            $badge.remove();
            $conversationItem.removeClass('has-unread');
        }
    }

    function markMessagesAsRead(partnerUsername) {
        if (chatHub && chatHub.connection.state === $.signalR.connectionState.connected) {
            chatHub.server.markMessagesAsRead(partnerUsername)
                .done(function () {
                    console.log('✅ Marked messages as read for:', partnerUsername);
                    updateUnreadBadge(partnerUsername, 0);
                })
                .fail(function (err) {
                    console.error('❌ Error marking messages as read:', err);
                });
        }
    }

function switchChat(target) {
    hideTypingIndicator();

    // Stop typing if the user was typing
    if (isTyping && currentChat.mode === 'private' && currentChat.partnerUsername) {
        clearTimeout(typingTimer);
        isTyping = false;
        if (chatHub.server.userStoppedTyping) {
            chatHub.server.userStoppedTyping(currentChat.partnerUsername);
        }
    }

    // Hide sidebars to ensure full width view on chat switch
    $('#conversation-info-sidebar').hide();
    $('#search-sidebar').hide();
    $('#toggle-info-sidebar-btn').removeClass('active');
    $('#toggle-search-sidebar-btn').removeClass('active');

    $('#messagesList').empty();
    $('.conversation-list .list-group-item-action').removeClass('active');
    $(target).addClass('active');

    currentChat.mode = $(target).data('chat-mode');

    // Apply the correct background for the conversation
    const conversationId = getConversationId();
    const savedBg = chatBackgrounds[conversationId];
    applyBackground(savedBg);

    if (currentChat.mode === 'ai') {
        // AI mode UI setup
        $('.conversation-list').show(); // Hide conversation list for more space
        $('#ai-chat-header').show();
        $('#private-chat-header').hide();
        $('#messageInput').attr('placeholder', 'Hỏi tôi bất cứ điều gì...?');
        $('#ai-welcome-screen').show();
        $('.message-area').hide();
        currentChat.partnerUsername = null;
        currentChat.groupId = null;

    } else if (currentChat.mode === 'private') {
        // Set partner username before using it
        currentChat.partnerUsername = $(target).data('username');
        currentChat.partnerId = $(target).data('id'); 

        // Private chat UI setup
        $('.conversation-list').show(); // Ensure conversation list is visible
        $('#private-chat-header').show();
        $('#ai-chat-header').hide();
        $('#messageInput').attr('placeholder', 'Nhập tin nhắn...');
        $('#user-chat-header').show();
        $('#user-chat-buttons').show();
        $('#ai-welcome-screen').hide();
        $('.message-area').show();

        // Restore header buttons for private chat
        $('#start-voice-call-btn, #start-video-call-btn').show();
        $('#create-group-btn').show();

        // Restore the avatar structure to a single image
        $('#chat-header-avatar-wrapper').html('<img id="chat-header-avatar" src="" alt="Avatar" class="chat-header-avatar me-2" />');

        // Join SignalR group for private chat
        if (chatHub.server.joinPrivateGroup) {
            chatHub.server.joinPrivateGroup(currentChat.partnerUsername)
                .done(() => console.log(`✅ Joined private group with ${currentChat.partnerUsername}`))
                .fail(err => console.error('❌ Failed to join private group:', err));
        }

        // Update chat header with partner info
        const displayName = $(target).find('strong').text().trim();
        const avatarSrc = $(target).data('avatar-url') || '/Content/default-avatar.png';

        const partnerNickname = getNickname(currentChat.partnerUsername, conversationId);
        $('#chat-header-displayname').text(partnerNickname || displayName);
        $('#chat-header-avatar').attr('src', avatarSrc);

        const isOnline = isUserOnline(currentChat.partnerUsername);
        $('#chat-header-status').text(getLastSeenText(currentChat.partnerUsername))
            .toggleClass('online', isOnline);

        // Update hidden chat toggle based on local storage
        const hiddenChats = JSON.parse(localStorage.getItem('hiddenChats') || '[]');
        $('#info-action-hide-chat').prop('checked', hiddenChats.includes(currentChat.partnerUsername));

        // Load chat history for the selected partner
        loadChatHistory(currentChat.partnerUsername);

        // Mark messages as read for the selected partner
        markMessagesAsRead(currentChat.partnerUsername);
    } else if (currentChat.mode === 'group') {
        // Set current chat context
        currentChat.groupId = $(target).data('id');
        currentChat.partnerUsername = null;

        // Configure UI for group chat
        $('#private-chat-header').show();
        $('#ai-chat-header').hide();
        $('#messageInput').attr('placeholder', 'Nhập tin nhắn trong nhóm...');
        $('#user-chat-header').show();
        $('#user-chat-buttons').show();
        $('#ai-welcome-screen').hide();
        $('.message-area').show();

        // Update chat header with group info from the conversation list item
        const groupName = $(target).find('strong').text().trim();
        const avatarHtml = $(target).find('.conv-avatar-wrapper').html(); // Get the inner HTML of the avatar container

        $('#chat-header-displayname').text(groupName);
        $('#chat-header-avatar-wrapper').html(avatarHtml); // Replace the header avatar with the group's avatar
        $('#chat-header-status').text('Nhóm chat');

        // Configure header buttons for a group chat (hide irrelevant buttons)
        $('#start-voice-call-btn, #start-video-call-btn, #create-group-btn').hide();
        $('#toggle-search-sidebar-btn, #toggle-info-sidebar-btn, #toggle-conversations-btn').show();

        // Load the group's message history
        loadGroupChatHistory(currentChat.groupId);
    }
}

    $('.conversation-list').on('click', '.list-group-item-action', function (e) {
        e.preventDefault();
        switchChat(this);
    });

    $('#toggle-conversations-btn').on('click', function () {
        $('.conversation-list').toggle();
    });

    $('#toggle-attach-menu').on('click', function (e) {
        e.stopPropagation();

        if ($('#attachment-menu').length === 0) {
            const menuHtml = `
        <div id="attachment-menu" style="display:none; position:fixed; background:white; border:1px solid #ddd; border-radius:8px; box-shadow:0 4px 12px rgba(0,0,0,0.15); padding:8px 0; z-index:1000;">
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

    $(document).ready(function () {
        const button = document.querySelector('#emoji-button');
        const messageInput = document.querySelector('#messageInput');

        if (button && messageInput) {
            // Tạo emoji picker
            const picker = document.createElement('emoji-picker');
            picker.style.cssText = 'position:absolute; bottom:60px; left:15px; display:none; z-index:1000;';
            document.querySelector('.input-area').appendChild(picker);

            // Toggle picker
            button.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                picker.style.display = picker.style.display === 'none' ? 'block' : 'none';
            });

            // Chọn emoji
            picker.addEventListener('emoji-click', (event) => {
                const emoji = event.detail.unicode;
                const start = messageInput.selectionStart;
                const end = messageInput.selectionEnd;
                const text = messageInput.value;

                messageInput.value = text.substring(0, start) + emoji + text.substring(end);
                messageInput.setSelectionRange(start + emoji.length, start + emoji.length);
                messageInput.focus();
            });

            // Đóng picker khi click ngoài
            $(document).on('click', function (e) {
                if (!$(e.target).closest('#emoji-button, emoji-picker').length) {
                    picker.style.display = 'none';
                }
            });

            console.log('✅ Emoji Picker initialized');
        }
    });
    $('body').on('click', '.ai-suggest-btn', function () {
        const text = $(this).text().trim();

        if (!text) return;

        $('#ai-welcome-screen').hide();
        $('.message-area').show();

        renderMessage({
            senderUsername: currentUsername,
            content: JSON.stringify({ type: 'text', content: text }),
            timestamp: new Date().toISOString(),
            isSelf: true,
            status: 'Pending',
            messageId: `temp_${Date.now()}`
        });

        if (chatHub.server.sendMessageToAI) {
            chatHub.server.sendMessageToAI(text);
        }

        showTypingIndicator('AI Assistant', '/Content/default-avatar.png');
    });

    $('body').on('click', '.ai-prompt-btn', function () {
        const promptText = $(this).data('prompt');
        if (!promptText) return;

        // Hide welcome screen and show message area
        $('#ai-welcome-screen').hide();
        $('.message-area').show();

        // Render the user's message immediately
        renderMessage({
            senderUsername: currentUsername,
            content: JSON.stringify({ type: 'text', content: promptText }),
            timestamp: new Date().toISOString(),
            isSelf: true,
            status: 'Pending',
            messageId: `temp_${Date.now()}`
        });

        // Send the prompt to the AI via SignalR
        if (chatHub.server.sendMessageToAI) {
            chatHub.server.sendMessageToAI(promptText);
        }

        // Show typing indicator for the AI
        showTypingIndicator('AI Assistant', '/Content/default-avatar.png');
    });

    // ========== PARTNER PROFILE MODAL - CODE THAY THẾ ========== 
    // Paste code này vào chat-client.js, thay thế hàm cũ

    // Click vào AVATAR hoặc TÊN để mở profile (không click vào toàn bộ header)
    $('body').on('click', '#chat-header-avatar, #chat-header-displayname', function (e) {
        e.preventDefault();
        e.stopPropagation();

        if (window.currentChat.mode === 'private' && window.currentChat.partnerUsername) {
            openPartnerProfileModal(window.currentChat.partnerUsername);
        }
    });

    // Hàm mở profile modal
    function openPartnerProfileModal(partnerUsername) {
        console.log('🔍 Opening profile for:', partnerUsername);

        // Reset modal về trạng thái loading
        $('#partner-modal-display-name').text('Đang tải...');
        $('#partner-modal-username').text(`@${partnerUsername}`);
        $('#partner-modal-avatar').attr('src', '/Content/default-avatar.png');
        $('#partner-modal-cover').css('background-image', 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)');
        $('#partner-modal-gender').text('Đang tải...');
        $('#partner-modal-dob').text('Đang tải...');
        $('#partner-modal-phone').text('Đang tải...');
        $('#partner-modal-email').text('Đang tải...');
        $('#partner-modal-qrcode').attr('src', '');
        $('#partner-unfriend-form').hide();

        // Hiển thị modal
        $('#partnerProfileModal').modal('show');

        // Gọi API lấy thông tin
        $.ajax({
            url: '/Profile/GetUserPublicProfile',
            type: 'GET',
            data: { username: partnerUsername },
            dataType: 'json',
            success: function (response) {
                console.log('✅ Profile loaded:', response);

                if (response.success && response.user) {
                    const user = response.user;

                    // Cập nhật thông tin cơ bản
                    $('#partner-modal-display-name').text(user.DisplayName || partnerUsername);
                    $('#partner-modal-username').text(`@${user.Username}`);

                    // Cập nhật avatar
                    const avatarUrl = user.AvatarUrl || '/Content/default-avatar.png';
                    $('#partner-modal-avatar').attr('src', avatarUrl);

                    // Cập nhật cover photo
                    if (user.CoverUrl) {
                        $('#partner-modal-cover').css('background-image', `url(${user.CoverUrl})`);
                    }
                    if (user.QrCodeUrl) {
                        $('#partner-modal-qrcode').attr('src', user.QrCodeUrl).show();
                    } else {
                        $('#partner-modal-qrcode').hide();
                    }
                    // Cập nhật các thông tin khác
                    $('#partner-modal-gender').text(user.Gender || 'Chưa cập nhật');
                    $('#partner-modal-phone').text(user.PhoneNumber || 'Chưa cập nhật');
                    $('#partner-modal-email').text(user.Email || 'Chưa cập nhật');
                    $('#partner-modal-bio').text(user.Bio || 'Không có tiểu sử.');

                    // Format ngày sinh
                    if (user.DateOfBirth) {
                        try {
                            const dob = new Date(user.DateOfBirth);
                            if (!isNaN(dob.getTime())) {
                                const day = dob.getDate();
                                const month = dob.getMonth() + 1;
                                const year = dob.getFullYear();
                                $('#partner-modal-dob').text(`${day}/${month < 10 ? '0' + month : month}/${year}`);
                            } else {
                                $('#partner-modal-dob').text('Chưa cập nhật');
                            }
                        } catch (e) {
                            console.error('Error parsing date:', e);
                            $('#partner-modal-dob').text('Chưa cập nhật');
                        }
                    } else {
                        $('#partner-modal-dob').text('Chưa cập nhật');
                    }

                    // Hiện nút unfriend nếu có friendshipId
                    if (user.FriendshipId) {
                        $('#partner-unfriend-id').val(user.FriendshipId);
                        $('#partner-unfriend-form').show();
                    } else {
                        $('#partner-unfriend-form').hide();
                    }

                } else {
                    console.error('❌ API returned error:', response.message);
                    $('#partner-modal-display-name').text(response.message || 'Không tìm thấy người dùng');
                    setTimeout(() => {
                        $('#partnerProfileModal').modal('hide');
                    }, 2000);
                }
            },
            error: function (xhr, status, error) {
                console.error('❌ AJAX Error:', {
                    status: status,
                    error: error,
                    response: xhr.responseText
                });
                $('#partner-modal-display-name').text('Lỗi kết nối máy chủ');
                setTimeout(() => {
                    $('#partnerProfileModal').modal('hide');
                }, 2000);
            }
        });
    }

    $.connection.hub.start().done(function () {
        console.log('✅ SignalR Connected. Connection ID:', $.connection.hub.id);

        // Load ALL conversations (friends and groups) after SignalR is connected
        loadConversations('all');

        // Nếu có selectedFriendUsername từ server, mở chat đó
        if (config.selectedFriendUsername) {
            const $selectedFriend = $(`.friend-item[data-username="${config.selectedFriendUsername}"]`);
            if ($selectedFriend.length) {
                setTimeout(() => {
                    $selectedFriend.click();
                }, 500);
            }
        }
    })
    .fail(function (error) {
        console.error('❌ SignalR connection failed:', error);
        alert('Không thể kết nối đến server. Vui lòng tải lại trang.');
    });
    $(document).ready(function () {
        $('.conversation-list').show().css('display', 'flex');
        console.log('✅ Conversation list forced to display');
    });
    window.openPartnerProfileModal = openPartnerProfileModal;

    console.log('✅ Partner Profile Modal initialized');

    // ========== JUMP TO SEARCHED MESSAGE ==========
    // Add CSS for highlight effect
    $('<style>')
        .prop('type', 'text/css')
        .html(`
            .chat-message.highlight {
                animation: highlight-outline-anim 1.5s ease-out;
                border-radius: 8px;
            }
            @keyframes highlight-outline-anim {
                0%, 60% { outline: 2px solid #0d6efd; }
                100% { outline: 2px solid transparent; }
            }
        `)
        .appendTo('head');

    // Add click handler for search results
    $('body').on('click', '.search-result-item', function() {
        const messageId = $(this).data('message-id');
        if (!messageId) return;

        const $message = $(`.chat-message[data-message-id="${messageId}"]`);
        const $messagesList = $('#messagesList');

        if ($message.length) {
            // Scroll message into view (centered)
            $messagesList.animate({
                scrollTop: $messagesList.scrollTop() + $message.position().top - ($messagesList.height() / 2) + ($message.height() / 2)
            }, 300);

            // Add highlight class and remove after animation
            $message.addClass('highlight');
            setTimeout(() => {
                $message.removeClass('highlight');
            }, 1500); // Animation is 1.5s
        } else {
            alert('Không thể tìm thấy tin nhắn trong các tin đã tải. Vui lòng cuộn lên để tải thêm tin nhắn cũ và thử lại.');
        }
    });
});