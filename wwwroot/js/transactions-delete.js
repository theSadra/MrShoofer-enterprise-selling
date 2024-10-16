'use strict';

document.addEventListener('DOMContentLoaded', function (e) {
  (function () {
    const deleteButtons = document.querySelectorAll('.delete-transaction');
    deleteButtons.forEach(deleteButton => {
      deleteButton.addEventListener('click', function (e) {
        e.preventDefault();
        const userName = this.getAttribute('data-transaction-username');
        Swal.fire({
          title: 'پیام تایید',
          html: `<p class="text-danger">آیا می خواهید تراکنش را حذف کنید ؟<br> <span class="fw-medium text-body">${userName}</span></p>`,
          icon: 'warning',
          showCancelButton: true,
          confirmButtonText: 'حذف کن',
          cancelButtonText: 'بازگشت',
          customClass: {
            confirmButton: 'btn btn-primary waves-effect waves-light',
            cancelButton: 'btn btn-secondary waves-effect waves-light'
          }
        }).then(result => {
          if (result.isConfirmed) {
            window.location.href = this.getAttribute('href'); //redirect to href
          } else {
            Swal.fire({
              title: 'لغو شد',
              html: `<p>تراکنش <span class="fw-medium text-primary">${userName}</span> حذف نشد!</p>`,
              icon: 'error',
              confirmButtonText: 'باشه',
              customClass: {
                confirmButton: 'btn btn-success waves-effect waves-light'
              }
            });
          }
        });
      });
    });
  })();
});
