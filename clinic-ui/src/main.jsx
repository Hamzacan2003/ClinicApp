import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import Swal from 'sweetalert2'
import './index.css'
import App from './App.jsx'

// Tarayıcının çirkin alert pencerelerini yakalayıp şık popup'a çevirir
window.alert = (message) => {
  const text = String(message || '');
  const isError = text.toLowerCase().includes('hata') || 
                  text.toLowerCase().includes('uyuşmuyor') || 
                  text.toLowerCase().includes('geçersiz') || 
                  text.toLowerCase().includes('bulunamadı') ||
                  text.toLowerCase().includes('başarısız') ||
                  text.toLowerCase().includes('reddedildi');

  Swal.fire({
    title: isError ? 'İşlem Başarısız' : 'Bilgilendirme',
    text: text,
    icon: isError ? 'error' : 'success',
    confirmButtonText: 'Tamam',
    confirmButtonColor: '#2563eb',
    customClass: {
      popup: 'rounded-2xl shadow-2xl p-6 font-sans',
      confirmButton: 'px-5 py-2.5 rounded-lg text-white font-medium text-sm'
    }
  });
};

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>,
)