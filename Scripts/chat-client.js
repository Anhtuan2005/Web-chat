$(function () {
    const config = window.chatConfig || {};
    const currentUsername = config.currentUsername || '';  
    const urls = config.urls || {};  
    let antiForgeryToken = config.antiForgeryToken || '';  

    antiForgeryToken = $('#logoutForm input[name="__RequestVerificationToken"]').val() || antiForgeryToken;

    window.chatHub = $.connection.chatHub;
    window.currentChat = { mode: 'ai', partnerUsername: null };
    const chatHub = window.chatHub;
    let currentChat = window.currentChat;
    let aiChatHistory = [];
    if (!currentUsername) {
        console.warn('User not authenticated - missing currentUsername');
        return;
    }
    const uploadUrl = urls.uploadFiles;
    const $menu = $('#attachment-menu');
    const $toggleBtn = $('#toggle-attach-menu');

    $toggleBtn.on('click', function (e) {
        e.stopPropagation();
        const btnRect = $toggleBtn[0].getBoundingClientRect();
        $menu.css({
            display: 'block',
            bottom: (window.innerHeight - btnRect.top) + 'px',
            left: (btnRect.left) + 'px'
        });
    });
    $(window).on('click', function () {
        $menu.css('display', 'none');
    });
    $('#send-image-btn').on('click', function (e) {
        e.preventDefault();
        $menu.css('display', 'none');
        $('#imageUploadInput').click();
    });
    $('#send-file-btn').on('click', function (e) {
        e.preventDefault();
        $menu.css('display', 'none');
        $('#fileUploadInput').click();
    });
    // File Upload Preview (giữ nguyên)
    $('#imageUploadInput, #fileUploadInput').on("change", function () {
        const files = this.files;
        const container = $("#imagePreviewContainer");
        container.empty();
        if (files.length > 0) {
            Array.from(files).forEach(file => {
                const reader = new FileReader();
                reader.onload = function (e) {
                    let preview;
                    if (file.type.startsWith("image/")) {
                        preview = `<img src="${e.target.result}" class="img-fluid rounded" style="width:120px;height:120px;object-fit:cover;">`;
                    } else if (file.type.startsWith("video/")) {
                        preview = `<video src="${e.target.result}" controls style="width:120px;height:120px;"></video>`;
                    } else {
                        preview = `<div style="width:120px;height:120px;border:1px solid #ccc;display:flex;align-items:center;justify-content:center;">📄 ${file.name}</div>`;
                    }
                    container.append(preview);
                };
                reader.readAsDataURL(file);
            });
            $("#imagePreviewModal").modal("show");
        }
        $(this).val(null);
    });
    // Send Files (giữ nguyên)
    $("#sendImageButton").click(function () {
        const imageFiles = $("#imageUploadInput")[0].files;
        const otherFiles = $("#fileUploadInput")[0].files;
        let filesToUpload = (imageFiles.length > 0) ? imageFiles : otherFiles;
        if (filesToUpload.length === 0) return;
        const formData = new FormData();
        for (let i = 0; i < filesToUpload.length; i++) {
            formData.append("file" + i, filesToUpload[i]);
        }
        $.ajax({
            url: uploadUrl,
            type: "POST",
            data: formData,
            processData: false,
            contentType: false,
            success: function (res) {
                if (res.success) {
                    res.urls.forEach(url => {
                        const ext = url.split(".").pop().toLowerCase();
                        let type = "file";
                        if (["jpg", "jpeg", "png", "gif"].includes(ext)) type = "image";
                        else if (["mp4", "mov", "webm"].includes(ext)) type = "video";
                        const msgJson = JSON.stringify({ type, content: url });
                        if (window.currentChat.mode === "private") {
                            window.chatHub.server.sendPrivateMessage(window.currentChat.partnerUsername, msgJson);
                        } else if (window.currentChat.mode === "ai") {
                            window.chatHub.server.sendMessageToAI(`[${type}] ${url}`);
                        }
                    });
                } else {
                    alert("Lỗi upload: " + res.message);
                }
            },
            complete: function () {
                $("#imagePreviewModal").modal("hide");
                $("#imageUploadInput").val(null);
                $("#fileUploadInput").val(null);
            }
        });
    });
    $('body').on('click', '#user-chat-header', function () {
        // Chỉ thực thi nếu đang chat private và có username
        if (window.currentChat.mode === 'private' && window.currentChat.partnerUsername) {

            const partnerUsername = window.currentChat.partnerUsername;

            // Hiển thị trạng thái "Đang tải..."
            $('#partner-modal-display-name').text('Đang tải...');
            $('#partner-modal-username').text('');
            $('#partner-modal-avatar').attr('src', '/Content/default-avatar.png');
            $('#partner-modal-cover').css('background-image', 'url()');
            $('#partner-modal-cover').css('background-color', '#e0e0e0');

            // Set text "Đang tải..." cho các trường mới
            $('#partner-modal-gender').text('Đang tải...');
            $('#partner-modal-dob').text('Đang tải...');
            $('#partner-modal-phone').text('Đang tải...');
            $('#partner-modal-email').text('Đang tải...');
            $('#partner-modal-bio').text('Đang tải...');

            // Ẩn nút xóa bạn
            $('#partner-unfriend-form').hide();

            $('#partnerProfileModal').modal('show');

            // Gọi AJAX để lấy thông tin
            $.getJSON(`/Profile/GetUserPublicProfile?username=${partnerUsername}`, function (response) {
                if (response.success && response.user) {
                    const user = response.user;

                    // Cập nhật thông tin lên modal
                    $('#partner-modal-display-name').text(user.DisplayName || 'Không có tên');
                    $('#partner-modal-username').text(`@@${user.Username}`);

                    const avatarUrl = user.AvatarUrl ? user.AvatarUrl : '/Content/default-avatar.png';
                    $('#partner-modal-avatar').attr('src', avatarUrl);

                    if (user.CoverUrl) {
                        $('#partner-modal-cover').css('background-image', `url(${user.CoverUrl})`);
                    }

                    $('#partner-modal-gender').text(user.Gender || 'Chưa cập nhật');
                    $('#partner-modal-phone').text(user.PhoneNumber || 'Chưa cập nhật'); // Đã mask từ C#
                    $('#partner-modal-email').text(user.Email || 'Chưa cập nhật'); // Đã mask từ C#
                    $('#partner-modal-bio').text(user.Bio || 'Không có tiểu sử.');

                    // Format ngày sinh (chỉ hiển thị nếu có)
                    if (user.DateOfBirth) {
                        try {
                            const dob = new Date(user.DateOfBirth); // Parse chuỗi ISO
                            const day = dob.getDate();
                            const month = dob.getMonth() + 1; // JS month 0-11
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
                    } else {
                        $('#partner-unfriend-form').hide();
                    }

                } else {
                    // Xử lý lỗi
                    $('#partner-modal-display-name').text(response.message || 'Không tìm thấy người dùng');
                    setTimeout(function () {
                        $('#partnerProfileModal').modal('hide');
                    }, 2000);
                }
            }).fail(function () {
                // Xử lý lỗi AJAX
                $('#partner-modal-display-name').text('Lỗi kết nối máy chủ');
                setTimeout(function () {
                    $('#partnerProfileModal').modal('hide');
                }, 2000);
            });
        }
    });
    // ========================================================
    // JS CHO SIDEBAR THÔNG TIN HỘI THOẠI
    // ========================================================

    $('body').on('click', '#toggle-info-sidebar-btn', function () {
        var $sidebar = $('#conversation-info-sidebar');
        var $button = $(this);

        // Toggle (đóng/mở)
        $sidebar.toggle();
        $button.toggleClass('active');

        if ($sidebar.is(':visible') && window.currentChat.mode === 'private') {
            loadConversationInfo(window.currentChat.partnerUsername);
        }
    });

    $('body').on('click', '#close-info-sidebar-btn', function () {
        $('#conversation-info-sidebar').hide();
        $('#toggle-info-sidebar-btn').removeClass('active');
    });

    function loadConversationInfo(partnerUsername) {
        // Cập nhật tên và avatar (lấy từ header có sẵn)
        var avatarSrc = $('#chat-header-avatar').attr('src');
        var displayName = $('#chat-header-displayname').text();
        $('#info-sidebar-avatar').attr('src', avatarSrc);
        $('#info-sidebar-displayname').text(displayName);

        var $filesList = $('#info-sidebar-files-list');
        var $imagesList = $('#info-sidebar-images-list');
        $filesList.html('<p class="text-muted text-center small p-3">Đang tải file...</p>');
        $imagesList.html('<p class="text-muted text-center small p-3">Đang tải ảnh...</p>');

        $.getJSON(urls.getConversationInfo, { partnerUsername: partnerUsername }, function (data) {
            if (data.success) {

                // Xử lý Ảnh/Video
                if (data.images && data.images.length > 0) {
                    $imagesList.empty(); // Xóa "Đang tải..."
                    data.images.forEach(function (img) {
                        var imgHtml = `
                                <a href="${img.Url}" target="_blank" class="info-image-item">
                                    <img src="${img.Url}" alt="Ảnh" />
                                </a>`;
                        $imagesList.append(imgHtml);
                    });
                } else {
                    $imagesList.html('<p class="text-muted text-center small p-3">Chưa có ảnh/video nào.</p>');
                }

                // Xử lý File
                if (data.files && data.files.length > 0) {
                    $filesList.empty(); // Xóa "Đang tải..."
                    data.files.forEach(function (file) {
                        var fileHtml = `
                                <a href="${file.Url}" target="_blank" class="info-file-item">
                                    <i class="fas fa-file-word file-icon"></i>
                                    <div class="file-info">
                                        <div class="file-name" title="${file.FileName}">${file.FileName}</div>
                                        <div class="file-meta">${file.Timestamp}</div>
                                    </div>
                                </a>`;
                        $filesList.append(fileHtml);
                    });
                } else {
                    $filesList.html('<p class="text-muted text-center small p-3">Chưa có file nào.</p>');
                }
            }
        });
    }

    function getHiddenChats() {
        var hidden = localStorage.getItem('hiddenChats');
        return hidden ? JSON.parse(hidden) : [];
    }

    function setHiddenChats(chatsArray) {
        localStorage.setItem('hiddenChats', JSON.stringify(chatsArray));
    }

    function hideConversationInList(username, shouldHide) {
        var $friendItem = $('.conversation-list .friend-item[data-username="' + username + '"]');
        if (shouldHide) {
            $friendItem.hide();
        } else {
            $friendItem.show();
        }
    }
    $('body').on('submit', '#partner-unfriend-form', function (e) {
        const partnerName = $('#partner-modal-display-name').text();
        if (!confirm(`Bạn có chắc muốn xóa '${partnerName}' khỏi danh sách bạn bè? Thao tác này không thể hoàn tác.`)) {
            e.preventDefault();
        }
    });

    $('body').on('click', '#partner-action-block', function (e) {
        e.preventDefault();
        const partnerName = $('#partner-modal-display-name').text();
        alert(`Chức năng Chặn '${partnerName}' (Đang phát triển)`);
    });

    $('body').on('click', '#partner-action-report', function (e) {
        e.preventDefault();
        const partnerName = $('#partner-modal-display-name').text();
        alert(`Chức năng Báo xấu '${partnerName}' (Đang phát triển)`);
    });
    // Function update timestamp bar
    function updateTimestampBar() {
        const now = new Date();
        const vietTime = now.toLocaleDateString('vi-VN', {
            weekday: 'short',
            hour: '2-digit',
            minute: '2-digit'
        }).replace(' ', ' lúc ');
        $('#current-timestamp').text(vietTime);
    }
    function sendTextMessage(e) {
        if (e) e.preventDefault();
        const messageContent = $('#messageInput').val().trim();
        if (messageContent === '') return;

        // FIX TIMESTAMP: Format chuẩn
        const now = new Date();
        const vietTime = now.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });

        // FIX: Đưa vietTime VÀO TRONG bubble, dùng class 'bubble-time'
        const escapedContent = $('<div/>').text(messageContent).html();
        let selfMessageHtml = `
                <div class="chat-message self" data-timestamp="${now.toISOString()}">
                    <div class="chat-bubble">
                        <span>${escapedContent}</span>
                        <span class="bubble-time">${vietTime}</span>
                    </div>
                </div>`;

        const messagesList = $('#messagesList');
        messagesList.append(selfMessageHtml);
        messagesList.scrollTop(messagesList[0].scrollHeight);

        // Ẩn welcome & show area (giữ nguyên, nhưng force cho private)
        $('#ai-welcome-screen').hide();
        $('.message-area').show();
        if (currentChat.mode === 'ai') {
            $('.chat-header').hide();
            $('#user-chat-header').hide();
            $('#user-chat-buttons').hide();
        } else {
            $('#private-chat-header').show();  // FORCE HEADER PRIVATE NẾU MODE PRIVATE
        }

        // Gửi đến server
        if (currentChat.mode === 'ai') {
            console.log('Sending to AI:', messageContent);
            chatHub.server.sendMessageToAI(messageContent);
        } else if (currentChat.mode === 'private') {
            const msgJson = JSON.stringify({ type: "text", content: messageContent });
            chatHub.server.sendPrivateMessage(currentChat.partnerUsername, msgJson);
        }
        if (currentChat.mode === 'private') {
            localStorage.setItem('lastChatPartner', currentChat.partnerUsername);
        }
        $('#messageInput').val('').focus();
        // Update bar
        updateTimestampBar();
    }
    function switchChat(target) {
        $('#messagesList').empty();
        currentChat.mode = $(target).data('chat-mode');
        $('.conversation-list .list-group-item-action').removeClass('active');
        $(target).addClass('active');

        if (currentChat.mode === 'ai') {
            $('#ai-chat-header').show();
            $('#private-chat-header').hide();
            $('#chat-timestamp-bar').show();
            $('#messageInput').attr('placeholder', 'Hỏi tôi bất cứ điều gì...?');
            updateTimestampBar();
            $('#ai-welcome-screen').show();
            $('.message-area').hide();
            $('#info-action-hide-chat').prop('checked', false).prop('disabled', true);
            currentChat.partnerUsername = null;
        } else {  // Private mode
            $('#private-chat-header').show();
            $('#ai-chat-header').hide();
            $('#chat-timestamp-bar').show();
            $('#messageInput').attr('placeholder', 'Nhập tin nhắn...');
            $('#user-chat-header').show();
            $('#user-chat-buttons').show();

            // Lấy partnerUsername TỪ target
            currentChat.partnerUsername = $(target).data('username');

            const hiddenChats = getHiddenChats();

            if (hiddenChats.includes(currentChat.partnerUsername)) {
                $('#info-action-hide-chat').prop('checked', true);
            } else {
                $('#info-action-hide-chat').prop('checked', false);
            }
            $('#info-action-hide-chat').prop('disabled', false);

            $('#ai-welcome-screen').hide();
            $('.message-area').show();

            const displayName = $(target).find('strong').text().trim();
            const avatarSrc = $(target).data('avatar-url') || '/Content/default-avatar.png';
            $('#chat-header-displayname').text(displayName);
            $('#chat-header-avatar').attr('src', avatarSrc);
            $('#chat-header-status').text('Đang hoạt động');

            if (chatHub.server.joinPrivateGroup) {
                chatHub.server.joinPrivateGroup(currentChat.partnerUsername);
            }

            $.getJSON(urls.getChatHistory, { partnerUsername: currentChat.partnerUsername }, function (response) {
                if (response.success) {
                    $('#messagesList').empty();
                    response.messages.forEach(msg => {
                        const isSelf = msg.SenderUsername === currentUsername;
                        let firstLetter = isSelf ? '' : msg.SenderUsername.charAt(0).toUpperCase();

                        let vietTime;
                        try {
                            const msgDate = new Date(msg.Timestamp);
                            if (isNaN(msgDate.getTime())) {
                                vietTime = 'Vừa xong';
                            } else {
                                vietTime = msgDate.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
                            }
                        } catch (e) {
                            vietTime = 'Lỗi thời gian';
                        }

                        // ==================================================
                        // ===== FIX LỖI CÚ PHÁP '...' (switchChat) =====
                        // ==================================================
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

                        const messageBodyHtml = `
                                <div class="chat-bubble">
                                    ${bubbleContentHtml}
                                    <span class="bubble-time">${vietTime}</span>
                                </div>`;

                        const messageHtml = `
                                <div class="chat-message ${isSelf ? 'self' : 'other'}" data-timestamp="${msg.Timestamp}">
                                    ${!isSelf ? `<div class="avatar" title="${msg.SenderUsername}">${firstLetter}</div>` : ''}
                                    ${messageBodyHtml}
                                </div>`;
                        $('#messagesList').append(messageHtml);
                    });
                    $('#messagesList').scrollTop($('#messagesList')[0].scrollHeight);
                }
            });
        }
        if ($(window).width() < 768) {
            $('.conversation-list').hide();
        }
    }
    // ✅ FIXED VERSION - Di chuyển hideConversationInList() sau append()
    function loadFriendsList() {
        const container = $('#conversation-list-ul');
        container.find('.friend-item').remove();
        const hiddenChats = getHiddenChats();

        console.log('📞 Loading friends from:', urls.getFriendsList);

        $.getJSON(urls.getFriendsList, function (friends) {
            console.log('✅ Received friends:', friends.length, friends);

            if (!friends || friends.length === 0) {
                console.warn('No friends returned from API');
                return;
            }

            friends.forEach(function (friend) {
                if (friend.Username === currentUsername) {
                    return;
                }

                const friendHtml = `
                    <a href="#" class="list-group-item list-group-item-action friend-item"
                       data-chat-mode="private"
                       data-username="${friend.Username}"
                       data-userid="${friend.Id}"
                       data-avatar-url="${friend.AvatarUrl || ''}">
                        <strong><i class="fas fa-user"></i> ${friend.DisplayName}</strong>
                    </a>`;

                container.append(friendHtml);

                if (hiddenChats.includes(friend.Username)) {
                    hideConversationInList(friend.Username, true);
                }
            });

            const urlParams = new URLSearchParams(window.location.search);
            const friendUsername = urlParams.get('friendUsername');
            const lastPartner = localStorage.getItem('lastChatPartner');
            const targetUsername = friendUsername || lastPartner;

            let target;
            if (targetUsername) {
                target = container.find(`.list-group-item-action[data-username='${targetUsername}']`);
            }
            if (!target || target.length === 0) {
                target = $('#ai-chat-btn');
            }
            switchChat(target);
        })
            .fail(function (jqXHR, status, error) {
                console.error('❌ loadFriendsList failed:', {
                    status: status,
                    error: error,
                    responseText: jqXHR.responseText,
                    url: urls.getFriendsList
                });
                alert('Không thể tải danh sách bạn bè!\nLỗi: ' + status);
            });
    }
    if (chatHub && chatHub.client) {
        // SỬA CHỮ KÝ HÀM: Dùng đúng 4 tham số mà Hub của bạn gửi
        chatHub.client.receiveMessage = function (senderUsername, senderAvatar, messageJson, timestamp) {

            // 1. Bỏ qua tin nhắn của chính mình
            if (senderUsername === currentUsername) {
                return;
            }

            // ==================================================
            // ===== FIX LỖI CÚ PHÁP '...' (AI) =====
            // ==================================================
            if (senderUsername === 'AI Assistant') {
                if (currentChat.mode === 'ai') {
                    $('#ai-welcome-screen').hide();
                    $('.message-area').show();

                    let msgObj;
                    try {
                        msgObj = JSON.parse(messageJson);
                    } catch (e) {
                        msgObj = { type: "text", content: messageJson };
                    }

                    const msgDate = timestamp ? new Date(timestamp) : new Date();
                    const vietTime = msgDate.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });

                    let bubbleHtml = "";
                    if (msgObj.type === "image") {
                        bubbleHtml = `<img src="${msgObj.content}" class="img-fluid rounded" style="max-width: 250px;" onclick="window.open(this.src, '_blank');" />`;
                    } else if (msgObj.type === "video") {
                        bubbleHtml = `<video controls src="${msgObj.content}" style="max-width: 300px; border-radius: 10px;"></video>`;
                    } else if (msgObj.type === "file") {
                        bubbleHtml = `
                                <a href="${msgObj.content}" target="_blank" style="display:flex; align-items:center; padding:8px 12px; background:#f0f0f0; border-radius:8px; text-decoration:none; color:#333;">
                                    <i class="fas fa-file-alt" style="font-size:1.5rem; margin-right:10px; color:#007bff;"></i>
                                    <div>
                                        <div style="font-weight:600; font-size:0.9rem;">${msgObj.fileName || 'File'}</div>
                                        <div style="font-size:0.75rem; color:#666;">${msgObj.fileSize || ''}</div>
                                    </div>
                                </a>`;
                    }
                    else if (msgObj.type === "call_log") {
                        bubbleHtml = createCallLogHtml(msgObj);
                    }
                    else {
                        const escaped = $('<div/>').text(msgObj.content).html();
                        bubbleHtml = `<span>${escaped}</span>`;
                    }

                    const aiMessageHtml = `
                        <div class="chat-message other ai-message" data-timestamp="${msgDate.toISOString()}">
                            <div class="avatar" title="AI Assistant">🤖</div>
                            <div class="chat-bubble ai-bubble">
                                ${bubbleHtml}
                                <span class="bubble-time">${vietTime}</span>
                            </div>
                        </div>`;

                    $('#messagesList').append(aiMessageHtml);
                    $('#messagesList').scrollTop($('#messagesList')[0].scrollHeight);

                    // Phát âm thanh
                    if (localStorage.getItem('playSounds') !== 'false') {
                        var sound = document.getElementById('notification-sound');
                        if (sound) sound.play().catch(e => { });
                    }
                }
                return; // Dừng lại sau khi xử lý AI
            }

            // ====== XỬ LÝ TIN NHẮN PRIVATE (ĐÃ SỬA) ======
            if (currentChat.mode !== 'private' || currentChat.partnerUsername !== senderUsername) {
                console.log(`Received from ${senderUsername}, but not in that chat window.`);

                // Phát âm thanh (tin nhắn mới ở cửa sổ khác)
                if (localStorage.getItem('playSounds') !== 'false') {
                    var sound = document.getElementById('notification-sound');
                    if (sound) sound.play().catch(e => { });
                }
                return; // Không hiển thị
            }

            // Parse JSON message
            let msgObj;
            try {
                msgObj = JSON.parse(messageJson);
            } catch (e) {
                msgObj = { type: "text", content: messageJson };
            }

            const msgDate = timestamp ? new Date(timestamp) : new Date();
            const vietTime = msgDate.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });

            // Tạo HTML theo type
            let bubbleHtml = "";
            if (msgObj.type === "image") {
                bubbleHtml = `<img src="${msgObj.content}" class="img-fluid rounded" style="max-width: 250px; cursor: pointer;" onclick="window.open(this.src, '_blank');" />`;
            } else if (msgObj.type === "video") {
                bubbleHtml = `<video controls src="${msgObj.content}" style="max-width: 300px; border-radius: 10px;"></video>`;
            } else if (msgObj.type === "file") {
                bubbleHtml = `
                        <a href="${msgObj.content}" target="_blank" style="display:flex; align-items:center; padding:8px 12px; background:#f0f0f0; border-radius:8px; text-decoration:none; color:#333;">
                            <i class="fas fa-file-alt" style="font-size:1.5rem; margin-right:10px; color:#007bff;"></i>
                            <div>
                                <div style="font-weight:600; font-size:0.9rem;">${msgObj.fileName || 'File'}</div>
                                <div style="font-size:0.75rem; color:#666;">${msgObj.fileSize || ''}</div>
                            </div>
                        </a>`;
            } else {
                const escaped = $('<div/>').text(msgObj.content).html();
                bubbleHtml = `<span>${escaped}</span>`;
            }

            const firstLetter = senderUsername.charAt(0).toUpperCase();
            const messageHtml = `
                    <div class="chat-message other" data-timestamp="${msgDate.toISOString()}">
                        <div class="avatar" title="${senderUsername}">${firstLetter}</div>
                        <div class="chat-bubble">
                            ${bubbleHtml}
                            <span class="bubble-time">${vietTime}</span>
                        </div>
                    </div>`;

            $('#messagesList').append(messageHtml);
            $('#messagesList').scrollTop($('#messagesList')[0].scrollHeight);
            updateTimestampBar();

            // Phát âm thanh (tin nhắn mới ở cửa sổ hiện tại)
            if (localStorage.getItem('playSounds') !== 'false') {
                var sound = document.getElementById('notification-sound');
                if (sound) sound.play().catch(e => { });
            }
        };

        // ========================================================
        // ===== THÊM CÁC HÀM NHẬN CUỘC GỌI VÀO ĐÂY =====
        // ========================================================
        let pendingOffer = null;
        let pendingCaller = null;

        // 1. Nhận được Offer
        window.chatHub.client.receiveCallOffer = function (callerUsername, offerSdp, callType) {
            if (isCallInProgress) {
                console.log('Busy, ignoring incoming call from ' + callerUsername);
                // (Bạn có thể gửi tín hiệu 'busy' về đây)
                return;
            }
            console.log(`Incoming ${callType} call from ${callerUsername}`);
            pendingOffer = JSON.parse(offerSdp);
            pendingCaller = callerUsername;

            const avatar = $(`.friend-item[data-username="${callerUsername}"]`).data('avatar-url') || '/Content/default-avatar.png';
            const displayName = $(`.friend-item[data-username="${callerUsername}"]`).find('strong').text().trim() || callerUsername;

            $('#incoming-call-avatar').attr('src', avatar);
            $('#incoming-call-name').text(displayName);
            $('#incoming-call-type').text(callType === 'video' ? 'Đang gọi video...' : 'Đang gọi thoại...');
            $('#incomingCallModal').modal('show');
        };

        // 2. Nhận được Answer
        window.chatHub.client.receiveCallAnswer = async function (calleeUsername, answerSdp) {
            if (!peerConnection || !isCallInProgress) return;
            console.log('Received call answer from ' + calleeUsername);
            try {
                const answer = JSON.parse(answerSdp);
                await peerConnection.setRemoteDescription(new RTCSessionDescription(answer));
                callStartTime = new Date();
                $('#call-view-status').text('Đã kết nối');
            } catch (e) {
                console.error('Error setting remote description', e);
            }
        };

        // 3. Nhận được ICE Candidate
        window.chatHub.client.receiveIceCandidate = async function (senderUsername, candidate) {
            if (!peerConnection) return;
            try {
                const iceCandidate = JSON.parse(candidate);
                await peerConnection.addIceCandidate(new RTCIceCandidate(iceCandidate));
            } catch (e) {
                console.error('Error adding received ICE candidate', e);
            }
        };

        // 4. Nhận được tín hiệu Kết thúc cuộc gọi
        window.chatHub.client.callEnded = function (senderUsername) {
            console.log('Call ended by ' + senderUsername);
            hangUp(false); // Dọn dẹp mà không gửi log (vì bên kia đã gửi)
        };

    } 
    // ========================================================
    // TÌM KIẾM
    // ========================================================

    $('body').on('click', '#toggle-search-sidebar-btn', function () {
        // Đóng sidebar thông tin (nếu đang mở)
        $('#conversation-info-sidebar').hide();
        $('#toggle-info-sidebar-btn').removeClass('active');

        // Mở sidebar tìm kiếm
        $('#search-sidebar').show();
        $(this).addClass('active');
        $('#search-input').focus();
    });

    // 2. Nút đóng Sidebar (bên trong sidebar)
    $('body').on('click', '#close-search-sidebar-btn', function () {
        $('#search-sidebar').hide();
        $('#toggle-search-sidebar-btn').removeClass('active');

        // Khi đóng, xóa highlight và hiện lại tất cả tin nhắn
        $('#messagesList .chat-message').show();
        clearHighlights();
    });

    // 3. Hàm tìm kiếm khi gõ phím
    $('#search-input').on('keyup', function () {
        var searchTerm = $(this).val().toLowerCase().trim();
        var $resultsList = $('#search-results-list');
        var $welcomeScreen = $('#search-welcome');

        clearHighlights(); // Xóa highlight cũ
        $resultsList.empty(); // Xóa kết quả cũ

        if (searchTerm === "") {
            // Nếu không có từ khóa
            $welcomeScreen.show();
            $resultsList.hide();
            $('#messagesList .chat-message').show(); // Hiện lại tất cả
            return;
        }

        // Nếu có từ khóa
        $welcomeScreen.hide();
        $resultsList.show();

        var resultCount = 0;
        var partnerName = $('#chat-header-displayname').text();
        var partnerAvatar = $('#chat-header-avatar').attr('src');
        // Lấy avatar của user (từ _ChatLayout)
        var selfAvatar = $('#layout-user-avatar-img').attr('src') || '/Content/default-avatar.png';

        // Lặp qua tất cả tin nhắn trong cửa sổ chat
        $('#messagesList .chat-message').each(function () {
            var $message = $(this);
            var $bubble = $message.find('.chat-bubble span').first(); // Chỉ lấy span chứa text
            if ($bubble.length === 0) return; // Bỏ qua nếu là ảnh/video

            var messageText = $bubble.text().toLowerCase();

            if (messageText.includes(searchTerm)) {
                // TÌM THẤY
                resultCount++;
                $message.show(); // Hiển thị tin nhắn

                // Highlight từ khóa
                var originalText = $bubble.text();
                var highlightedText = originalText.replace(new RegExp(searchTerm, 'gi'), '<span class="highlight">$&</span>');
                $bubble.html(highlightedText);

                // Lấy thông tin để tạo kết quả
                var senderName = $message.hasClass('self') ? "Bạn" : partnerName;
                var senderAvatar = $message.hasClass('self') ? selfAvatar : partnerAvatar;
                var timestamp = $message.data('timestamp'); // Lấy timestamp ta đã thêm
                var timeText = $message.find('.bubble-time').text(); // Lấy "12:00"

                // Tạo HTML cho kết quả
                var resultHtml = `
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
                // KHÔNG TÌM THẤY
                $message.hide(); // Ẩn tin nhắn
            }
        });

        if (resultCount === 0) {
            $resultsList.html('<p class="text-muted text-center small p-3">Không tìm thấy kết quả nào.</p>');
        }
    });

    // 4. Hàm xóa highlight
    function clearHighlights() {
        $('#messagesList .chat-message').find('.highlight').each(function () {
            var $parent = $(this).parent();
            $parent.html($parent.text()); // Chuyển HTML (có span) về text
        });
    }

    $('#search-results-list').on('click', '.search-result-item', function (e) {
        e.preventDefault();
        var targetTimestamp = $(this).data('scroll-to');
        var $targetMessage = $(`#messagesList .chat-message[data-timestamp="${targetTimestamp}"]`);

        if ($targetMessage.length > 0) {
            // Cuộn đến tin nhắn và highlight nó
            $('#messagesList').scrollTop($('#messagesList').scrollTop() + $targetMessage.position().top - 50); // Cuộn

            // Nhấp nháy highlight
            $targetMessage.css('transition', 'background-color 0.2s');
            $targetMessage.css('background-color', '#fffb8f');
            setTimeout(function () {
                $targetMessage.css('background-color', '');
            }, 1000);
        }
    });
    // Event Handlers
    $('.conversation-list').on('click', '.list-group-item-action', function (e) {
        e.preventDefault();
        switchChat(this);
    });
    $('body').off('click', '#sendButton').on('click', '#sendButton', function (e) {
        e.preventDefault();
        sendTextMessage(e);
    });
    $('body').off('keypress', '#messageInput').on('keypress', '#messageInput', function (e) {
        if (e.which === 13 && !e.shiftKey) {
            e.preventDefault();
            sendTextMessage(e);
        }
    });
    $('body').on('click', '#toggle-conversations-btn', function (e) {
        e.preventDefault();
        $('.conversation-list').toggle();
    });
    $('#quick-image-btn').on('click', function (e) {
        e.preventDefault();
        $('#imageUploadInput').click();
    });
    $('body').on('click', '.ai-prompt-btn', function () {
        const promptText = $(this).data('prompt');
        $('#messageInput').val(promptText);
        sendTextMessage(null);
    });
    // AI Back & Info
    $('#ai-back-btn').on('click', function () {
        $('.conversation-list').toggle();
    });
    $('#ai-info-btn').on('click', function () {
        alert('Info về Meta AI: Powered by Llama 4!');
    });
    $('body').on('change', '#info-action-hide-chat', function () {
        if (window.currentChat.mode !== 'private' || $(this).is(':disabled')) return;

        var isChecked = $(this).is(':checked');
        var partnerUsername = window.currentChat.partnerUsername;
        var hiddenChats = getHiddenChats();

        if (isChecked) {
            if (!hiddenChats.includes(partnerUsername)) {
                hiddenChats.push(partnerUsername);
            }
            hideConversationInList(partnerUsername, true);
        } else {
            hiddenChats = hiddenChats.filter(u => u !== partnerUsername);
            hideConversationInList(partnerUsername, false);
        }
        setHiddenChats(hiddenChats);
    });

    $('body').on('click', '#info-action-report', function (e) {
        e.preventDefault();
        const partnerName = $('#info-sidebar-displayname').text();
        alert(`Chức năng Báo xấu '${partnerName}' (Đang phát triển)`);
    });

    $('body').on('click', '#info-action-clear-history', function (e) {
        e.preventDefault();
        if (window.currentChat.mode !== 'private') return;

        const partnerUsername = window.currentChat.partnerUsername;
        const partnerName = $('#info-sidebar-displayname').text();

        if (confirm(`Bạn có chắc muốn xóa TOÀN BỘ lịch sử trò chuyện với '${partnerName}'? Thao tác này không thể hoàn tác.`)) {

            $.ajax({
                url: urls.clearHistory,
                type: 'POST',
                data: {
                    __RequestVerificationToken: antiForgeryToken,
                    partnerUsername: partnerUsername
                },
                success: function (response) {
                    if (response.success) {
                        $('#messagesList').empty();

                        if ($('#conversation-info-sidebar').is(':visible')) {
                            loadConversationInfo(partnerUsername);
                        }
                        alert('Đã xóa lịch sử trò chuyện.');
                    } else {
                        alert('Lỗi: ' + response.message);
                    }
                },
                error: function () {
                    alert('Lỗi kết nối máy chủ. Không thể xóa.');
                }
            });
        }
    });
    // ========================================================
    // BIẾN TOÀN CỤC CHO WEB-RTC
    // ========================================================
    let localStream;
    let remoteStream;
    let peerConnection;
    let isCallInProgress = false;
    let currentCallPartner = null;
    let currentCallType = 'voice'; // 'voice' or 'video'
    let isMicOn = true;
    let isVideoOn = true;
    let callStartTime = null;

    // Cấu hình STUN Server (dùng của Google, miễn phí)
    const configuration = {
        'iceServers': [
            { 'urls': 'stun:stun.l.google.com:19302' }
        ]
    };

    // ========================================================
    // HÀM HELPER CHO WEB-RTC
    // ========================================================

    // 1. Hàm khởi tạo PeerConnection
    function createPeerConnection() {
        if (peerConnection) {
            peerConnection.close();
        }
        peerConnection = new RTCPeerConnection(configuration);

        // Thêm local stream vào connection
        if (localStream) {
            localStream.getTracks().forEach(track => {
                peerConnection.addTrack(track, localStream);
            });
        }

        // A. Khi nhận được ICE candidate
        peerConnection.onicecandidate = (event) => {
            if (event.candidate && currentCallPartner) {
                console.log('Sending ICE candidate to ' + currentCallPartner);
                window.chatHub.server.sendIceCandidate(currentCallPartner, JSON.stringify(event.candidate));
            }
        };

        // B. Khi nhận được track (stream) từ người kia
        peerConnection.ontrack = (event) => {
            console.log('Received remote track');
            if (!remoteStream) {
                remoteStream = new MediaStream();
            }

            event.streams[0].getTracks().forEach(track => {
                remoteStream.addTrack(track);
            });

            $('#remoteVideo').get(0).srcObject = remoteStream;
            $('#call-info-overlay').hide(); // Ẩn avatar/tên
        };
    }

    // 2. Hàm lấy Camera/Mic
    async function startMedia(callType) {
        try {
            currentCallType = callType;
            const constraints = {
                audio: true,
                video: callType === 'video'
            };
            localStream = await navigator.mediaDevices.getUserMedia(constraints);
            $('#localVideo').get(0).srcObject = localStream;
            isVideoOn = callType === 'video';
            isMicOn = true;
        } catch (e) {
            console.error('Error getting user media!', e);
            alert('Không thể truy cập camera hoặc mic. Vui lòng cấp quyền.');
            throw e; // Dừng tiến trình gọi
        }
    }

    // 3. Hàm hiển thị giao diện gọi
    function showCallView(partnerUsername, callType) {
        isCallInProgress = true;
        currentCallPartner = partnerUsername;
        currentCallType = callType;

        // Cập nhật thông tin trên modal gọi
        const avatar = $(`.friend-item[data-username="${partnerUsername}"]`).data('avatar-url') || '/Content/default-avatar.png';
        const displayName = $(`.friend-item[data-username="${partnerUsername}"]`).find('strong').text().trim();

        $('#call-view-avatar').attr('src', avatar);
        $('#call-view-name').text(displayName);
        $('#call-view-status').text(callType === 'video' ? 'Cuộc gọi video' : 'Cuộc gọi thoại');

        $('#call-info-overlay').show();
        $('#remoteVideo').get(0).srcObject = null; // Reset video
        $('#localVideo').get(0).srcObject = localStream;

        // Ẩn/hiện nút video
        $('#toggle-video-btn').toggle(callType === 'video');
        $('body').on('click', '.call-back-btn', function () {
            if (window.currentChat.mode !== 'private' || !window.currentChat.partnerUsername) {
                alert('Không thể gọi lại từ cửa sổ này.');
                return;
            }
            const callType = $(this).data('call-type'); // 'voice' or 'video'
            startCall(callType);
        });

        // Reset icon nút
        $('#toggle-mic-btn').html('<i class="fas fa-microphone"></i>');
        $('#toggle-video-btn').html('<i class="fas fa-video"></i>');

        $('#call-view').show();
    }

    // 4. Hàm kết thúc cuộc gọi (dọn dẹp)
    function hangUp(isInitiator = false) {

        if (isInitiator && window.currentChat.mode === 'private' && currentCallPartner) {
            let status = 'missed'; // Mặc định là lỡ
            let durationSeconds = 0;

            if (callStartTime) { // Nếu cuộc gọi đã thực sự bắt đầu
                const durationMs = new Date() - callStartTime;
                durationSeconds = Math.round(durationMs / 1000);
                status = 'completed'; // Đã hoàn thành
            }
            const logMessage = {
                type: "call_log",
                status: status,
                duration: durationSeconds,
                callType: currentCallType // 'voice' or 'video'
            };
            window.chatHub.server.sendPrivateMessage(currentCallPartner, JSON.stringify(logMessage));
        }

        if (peerConnection) {
            peerConnection.close();
            peerConnection = null;
        }
        if (localStream) {
            localStream.getTracks().forEach(track => track.stop());
            localStream = null;
        }
        if (remoteStream) {
            remoteStream.getTracks().forEach(track => track.stop());
            remoteStream = null;
        }

        $('#call-view').hide();
        $('#incomingCallModal').modal('hide');

        callStartTime = null;

        if (isCallInProgress && currentCallPartner && isInitiator) {
            window.chatHub.server.endCall(currentCallPartner);
        }

        isCallInProgress = false;
        currentCallPartner = null;
        console.log('Call ended.');
    }


    // ========================================================
    // LOGIC: BẮT ĐẦU CUỘC GỌI (CALLER)
    // ========================================================
    async function startCall(callType) {
        const partnerUsername = window.currentChat.partnerUsername;
        if (!partnerUsername) return alert('Lỗi: Không tìm thấy người nhận.');
        if (isCallInProgress) return alert('Đang trong một cuộc gọi khác.');

        console.log(`Starting ${callType} call with ${partnerUsername}`);
        currentCallPartner = partnerUsername;

        try {
            // 1. Lấy media
            await startMedia(callType);

            // 2. Hiển thị UI
            showCallView(partnerUsername, callType);
            $('#call-view-status').text('Đang gọi...');

            // 3. Tạo Peer Connection
            createPeerConnection();

            // 4. Tạo Offer
            const offer = await peerConnection.createOffer();
            await peerConnection.setLocalDescription(offer);

            // 5. Gửi Offer qua SignalR
            console.log('Sending call offer...');
            window.chatHub.server.sendCallOffer(partnerUsername, JSON.stringify(offer), callType);

        } catch (e) {
            console.error('Failed to start call', e);
            hangUp(); // Dọn dẹp nếu có lỗi
        }
    }

    // Gắn sự kiện vào nút bấm
    $('body').on('click', '#start-voice-call-btn', function () {
        startCall('voice');
    });
    $('body').on('click', '#start-video-call-btn', function () {
        startCall('video');
    });


    // ========================================================
    // LOGIC: NHẬN CUỘC GỌI (CALLEE) - TỪ SIGNALR
    // ========================================================
    $(function () {
        // ... (code chatHub.client của bạn) ...
        // Thêm các client receivers NÀY vào

        let pendingOffer = null;
        let pendingCaller = null;

        // 1. Nhận được Offer
        window.chatHub.client.receiveCallOffer = function (callerUsername, offerSdp, callType) {
            if (isCallInProgress) {
                // Đang bận, tự động từ chối (hoặc báo bận)
                // Tạm thời bỏ qua
                console.log('Busy, ignoring incoming call from ' + callerUsername);
                return;
            }

            console.log(`Incoming ${callType} call from ${callerUsername}`);
            pendingOffer = JSON.parse(offerSdp);
            pendingCaller = callerUsername;

            // Hiển thị modal
            const avatar = $(`.friend-item[data-username="${callerUsername}"]`).data('avatar-url') || '/Content/default-avatar.png';
            const displayName = $(`.friend-item[data-username="${callerUsername}"]`).find('strong').text().trim();

            $('#incoming-call-avatar').attr('src', avatar);
            $('#incoming-call-name').text(displayName);
            $('#incoming-call-type').text(callType === 'video' ? 'Đang gọi video...' : 'Đang gọi thoại...');

            $('#incomingCallModal').modal('show');
        };

        // 2. Nhận được Answer
        window.chatHub.client.receiveCallAnswer = async function (calleeUsername, answerSdp) {
            if (!peerConnection || !isCallInProgress) return;

            console.log('Received call answer from ' + calleeUsername);
            try {
                const answer = JSON.parse(answerSdp);
                await peerConnection.setRemoteDescription(new RTCSessionDescription(answer));

                callStartTime = new Date(); // <-- THÊM DÒNG NÀY
                $('#call-view-status').text('Đã kết nối');

            } catch (e) {
                console.error('Error setting remote description', e);
            }
        };

        // 3. Nhận được ICE Candidate
        window.chatHub.client.receiveIceCandidate = async function (senderUsername, candidate) {
            if (!peerConnection) return;

            try {
                const iceCandidate = JSON.parse(candidate);
                await peerConnection.addIceCandidate(new RTCIceCandidate(iceCandidate));
            } catch (e) {
                console.error('Error adding received ICE candidate', e);
            }
        };

        window.chatHub.client.callEnded = function (senderUsername) {
            console.log('Call ended by ' + senderUsername);
            hangUp(false);
        };
        // ========================================================
        // HÀM RENDER TIN NHẮN CALL LOG
        // ========================================================
        function createCallLogHtml(contentObj) {
            let icon = '';
            let text = '';

            // Icon và chữ dựa trên trạng thái
            if (contentObj.status === 'completed') {
                icon = contentObj.callType === 'video' ? 'fa-video' : 'fa-phone-alt';
                let durationText = '0 giây';
                if (contentObj.duration > 0) {
                    const minutes = Math.floor(contentObj.duration / 60);
                    const seconds = contentObj.duration % 60;
                    if (minutes > 0) {
                        durationText = `${minutes} phút ${seconds} giây`;
                    } else {
                        durationText = `${seconds} giây`;
                    }
                }
                text = `Cuộc gọi ${contentObj.callType === 'video' ? 'video' : 'thoại'} <br> <span style="font-size: 0.85rem; color: #6c757d;">${durationText}</span>`;

            } else { // missed, declined, etc.
                icon = contentObj.callType === 'video' ? 'fa-video-slash' : 'fa-phone-slash';
                text = `Đã bỏ lỡ cuộc gọi ${contentObj.callType === 'video' ? 'video' : 'thoại'}`;
            }

            // Nút gọi lại
            let callBackButton = `
                <button class="btn btn-light btn-sm mt-2 w-100 call-back-btn"
                        data-call-type="${contentObj.callType}">
                    Gọi lại
                </button>`;

            // Kết hợp HTML
            return `
                <div style="display: flex; align-items: center; gap: 10px;">
                    <i class="fas ${icon}" style="font-size: 1.2rem; color: #555;"></i>
                    <div style="font-weight: 500;">
                        ${text}
                    </div>
                </div>
                ${callBackButton}
            `;
        }
        // ========================================================
        // LOGIC: CÁC NÚT BẤM TRONG MODAL
        // ========================================================

        // A. Chấp nhận cuộc gọi
        $('#accept-call-btn').on('click', async function () {
            if (!pendingOffer || !pendingCaller) return;

            console.log('Accepting call from ' + pendingCaller);
            $('#incomingCallModal').modal('hide');
            currentCallPartner = pendingCaller;

            try {
                // 1. Lấy media
                const callType = $('#incoming-call-type').text().includes('video') ? 'video' : 'voice';
                await startMedia(callType);

                // 2. Hiển thị UI
                showCallView(pendingCaller, callType);
                $('#call-view-status').text('Đang kết nối...');

                callStartTime = new Date();

                // 3. Tạo Peer Connection
                createPeerConnection();

                // 4. Set Remote Offer (từ người gọi)
                await peerConnection.setRemoteDescription(new RTCSessionDescription(pendingOffer));

                // 5. Tạo Answer
                const answer = await peerConnection.createAnswer();
                await peerConnection.setLocalDescription(answer);

                // 6. Gửi Answer qua SignalR
                console.log('Sending call answer to ' + pendingCaller);
                window.chatHub.server.sendCallAnswer(pendingCaller, JSON.stringify(answer));

                pendingOffer = null;
                pendingCaller = null;

            } catch (e) {
                console.error('Failed to accept call', e);
                hangUp();
            }
        });

        // B. Từ chối cuộc gọi
        $('#decline-call-btn').on('click', function () {
            console.log('Declining call from ' + pendingCaller);
            if (pendingCaller) {
                currentCallPartner = pendingCaller;
                currentCallType = $('#incoming-call-type').text().includes('video') ? 'video' : 'voice';
                hangUp(true);
            }
            $('#incomingCallModal').modal('hide');
            pendingOffer = null;
            pendingCaller = null;
        });

        // C. Dập máy
        $('#hang-up-btn').on('click', function () {
            hangUp(true);
        });

        // D. Tắt/Mở Mic
        $('#toggle-mic-btn').on('click', function () {
            if (!localStream) return;
            isMicOn = !isMicOn;
            localStream.getAudioTracks().forEach(track => {
                track.enabled = isMicOn;
            });
            $(this).html(isMicOn ? '<i class="fas fa-microphone"></i>' : '<i class="fas fa-microphone-slash text-danger"></i>');
        });

        $('#toggle-video-btn').on('click', function () {
            if (!localStream || currentCallType !== 'video') return;
            isVideoOn = !isVideoOn;
            localStream.getVideoTracks().forEach(track => {
                track.enabled = isVideoOn;
            });
            $(this).html(isVideoOn ? '<i class="fas fa-video"></i>' : '<i class="fas fa-video-slash text-danger"></i>');
        });

    });
    // ========================================================
    // CHỨC NĂNG UPLOAD ẢNH, VIDEO, FILE
    // ========================================================

    // ✅ 1. Xử lý khi chọn ảnh
    $('#imageUploadInput').off('change').on('change', function () {
        const files = this.files;
        if (files.length === 0) return;

        const container = $('#imagePreviewContainer');
        container.empty();

        Array.from(files).forEach(file => {
            const reader = new FileReader();
            reader.onload = function (e) {
                const preview = `
                        <div style="position: relative; display: inline-block;">
                            <img src="${e.target.result}" class="img-fluid rounded" style="width:120px; height:120px; object-fit:cover;" />
                            <div style="position: absolute; bottom: 5px; right: 5px; background: rgba(0,0,0,0.6); color: white; padding: 2px 6px; border-radius: 4px; font-size: 0.75rem;">
                                ${(file.size / (1024 * 1024)).toFixed(1)} MB
                            </div>
                        </div>`;
                container.append(preview);
            };
            reader.readAsDataURL(file);
        });

        $('#imagePreviewModal').modal('show');
    });

    // ✅ 2. Xử lý khi chọn file
    $('#fileUploadInput').off('change').on('change', function () {
        const files = this.files;
        if (files.length === 0) return;

        const container = $('#imagePreviewContainer');
        container.empty();

        Array.from(files).forEach(file => {
            const ext = file.name.split('.').pop().toLowerCase();
            let icon = '📄';
            if (['pdf'].includes(ext)) icon = '📕';
            else if (['doc', 'docx'].includes(ext)) icon = '📘';
            else if (['xls', 'xlsx'].includes(ext)) icon = '📗';
            else if (['zip', 'rar', '7z'].includes(ext)) icon = '📦';

            const preview = `
                    <div style="width:120px; height:120px; border:2px dashed #ccc; display:flex; flex-direction:column; align-items:center; justify-content:center; border-radius:8px; padding:10px; text-align:center;">
                        <div style="font-size: 2rem; margin-bottom: 8px;">${icon}</div>
                        <div style="font-size: 0.75rem; font-weight: 600; overflow: hidden; text-overflow: ellipsis; width: 100%;">${file.name}</div>
                        <div style="font-size: 0.7rem; color: #999; margin-top: 4px;">${(file.size / (1024 * 1024)).toFixed(1)} MB</div>
                    </div>`;
            container.append(preview);
        });

        $('#imagePreviewModal').modal('show');
    });

    $('#sendImageButton').off('click').on('click', function () {
        const imageFiles = $('#imageUploadInput')[0].files;
        const otherFiles = $('#fileUploadInput')[0].files;

        let filesToUpload = imageFiles.length > 0 ? imageFiles : otherFiles;
        if (filesToUpload.length === 0) {
            alert('Vui lòng chọn file để gửi.');
            return;
        }

        const formData = new FormData();
        for (let i = 0; i < filesToUpload.length; i++) {
            formData.append('file' + i, filesToUpload[i]);
        }

        $('#sendImageButton').prop('disabled', true).text('Đang gửi...');

        $.ajax({
            url: urls.uploadFiles,
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {
                if (response.success && response.files) {
                    response.files.forEach(fileData => {
                        const now = new Date();
                        const vietTime = now.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });

                        let bubbleContentHtml = "";

                        // Tạo HTML preview dựa vào type
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

                        // Hiển thị tin nhắn của mình ngay lập tức
                        const selfMessageHtml = `
                            <div class="chat-message self" data-timestamp="${now.toISOString()}">
                                <div class="chat-bubble">
                                    ${bubbleContentHtml}
                                    <span class="bubble-time">${vietTime}</span>
                                </div>
                            </div>`;

                        const messagesList = $('#messagesList');
                        messagesList.append(selfMessageHtml);
                        messagesList.scrollTop(messagesList[0].scrollHeight);

                        // Tạo tin nhắn JSON để gửi qua SignalR
                        const msgJson = JSON.stringify({
                            type: fileData.type,
                            content: fileData.filePath,
                            fileName: fileData.fileName || '',
                            fileSize: fileData.fileSize || ''
                        });

                        // Gửi tin nhắn qua SignalR
                        if (window.currentChat.mode === 'private') {
                            window.chatHub.server.sendPrivateMessage(window.currentChat.partnerUsername, msgJson);
                        } else if (window.currentChat.mode === 'ai') {
                            window.chatHub.server.sendMessageToAI(`[${fileData.type}] ${fileData.filePath}`);
                        }
                    });

                    // Đóng modal
                    $('#imagePreviewModal').modal('hide');
                    $('#imageUploadInput').val('');
                    $('#fileUploadInput').val('');

                    // ✅ Ẩn AI welcome nếu đang chat AI
                    if (window.currentChat.mode === 'ai') {
                        $('#ai-welcome-screen').hide();
                        $('.message-area').show();
                    }
                } else {
                    alert('Lỗi upload: ' + (response.message || 'Không rõ nguyên nhân'));
                }
            },
            error: function (xhr, status, error) {
                alert('Lỗi kết nối: ' + error);
            },
            complete: function () {
                $('#sendImageButton').prop('disabled', false).text('Gửi');
            }
        });
    });

    // ✅ 4. Nút gửi ảnh nhanh
    $('#quick-image-btn').off('click').on('click', function () {
        $('#imageUploadInput').click();
    });

    // ✅ 5. Menu đính kèm
    $('#toggle-attach-menu').off('click').on('click', function (e) {
        e.stopPropagation();

        // Tạo menu nếu chưa có
        if ($('#attachment-menu').length === 0) {
            const menuHtml = `
                    <div id="attachment-menu" style="display:none; position:fixed; background:white; border:1px solid #ddd; border-radius:8px; box-shadow:0 4px 12px rgba(0,0,0,0.15); padding:8px 0; z-index:1000; min-width:180px;">
                        <a href="#" id="send-image-btn" style="display:block; padding:10px 16px; color:#333; text-decoration:none; transition:background 0.2s;">
                            <i class="fas fa-image" style="width:20px; margin-right:8px; color:#007bff;"></i> Gửi ảnh
                        </a>
                        <a href="#" id="send-video-btn" style="display:block; padding:10px 16px; color:#333; text-decoration:none; transition:background 0.2s;">
                            <i class="fas fa-video" style="width:20px; margin-right:8px; color:#dc3545;"></i> Gửi video
                        </a>
                        <a href="#" id="send-file-btn" style="display:block; padding:10px 16px; color:#333; text-decoration:none; transition:background 0.2s;">
                            <i class="fas fa-file-alt" style="width:20px; margin-right:8px; color:#28a745;"></i> Gửi file
                        </a>
                    </div>`;
            $('body').append(menuHtml);

            // Style hover
            $('#attachment-menu a').hover(
                function () { $(this).css('background', '#f8f9fa'); },
                function () { $(this).css('background', 'transparent'); }
            );
        }

        // Hiển thị menu
        const btnRect = $(this)[0].getBoundingClientRect();
        $('#attachment-menu').css({
            display: 'block',
            bottom: (window.innerHeight - btnRect.top) + 'px',
            left: (btnRect.left) + 'px'
        });
    });

    // Đóng menu khi click ngoài
    $(document).off('click.attachMenu').on('click.attachMenu', function (e) {
        if (!$(e.target).closest('#toggle-attach-menu, #attachment-menu').length) {
            $('#attachment-menu').hide();
        }
    });

    // ✅ 6. Click vào các mục trong menu
    $('body').off('click', '#send-image-btn').on('click', '#send-image-btn', function (e) {
        e.preventDefault();
        $('#attachment-menu').hide();
        $('#imageUploadInput').attr('accept', 'image/*').click();
    });

    $('body').off('click', '#send-video-btn').on('click', '#send-video-btn', function (e) {
        e.preventDefault();
        $('#attachment-menu').hide();
        $('#imageUploadInput').attr('accept', 'video/*').click();
    });

    $('body').off('click', '#send-file-btn').on('click', '#send-file-btn', function (e) {
        e.preventDefault();
        $('#attachment-menu').hide();
        $('#fileUploadInput').attr('accept', '.pdf,.doc,.docx,.xls,.xlsx,.zip,.rar,.txt').click();
    });

    // ========================================================
    // EMOJI PICKER
    // ========================================================
    (function () {
        const $emojiBtn = $('#emoji-button');
        const $messageInput = $('#messageInput');

        if ($emojiBtn.length && $messageInput.length) {
            // Kiểm tra xem đã có emoji picker chưa
            if (typeof EmojiButton !== 'undefined') {
                try {
                    const picker = new EmojiButton({
                        position: 'top-start',
                        autoHide: true,
                        theme: 'auto'
                    });

                    picker.on('emoji', selection => {
                        $messageInput.val($messageInput.val() + selection.emoji);
                        $messageInput.focus();
                    });

                    $emojiBtn.off('click.emoji').on('click.emoji', function (e) {
                        e.stopPropagation();
                        picker.togglePicker($emojiBtn[0]);
                    });
                } catch (err) {
                    console.warn('Emoji Button library error:', err);
                    initSimpleEmojiPicker();
                }
            } else {
                // Fallback: Emoji picker đơn giản
                initSimpleEmojiPicker();
            }
        }

        function initSimpleEmojiPicker() {
            $emojiBtn.off('click.emoji').on('click.emoji', function (e) {
                e.stopPropagation();

                if ($('#simple-emoji-picker').length === 0) {
                    const emojis = ['😀', '😃', '😄', '😁', '😆', '😅', '🤣', '😂', '😊', '😇', '🙂', '🙃', '😉', '😌', '😍', '🥰', '😘', '😗', '😙', '😚', '❤️', '💕', '💖', '💗', '👍', '👎', '👏', '🙏', '💪', '🎉', '🎊', '🎁', '🔥', '⭐', '✨', '💯', '✅', '❌'];

                    const pickerHtml = `
                            <div id="simple-emoji-picker" style="display:none; position:fixed; background:white; border:1px solid #ddd; border-radius:12px; box-shadow:0 4px 16px rgba(0,0,0,0.2); padding:12px; z-index:1000; max-width:300px;">
                                <div style="display:grid; grid-template-columns:repeat(8, 1fr); gap:8px;">
                                    ${emojis.map(emoji => `<button class="emoji-btn" style="border:none; background:transparent; font-size:1.5rem; cursor:pointer; padding:4px; border-radius:4px; transition:background 0.2s;">${emoji}</button>`).join('')}
                                </div>
                            </div>`;
                    $('body').append(pickerHtml);

                    // Hover effect
                    $(document).on('mouseenter', '.emoji-btn', function () {
                        $(this).css('background', '#f0f0f0');
                    }).on('mouseleave', '.emoji-btn', function () {
                        $(this).css('background', 'transparent');
                    });

                    // Click emoji
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

            // Đóng picker khi click ngoài
            $(document).off('click.emojiPicker').on('click.emojiPicker', function (e) {
                if (!$(e.target).closest('#emoji-button, #simple-emoji-picker').length) {
                    $('#simple-emoji-picker').hide();
                }
            });
        }
    })();

    $.connection.hub.url = window.location.origin + "/signalr";
    $.connection.hub.qs = { "noCache": new Date().getTime() };
    $.connection.hub.transportConnectTimeout = 10000;
    $.connection.hub.keepAlive = 15000;
    $.connection.hub.reconnectDelay = 5000;
    $.connection.hub.start({ transport: ['webSockets', 'serverSentEvents', 'longPolling'] })
        .done(function () {
            console.log("✅ SignalR connected successfully");
            console.log("Config loaded:", { username: currentUsername, urls: Object.keys(urls) });  
            loadFriendsList();  
        })
        .fail(function (err) {
            console.error("❌ SignalR connection failed:", err);
        });
});