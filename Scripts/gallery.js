
let currentGalleryImages = [];
let currentGalleryIndex = 0;

function openGallery(index, images) {
    currentGalleryImages = images;
    currentGalleryIndex = index;
    const galleryModal = document.getElementById('galleryModal');
    if (galleryModal) {
        galleryModal.style.display = "flex";
        showGalleryImage();
        document.addEventListener('keydown', handleKeyboardNav);
    }
}

function closeGallery() {
    const galleryModal = document.getElementById('galleryModal');
    if (galleryModal) {
        galleryModal.style.display = "none";
        document.removeEventListener('keydown', handleKeyboardNav);
    }
}

function changeImage(n) {
    currentGalleryIndex += n;
    if (currentGalleryIndex >= currentGalleryImages.length) {
        currentGalleryIndex = 0;
    }
    if (currentGalleryIndex < 0) {
        currentGalleryIndex = currentGalleryImages.length - 1;
    }
    showGalleryImage();
}

function showGalleryImage() {
    const img = document.getElementById("galleryImage");
    const caption = document.getElementById("galleryCaption");
    if (img && caption) {
        img.style.opacity = 0.5;
        // Check if the image source is valid
        if (currentGalleryImages && currentGalleryImages[currentGalleryIndex]) {
            img.src = currentGalleryImages[currentGalleryIndex];
            caption.innerHTML = (currentGalleryIndex + 1) + " / " + currentGalleryImages.length;
        }
        setTimeout(() => { img.style.opacity = 1; }, 100);
    }
}

function handleKeyboardNav(e) {
    if (e.key === "ArrowLeft") changeImage(-1);
    if (e.key === "ArrowRight") changeImage(1);
    if (e.key === "Escape") closeGallery();
}

// It's better to add this event listener when the DOM is ready.
document.addEventListener('DOMContentLoaded', (event) => {
    const galleryModal = document.getElementById('galleryModal');
    if (galleryModal) {
        galleryModal.addEventListener('click', function (e) {
            if (e.target === this) closeGallery();
        });
    }
});
