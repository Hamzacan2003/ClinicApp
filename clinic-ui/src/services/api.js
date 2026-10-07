import axios from 'axios';

// Vercel Environment Variable üzerinden veya canlı Render API adresi
const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://clinic-api-bs2z.onrender.com/api';

const api = axios.create({
  baseURL: API_BASE_URL,
});

// 401 Hatasını Çözen Interceptor: Kayıtlı JWT token'ı isteklere otomatik ekler
api.interceptors.request.use(
  (config) => {
    const userStr = localStorage.getItem('clinic_user');
    if (userStr) {
      try {
        const user = JSON.parse(userStr);
        const token = user?.token || user?.accessToken || user?.data?.token;
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }
      } catch (err) {
        console.error("Token okuma hatası:", err);
      }
    }
    return config;
  },
  (error) => Promise.reject(error)
);

export const getDoctors = () => api.get('/Doctors');
export const staffLogin = (data) => api.post('/Doctors/login', data);
export const getDoctorSchedule = (doctorId) => api.get(`/Doctors/${doctorId}/schedule`);
export const updateDoctorSchedule = (doctorId, data) => api.post(`/Doctors/${doctorId}/schedule/update`, data);
export const addDoctorTimeOff = (doctorId, data) => api.post(`/Doctors/${doctorId}/time-off`, data);
export const getDoctorTimeOffs = (doctorId) => api.get(`/Doctors/${doctorId}/time-offs`);
export const deleteDoctorTimeOff = (id) => api.delete(`/Doctors/time-off/${id}`);
export const createStaff = (data) => api.post('/StaffManagement/create-staff', data);
export const getAvailableSlots = (doctorId, date) => 
  api.get(`/Appointments/slots?doctorId=${doctorId}&date=${date}`);
export const bookAppointment = (data) => api.post('/Appointments/book', data);
export const searchPatientByTc = (tc) => api.get(`/Appointments/search-tc/${tc}`);

export const getDashboardStats = () => api.get('/Dashboard/stats');
export const getDailyAppointments = (doctorId, date) => 
  api.get(`/DoctorPanel/daily-appointments?doctorId=${doctorId}&date=${date}`);
export const addMedicalRecord = (formData) => 
  api.post('/DoctorPanel/add-medical-record', formData, {
    headers: { 'Content-Type': 'multipart/form-data' }
  });
export const getPatientHistory = (nationalId) => 
  api.get(`/DoctorPanel/patient-history/${nationalId}`);

// Ek Gelişmiş Servisler
export const forgotPassword = (email) => api.post('/StaffManagement/forgot-password', { email });
export const resetPassword = (data) => api.post('/StaffManagement/reset-password', data);
export const changePassword = (data) => api.post('/StaffManagement/change-password', data);

export const getStaffList = () => api.get('/StaffManagement/staff-list');
export const updateDoctorFee = (doctorId, fee) => api.post('/StaffManagement/update-doctor-fee', { doctorId, fee });
export const deleteDoctor = (id) => api.delete(`/StaffManagement/delete-doctor/${id}`);

export const chargeAppointment = (data) => api.post('/StaffManagement/charge-appointment', data);
export const getDailyRevenue = (date) => api.get(`/StaffManagement/daily-revenue?date=${date}`);

export const getBanners = () => api.get('/StaffManagement/banners');
export const uploadBanner = (formData) => api.post('/StaffManagement/banners/upload', formData, {
  headers: { 'Content-Type': 'multipart/form-data' }
});
export const deleteBanner = (id) => api.delete(`/StaffManagement/banners/${id}`);

export default api;