document.addEventListener('DOMContentLoaded', () => {
    initSearch();
    initDeleteDialog();
});

// Auto-submit the search form shortly after the user stops typing.
function initSearch() {
    const form = document.getElementById('searchForm');
    const input = document.getElementById('Search');
    if (!form || !input) return;

    let timer;
    input.addEventListener('input', () => {
        clearTimeout(timer);
        timer = setTimeout(() => form.submit(), 400);
    });

    // After a search reload, restore focus with the caret at the end.
    if (input.value) {
        input.focus();
        input.setSelectionRange(input.value.length, input.value.length);
    }
}

function initDeleteDialog() {
    const dialog = document.getElementById('deleteDialog');
    const form = document.getElementById('deleteForm');
    if (!dialog || !form) return;

    document.querySelectorAll('[data-delete-id]').forEach(link => {
        link.addEventListener('click', e => {
            e.preventDefault();
            document.getElementById('deleteId').value = link.dataset.deleteId;
            document.getElementById('deleteName').textContent = link.dataset.deleteName;
            dialog.showModal();
        });
    });

    document.querySelectorAll('[data-close-dialog]').forEach(button => {
        button.addEventListener('click', () => dialog.close());
    });

    // Clicking the backdrop closes the dialog.
    dialog.addEventListener('click', e => {
        if (e.target === dialog) dialog.close();
    });

    // Prevent double submit.
    form.addEventListener('submit', () => {
        form.querySelector('[type="submit"]').disabled = true;
    });
}