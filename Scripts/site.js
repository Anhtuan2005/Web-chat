$(document).ready(function () {
    $(document).on('click', '.user-avatar-trigger', function () {
        const userId = $(this).data('userid');
        if (!userId) return;

        const $modal = $('#userProfileModal');
        $modal.data('current-user-id', userId); // Store the ID

        // Reset modal content to loading state
        $modal.find('#userProfileModalLabel').text('Đang tải...');
        $modal.find('#userProfileModalUsername').text('');
        $modal.find('#userProfileModalBio').text('Đang tải tiểu sử...');
        $modal.find('#userProfileModalGender').text('Đang tải...');
        $modal.find('#userProfileModalDob').text('Đang tải...');
        $modal.find('#userProfileModalEmail').text('**********');
        $modal.find('#userProfileModalPhone').text('**********');
        $modal.find('#userProfileModalAvatar').attr('src', '/Content/default-avatar.png');
        $modal.find('#userProfileModalCover').css('background-image', 'url(/Content/default-cover.jpg)');

        var myModal = new bootstrap.Modal($modal[0]);
        myModal.show();

        // Fetch user details via AJAX
        $.ajax({
            url: '/Friend/GetUserDetails',
            type: 'GET',
            data: { userId: userId },
            success: function (response) {
                if (response.success) {
                    const user = response.user;
                    $modal.find('#userProfileModalLabel').text(user.DisplayName);
                    $modal.find('#userProfileModalUsername').text('@' + user.Username);
                    $modal.find('#userProfileModalBio').text(user.Bio || 'Chưa có tiểu sử.');
                    $modal.find('#userProfileModalGender').text(user.Gender);
                    $modal.find('#userProfileModalDob').text(user.DateOfBirth);
                    $modal.find('#userProfileModalEmail').text(user.Email);
                    $modal.find('#userProfileModalPhone').text(user.PhoneNumber);
                    $modal.find('#userProfileModalAvatar').attr('src', user.AvatarUrl || '/Content/default-avatar.png');
                    $modal.find('#userProfileModalCover').css('background-image', `url(${user.CoverPhotoUrl})`);
                } else {
                    $modal.find('#userProfileModalLabel').text('Không thể tải thông tin.');
                }
            },
            error: function () {
                $modal.find('#userProfileModalLabel').text('Lỗi khi tải thông tin.');
            }
        });
    });

    // Handler for the block user button
    $(document).on('click', '#block-user-btn', function() {
        const $modal = $('#userProfileModal');
        const blockedUserId = $modal.data('current-user-id');

        if (!blockedUserId) {
            alert('Lỗi: Không tìm thấy ID người dùng để chặn.');
            return;
        }

        if (!confirm('Bạn có chắc chắn muốn chặn người dùng này không? Hành động này sẽ xóa mọi mối quan hệ bạn bè và không thể hoàn tác.')) {
            return;
        }

        $.ajax({
            url: '/Friend/BlockUser',
            type: 'POST',
            data: {
                __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val(),
                blockedUserId: blockedUserId
            },
            success: function(response) {
                if (response.success) {
                    alert(response.message || 'Đã chặn người dùng thành công.');
                    $modal.modal('hide');
                    location.reload(); // Refresh to reflect the change everywhere
                } else {
                    alert('Lỗi: ' + (response.message || 'Không thể chặn người dùng.'));
                }
            },
            error: function() {
                alert('Đã xảy ra lỗi kết nối. Vui lòng thử lại.');
            }
        });
    });
});
