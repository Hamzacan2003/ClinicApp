import Swal from 'sweetalert2';

// 1. Ekranın sağ üstünde kaybolan zarif Toast bildirimleri
const Toast = Swal.mixin({
  toast: true,
  position: 'top-end',
  showConfirmButton: false,
  timer: 3000,
  timerProgressBar: true,
  didOpen: (toast) => {
    toast.onmouseenter = Swal.stopTimer;
    toast.onmouseleave = Swal.resumeTimer;
  }
});

export const notify = {
  success: (message) => {
    Toast.fire({
      icon: 'success',
      title: message
    });
  },
  error: (message) => {
    Toast.fire({
      icon: 'error',
      title: message
    });
  },
  warning: (message) => {
    Toast.fire({
      icon: 'warning',
      title: message
    });
  },
  info: (message) => {
    Toast.fire({
      icon: 'info',
      title: message
    });
  },

  // 2. Onay pencereleri için şık Modal
  confirm: async (title, text, confirmButtonText = 'Evet, Onayla') => {
    const result = await Swal.fire({
      title,
      text,
      icon: 'question',
      showCancelButton: true,
      confirmButtonColor: '#2563eb',
      cancelButtonColor: '#dc2626',
      confirmButtonText,
      cancelButtonText: 'Vazgeç',
      customClass: {
        popup: 'rounded-2xl shadow-2xl p-6 font-sans',
        confirmButton: 'px-5 py-2.5 rounded-lg text-white font-medium text-sm',
        cancelButton: 'px-5 py-2.5 rounded-lg text-white font-medium text-sm'
      }
    });
    return result.isConfirmed;
  }
};