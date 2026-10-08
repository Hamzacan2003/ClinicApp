import React, { useState, useEffect } from 'react';
import * as signalR from '@microsoft/signalr';
import { 
  Calendar, Clock, User, Phone, Mail, ShieldCheck, 
  Activity, Award, CheckCircle, AlertCircle, FileText, 
  Upload, Search, DollarSign, LogIn, ChevronRight, Stethoscope,
  Bell, Eye, LogOut, LayoutDashboard, CalendarDays, Users, FolderOpen,
  Settings, Lock, Save, Trash2, PlusCircle, Ban, CreditCard,
  Layers, KeyRound, MapPin, UserPlus, History
} from 'lucide-react';
import { 
  getDoctors, getAvailableSlots, bookAppointment, 
  getDailyAppointments, addMedicalRecord, getPatientHistory, 
  getDashboardStats, staffLogin, 
  getDoctorSchedule, updateDoctorSchedule,
  addDoctorTimeOff, getDoctorTimeOffs, deleteDoctorTimeOff,
  forgotPassword, resetPassword, changePassword,
  getStaffList, updateDoctorFee, deleteDoctor,
  chargeAppointment, getDailyRevenue,
  getBanners, uploadBanner, deleteBanner, createStaff
} from './services/api';

const DAYS_TR = [
  { id: 0, name: 'Pazar' },
  { id: 1, name: 'Pazartesi' },
  { id: 2, name: 'Salı' },
  { id: 3, name: 'Çarşamba' },
  { id: 4, name: 'Perşembe' },
  { id: 5, name: 'Cuma' },
  { id: 6, name: 'Cumartesi' }
];

export default function App() {
  // F5 KORUMASI: Başlangıçta localStorage'dan kullanıcıyı al
  const savedUser = JSON.parse(localStorage.getItem('clinic_user') || 'null');
  const [currentView, setCurrentView] = useState(savedUser ? 'panel' : 'public'); 
  const [panelTab, setPanelTab] = useState('appointments'); 

  // Personel Giriş ve Kilitlenme
  const [showLoginModal, setShowLoginModal] = useState(false);
  const [loginForm, setLoginForm] = useState({ email: '', password: '' });
  const [loginError, setLoginError] = useState('');
  const [loggedUser, setLoggedUser] = useState(savedUser);

  // Şifremi Unuttum
  const [showForgotModal, setShowForgotModal] = useState(false);
  const [forgotStep, setForgotStep] = useState(1);
  const [forgotEmail, setForgotEmail] = useState('');
  const [resetCode, setResetCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [newPasswordConfirm, setNewPasswordConfirm] = useState('');
  const [forgotMsg, setForgotMsg] = useState('');

  // Profil Şifre Değiştirme
  const [profilePass, setProfilePass] = useState({ oldPass: '', newPass: '', confirm: '' });

  // Yeni Personel Ekleme Modalı
  const [showAddStaffModal, setShowAddStaffModal] = useState(false);
  const [newStaffForm, setNewStaffForm] = useState({
    firstName: '',
    lastName: '',
    email: '',
    password: '',
    role: 'Doktor',
    title: 'Op. Dr.',
    specialty: 'Göz Hastalıkları Uzmanı',
    consultationFee: 1500,
    biography: ''
  });

  // Hasta Eski Geçmişini İnceleme Modalı
  const [activeHistoryModalPatient, setActiveHistoryModalPatient] = useState(null);
  const [patientHistoryList, setPatientHistoryList] = useState([]);

  // Klinik Vitrin Banner'ları (Slider)
  const defaultBanners = [
    {
      imageUrl: "https://images.unsplash.com/photo-1629909613654-28e377c37b09?auto=format&fit=crop&w=1200&q=80",
      title: "Gelişmiş Göz Dibi Fotoğraflama ve Lazer",
      subtitle: "Çankaya / Ankara adresinde haftanın 6 günü hizmetinizdeyiz."
    }
  ];
  const [banners, setBanners] = useState(defaultBanners);
  const [currentBannerIdx, setCurrentBannerIdx] = useState(0);

  // Randevu Sihirbazı State'leri
  const [doctors, setDoctors] = useState([]);
  const [selectedDoctor, setSelectedDoctor] = useState(null);
  const [selectedDate, setSelectedDate] = useState(new Date().toLocaleDateString('en-CA'));
  const [slots, setSlots] = useState([]);
  const [selectedSlot, setSelectedSlot] = useState(null);
  const [loadingSlots, setLoadingSlots] = useState(false);

  // NVI Randevu Formu
  const [formData, setFormData] = useState({
    nationalId: '',
    firstName: '',
    lastName: '',
    birthYear: '',
    email: '',
    phoneNumber: '',
    patientComplaint: ''
  });
  const [bookingStatus, setBookingStatus] = useState({ loading: false, success: null, message: '' });

  // Panel Verileri
  const [dailyAppointments, setDailyAppointments] = useState([]);
  const [stats, setStats] = useState(null);
  const [panelDate, setPanelDate] = useState(new Date().toLocaleDateString('en-CA'));
  const [staffList, setStaffList] = useState([]);

  // Muayene & Röntgen Modalı
  const [activeModalAppointment, setActiveModalAppointment] = useState(null);
  const [medicalForm, setMedicalForm] = useState({ diagnosis: '', clinicalNotes: '', prescription: '', followUpDate: '' });
  const [uploadedFiles, setUploadedFiles] = useState([]);

  // Çoklu İşlem Tahsilat Modalı
  const [chargingApp, setChargingApp] = useState(null);
  const [treatmentItems, setTreatmentItems] = useState([{ procedureName: 'Genel Muayene', price: 1500 }]);
  const [paymentMethod, setPaymentMethod] = useState('Nakit');

  // Günlük Ciro Raporu
  const [dailyRevenueData, setDailyRevenueData] = useState(null);

  // Doktor Haftalık Takvimi & Özel İzinler
  const [doctorScheduleList, setDoctorScheduleList] = useState([]);
  const [timeOffList, setTimeOffList] = useState([]);
  const [newTimeOff, setNewTimeOff] = useState({
    date: new Date().toLocaleDateString('en-CA'),
    isAllDay: true,
    startTime: '13:00',
    endTime: '15:00',
    reason: 'Ameliyat / Özel İzin'
  });

  // T.C. Arşiv
  const [searchTc, setSearchTc] = useState('');
  const [patientHistoryRecords, setPatientHistoryRecords] = useState([]);

  // SignalR Bildirim
  const [notification, setNotification] = useState(null);

  useEffect(() => {
    loadDoctors();
    loadBanners();
  }, []);

  const loadBanners = async () => {
    try {
      const res = await getBanners();
      if (res.data && res.data.length > 0) setBanners(res.data);
    } catch {}
  };

  useEffect(() => {
    if (banners.length <= 1) return;
    const timer = setInterval(() => {
      setCurrentBannerIdx((prev) => (prev + 1) % banners.length);
    }, 4500);
    return () => clearInterval(timer);
  }, [banners]);

  const loadDoctors = async () => {
    try {
      const res = await getDoctors();
      if (res.data && res.data.length > 0) {
        setDoctors(res.data);
        if (!selectedDoctor) setSelectedDoctor(res.data[0]);
      }
    } catch (err) {
      console.error(err);
    }
  };

  useEffect(() => {
    if (selectedDoctor && selectedDate) fetchSlots(selectedDoctor.id, selectedDate);
  }, [selectedDoctor, selectedDate]);

  const fetchSlots = async (docId, dateStr) => {
    setLoadingSlots(true);
    setSelectedSlot(null);
    try {
      const res = await getAvailableSlots(docId, dateStr);
      setSlots(res.data);
    } catch {
      setSlots([]);
    } finally {
      setLoadingSlots(false);
    }
  };

  // İstemci tarafı geçmiş saat kontrolü
  const isSlotInPast = (slotTimeStr) => {
    const todayStr = new Date().toLocaleDateString('en-CA'); 
    if (selectedDate !== todayStr) return false;

    const [slotH, slotM] = slotTimeStr.split(':').map(Number);
    const now = new Date();
    return (slotH < now.getHours()) || (slotH === now.getHours() && slotM <= now.getMinutes());
  };

  useEffect(() => {
    if (currentView === 'panel' && selectedDoctor) {
      loadPanelData();
      loadDoctorSchedule(selectedDoctor.id);
      loadDoctorTimeOffsData(selectedDoctor.id);
      loadStaff();
      loadRevenue(panelDate);
    }
  }, [currentView, selectedDoctor, panelDate]);

  const loadPanelData = async () => {
    try {
      const appRes = await getDailyAppointments(selectedDoctor.id, panelDate);
      setDailyAppointments(appRes.data);
      const statRes = await getDashboardStats();
      setStats(statRes.data);
    } catch (err) {
      console.error(err);
    }
  };

  const loadStaff = async () => {
    try {
      const res = await getStaffList();
      setStaffList(res.data);
    } catch {}
  };

  const loadRevenue = async (date) => {
    try {
      const res = await getDailyRevenue(date);
      setDailyRevenueData(res.data);
    } catch {}
  };

  const loadDoctorSchedule = async (docId) => {
    try {
      const res = await getDoctorSchedule(docId);
      if (res.data.length > 0) {
        setDoctorScheduleList(res.data);
      } else {
        const defaultList = DAYS_TR.map(d => ({
          dayOfWeek: d.id,
          startTime: '09:00',
          endTime: '17:00',
          slotDurationMinutes: 30,
          isDayOff: d.id === 0
        }));
        setDoctorScheduleList(defaultList);
      }
    } catch {}
  };

  const loadDoctorTimeOffsData = async (docId) => {
    try {
      const res = await getDoctorTimeOffs(docId);
      setTimeOffList(res.data);
    } catch {}
  };

  // SignalR Canlı Hub Bağlantısı
  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("https://clinic-api-bs2z.onrender.com/clinichub")
      .withAutomaticReconnect()
      .build();

    connection.start().then(() => {
      if (selectedDoctor) connection.invoke("JoinDoctorGroup", selectedDoctor.id);
    }).catch(console.error);

    connection.on("ReceiveNewAppointment", (data) => {
      setNotification(`Yeni Randevu: ${data.patientName} (${data.time})`);
      setTimeout(() => setNotification(null), 7000);
      if (currentView === 'panel') loadPanelData();
    });

    return () => {
      connection.stop();
    };
  }, [selectedDoctor, currentView]);

  // LOGIN & LOCALSTORAGE KAYDI (F5 KORUMASI)
  const handleLoginSubmit = async (e) => {
    e.preventDefault();
    setLoginError('');
    try {
      const res = await staffLogin(loginForm);
      setLoggedUser(res.data);
      localStorage.setItem('clinic_user', JSON.stringify(res.data));
      setShowLoginModal(false);
      setCurrentView('panel');
    } catch (err) {
      const data = err.response?.data;
      if (data?.isLocked) {
        setForgotEmail(loginForm.email);
        setShowLoginModal(false);
        setShowForgotModal(true);
        setForgotStep(1);
        setForgotMsg(data.message);
      } else {
        setLoginError(data?.message || 'E-posta veya şifre hatalı!');
      }
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('clinic_user');
    setLoggedUser(null);
    setCurrentView('public');
  };

  // ŞİFREMİ UNUTTUM
  const handleSendForgotCode = async (e) => {
    e.preventDefault();
    setForgotMsg('');
    try {
      await forgotPassword(forgotEmail);
      setForgotStep(2);
      setForgotMsg("6 haneli doğrulama kodu e-postanıza iletildi.");
    } catch (err) {
      setForgotMsg(err.response?.data?.message || "E-posta gönderilemedi.");
    }
  };

  const handleResetPasswordSubmit = async (e) => {
    e.preventDefault();
    if (newPassword !== newPasswordConfirm) return alert("Şifreler uyuşmuyor!");
    try {
      await resetPassword({ email: forgotEmail, code: resetCode, newPassword });
      alert("Şifreniz sıfırlandı, giriş yapabilirsiniz.");
      setShowForgotModal(false);
      setShowLoginModal(true);
    } catch (err) {
      alert(err.response?.data?.message || "Kod geçersiz.");
    }
  };

  // PROFİL ŞİFRE GÜNCELLEME
  const handleProfilePassChange = async (e) => {
    e.preventDefault();
    if (profilePass.newPass !== profilePass.confirm) return alert("Yeni şifreler uyuşmuyor!");
    try {
      await changePassword({
        email: loggedUser.email,
        oldPassword: profilePass.oldPass,
        newPassword: profilePass.newPass
      });
      alert("Şifreniz güncellendi!");
      setProfilePass({ oldPass: '', newPass: '', confirm: '' });
    } catch (err) {
      alert(err.response?.data?.message || "Eski şifre yanlış.");
    }
  };

  // YENİ PERSONEL EKLEME
  const handleCreateStaffSubmit = async (e) => {
    e.preventDefault();
    try {
      const res = await createStaff(newStaffForm);
      alert(res.data.message || "Personel başarıyla oluşturuldu!");
      setShowAddStaffModal(false);
      setNewStaffForm({
        firstName: '', lastName: '', email: '', password: '',
        role: 'Doktor', title: 'Op. Dr.', specialty: 'Göz Hastalıkları Uzmanı',
        consultationFee: 1500, biography: ''
      });
      loadStaff();
      loadDoctors();
    } catch (err) {
      alert(err.response?.data?.message || "Personel eklenemedi.");
    }
  };

  // HASTANIN ESKİ GEÇMİŞİNİ İNCELEME
  const handleOpenPatientHistory = async (patientName, nationalId) => {
    setActiveHistoryModalPatient({ name: patientName, tc: nationalId });
    try {
      const res = await getPatientHistory(nationalId);
      setPatientHistoryList(res.data);
    } catch {
      setPatientHistoryList([]);
    }
  };

  // TAKVİM VE SAAT KAPATMA
  const handleScheduleSave = async () => {
    if (!selectedDoctor) return;
    try {
      await updateDoctorSchedule(selectedDoctor.id, doctorScheduleList);
      alert("Haftalık çalışma takviminiz başarıyla kaydedildi!");
      fetchSlots(selectedDoctor.id, selectedDate);
    } catch {
      alert("Takvim güncellenirken hata oluştu.");
    }
  };

  const handleAddTimeOff = async (e) => {
    e.preventDefault();
    if (!selectedDoctor) return;
    try {
      const payload = {
        date: newTimeOff.date,
        startTime: newTimeOff.isAllDay ? null : newTimeOff.startTime,
        endTime: newTimeOff.isAllDay ? null : newTimeOff.endTime,
        reason: newTimeOff.reason
      };
      await addDoctorTimeOff(selectedDoctor.id, payload);
      alert("Seçili saat/gün başarıyla kapatıldı!");
      loadDoctorTimeOffsData(selectedDoctor.id);
      fetchSlots(selectedDoctor.id, selectedDate);
    } catch {
      alert("İzin tanımlanırken hata oluştu.");
    }
  };

  const handleDeleteTimeOff = async (id) => {
    if (!confirm("Bu izni kaldırıp saatleri tekrar randevuya açmak istiyor musunuz?")) return;
    try {
      await deleteDoctorTimeOff(id);
      loadDoctorTimeOffsData(selectedDoctor.id);
      fetchSlots(selectedDoctor.id, selectedDate);
    } catch {
      alert("İzin kaldırılamadı.");
    }
  };

  // ÇOKLU İŞLEM TAHSİLATI
  const handleAddProcedureRow = () => setTreatmentItems([...treatmentItems, { procedureName: '', price: 0 }]);
  const handleRemoveProcedureRow = (idx) => setTreatmentItems(treatmentItems.filter((_, i) => i !== idx));

  const handleChargeSubmit = async (e) => {
    e.preventDefault();
    try {
      const res = await chargeAppointment({
        appointmentId: chargingApp.id,
        paymentMethod: paymentMethod,
        treatments: treatmentItems.map(t => ({ procedureName: t.procedureName, price: parseFloat(t.price) || 0 }))
      });
      alert("Tahsilat ve işlemler başarıyla kaydedildi!");
      
      setDailyAppointments(prev => prev.map(a => 
        a.id === chargingApp.id 
          ? { 
              ...a, 
              paymentStatus: 'Paid', 
              paymentAmount: res.data.totalAmount, 
              paymentMethod: paymentMethod,
              treatmentDetails: res.data.treatmentSummary || treatmentItems.map(t => `${t.procedureName}: ${t.price} ₺`).join(' | ')
            } 
          : a
      ));

      setChargingApp(null);
      await loadPanelData();
      await loadRevenue(panelDate);
    } catch {
      alert("Tahsilat kaydedilemedi.");
    }
  };

  // RANDEVU AL
  const handleBookSubmit = async (e) => {
    e.preventDefault();
    if (!selectedSlot) return alert("Lütfen bir saat seçiniz.");

    setBookingStatus({ loading: true, success: null, message: '' });

    const payload = {
      doctorId: selectedDoctor.id,
      appointmentDate: new Date(selectedDate).toISOString(),
      slotTime: selectedSlot.time,
      nationalId: formData.nationalId.trim(),
      firstName: formData.firstName.trim().toUpperCase(),
      lastName: formData.lastName.trim().toUpperCase(),
      birthYear: parseInt(formData.birthYear, 10),
      email: formData.email.trim(),
      phoneNumber: formData.phoneNumber.trim(),
      patientComplaint: formData.patientComplaint
    };

    try {
      const res = await bookAppointment(payload);
      setBookingStatus({ loading: false, success: true, message: res.data.message });
      fetchSlots(selectedDoctor.id, selectedDate);
    } catch (err) {
      setBookingStatus({ 
        loading: false, 
        success: false, 
        message: err.response?.data?.message || "Bilgileri kontrol ediniz." 
      });
    }
  };

  // MUAYENE MODALI AÇILIRKEN VERİLERİ DOLDUR
  const handleOpenMedicalModal = (app) => {
    setActiveModalAppointment(app);
    setMedicalForm({
      diagnosis: app.diagnosis || '',
      clinicalNotes: app.clinicalNotes || '',
      prescription: app.prescription || '',
      followUpDate: ''
    });
  };

  // MUAYENE KAYDET / GÜNCELLE
  const handleMedicalSubmit = async (e) => {
    e.preventDefault();
    const data = new FormData();
    data.append('AppointmentId', activeModalAppointment.id);
    data.append('Diagnosis', medicalForm.diagnosis);
    data.append('ClinicalNotes', medicalForm.clinicalNotes || '');
    data.append('Prescription', medicalForm.prescription || '');
    if (medicalForm.followUpDate) data.append('FollowUpDate', new Date(medicalForm.followUpDate).toISOString());

    if (uploadedFiles && uploadedFiles.length > 0) {
      for (let i = 0; i < uploadedFiles.length; i++) {
        data.append('files', uploadedFiles[i]);
      }
    }

    try {
      await addMedicalRecord(data);
      alert("Muayene notu başarıyla güncellendi!");
      setActiveModalAppointment(null);
      setUploadedFiles([]);
      await loadPanelData();
    } catch (err) {
      alert("Hata: " + (err.response?.data?.message || err.message));
    }
  };

  // BANNER YÜKLE
  const handleBannerUpload = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    const form = new FormData();
    form.append('file', file);
    try {
      await uploadBanner(form);
      alert("Yeni vitrin resmi başarıyla yüklendi!");
      loadBanners();
    } catch {
      alert("Resim yüklenemedi.");
    }
  };

  const handleBannerDelete = async (id) => {
    if (!confirm("Bu resmi vitrinden kaldırmak istiyor musunuz?")) return;
    try {
      await deleteBanner(id);
      loadBanners();
    } catch {}
  };

  const pendingAppointments = dailyAppointments.filter(a => !a.hasMedicalRecord);
  const completedAppointments = dailyAppointments.filter(a => a.hasMedicalRecord);

  // Base64 Data URL ve Uzak URL Kontrolü
  const getFullImageUrl = (url) => {
    if (!url) return '';
    if (url.startsWith('data:') || url.startsWith('http://') || url.startsWith('https://')) {
      return url;
    }
    const cleanPath = url.startsWith('/') ? url : `/${url}`;
    return `https://clinic-api-bs2z.onrender.com${cleanPath}`;
  };

  return (
    <div className="min-h-screen bg-slate-50 text-slate-800">
      
      {/* CANLI BİLDİRİM */}
      {notification && (
        <div className="fixed top-5 right-5 z-50 bg-emerald-600 text-white px-5 py-3 rounded-xl shadow-2xl flex items-center gap-3 animate-bounce">
          <Bell className="w-6 h-6 animate-spin" />
          <div>
            <p className="font-bold text-sm">Canlı Randevu Uyarısı</p>
            <p className="text-xs">{notification}</p>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* 1. PUBLIC ANA SAYFA                                                       */}
      {/* ========================================================================= */}
      {currentView === 'public' && (
        <div>
          <header className="bg-white/95 backdrop-blur sticky top-0 z-40 border-b border-slate-200">
            <div className="max-w-7xl mx-auto px-6 h-20 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="bg-sky-600 text-white p-2.5 rounded-2xl shadow-md">
                  <Stethoscope className="w-7 h-7" />
                </div>
                <div>
                  <h1 className="font-extrabold text-2xl tracking-tight text-slate-900">E-KLİNİK</h1>
                  <p className="text-xs text-slate-500 font-medium">Göz & Cerrahi Tıp Merkezi</p>
                </div>
              </div>

              <nav className="hidden md:flex items-center gap-8 text-sm font-semibold text-slate-600">
                <a href="#hakkimizda" className="hover:text-sky-600 transition">Hakkımızda</a>
                <a href="#doktorlar" className="hover:text-sky-600 transition">Hekim Kadromuz</a>
                <a href="#randevu" className="hover:text-sky-600 transition">Online Randevu</a>
                <a href="#iletisim" className="hover:text-sky-600 transition">İletişim</a>
              </nav>

              <div className="flex items-center gap-4">
                <a href="#randevu" className="bg-sky-600 hover:bg-sky-700 text-white px-5 py-2.5 rounded-xl font-semibold text-sm shadow-lg shadow-sky-200 transition">
                  Hemen Randevu Al
                </a>
                <button 
                  onClick={() => setShowLoginModal(true)}
                  className="flex items-center gap-2 border border-slate-300 hover:bg-slate-100 px-4 py-2.5 rounded-xl font-semibold text-sm text-slate-700 transition"
                >
                  <LogIn className="w-4 h-4 text-sky-600" />
                  <span>Personel Girişi</span>
                </button>
              </div>
            </div>
          </header>

          {/* Hero & Dinamik Slider */}
          <section className="relative overflow-hidden bg-gradient-to-b from-sky-50/70 to-transparent pt-14 pb-20 border-b border-slate-200">
            <div className="max-w-7xl mx-auto px-6 grid md:grid-cols-2 gap-12 items-center">
              <div>
                <span className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-sky-100 text-sky-700 text-xs font-bold tracking-wide uppercase mb-6">
                  <Award className="w-4 h-4" /> 2011'den Beri Güvenle Hizmetinizde
                </span>
                <h2 className="text-4xl md:text-5xl font-black text-slate-900 leading-tight">
                  Göz Sağlığınızda İleri Teknoloji ve Uzman Kadro.
                </h2>
                <p className="mt-6 text-lg text-slate-600 leading-relaxed">
                  Son teknoloji retina tomografisi, akıllı lens cerrahisi ve uzman göz hekimlerimizle 
                  aynı gün randevu ve anında teşhis ayrıcalığını yaşayın.
                </p>

                <div className="mt-8 flex flex-wrap gap-4">
                  <a href="#randevu" className="bg-sky-600 hover:bg-sky-700 text-white px-6 py-3.5 rounded-xl font-bold text-base shadow-xl shadow-sky-200 flex items-center gap-2 transition">
                    Randevu Saatini Seç <ChevronRight className="w-5 h-5" />
                  </a>
                  <div className="flex items-center gap-3 px-4 py-2 border border-slate-200 rounded-xl bg-white shadow-sm">
                    <ShieldCheck className="w-6 h-6 text-emerald-500" />
                    <span className="text-xs font-semibold text-slate-600">NVI T.C. Doğrulamalı Güvenli Kayıt</span>
                  </div>
                </div>

                <div className="mt-12 grid grid-cols-3 gap-6 pt-8 border-t border-slate-200">
                  <div>
                    <p className="text-3xl font-extrabold text-slate-900">15+</p>
                    <p className="text-xs font-medium text-slate-500 mt-1">Yıllık Klinik Tecrübesi</p>
                  </div>
                  <div>
                    <p className="text-3xl font-extrabold text-sky-600">45.000+</p>
                    <p className="text-xs font-medium text-slate-500 mt-1">Başarılı Muayene & Tedavi</p>
                  </div>
                  <div>
                    <p className="text-3xl font-extrabold text-emerald-600">%99</p>
                    <p className="text-xs font-medium text-slate-500 mt-1">Hasta Memnuniyeti</p>
                  </div>
                </div>
              </div>

              {/* Slider Görseli */}
              <div className="relative">
                <div className="w-full h-[460px] rounded-3xl overflow-hidden shadow-2xl border-4 border-white bg-slate-200 relative">
                  {banners.map((b, idx) => (
                    <div 
                      key={idx}
                      className={`absolute inset-0 transition-opacity duration-1000 ${
                        idx === currentBannerIdx ? 'opacity-100 z-10' : 'opacity-0 z-0'
                      }`}
                    >
                      <img 
                        src={getFullImageUrl(b.imageUrl)} 
                        alt="Klinik Görseli" 
                        className="w-full h-full object-cover"
                      />
                      <div className="absolute inset-0 bg-gradient-to-t from-slate-900/85 via-transparent to-transparent flex flex-col justify-end p-8 text-white">
                        <span className="text-xs font-semibold text-sky-300">Modern Cerrahi & Tanı Merkezi</span>
                        <h3 className="text-xl font-bold mt-1">{b.title || 'Gelişmiş Göz Dibi Fotoğraflama ve Lazer'}</h3>
                        <p className="text-xs text-slate-200 mt-1">{b.subtitle || 'Çankaya / Ankara adresinde haftanın 6 günü hizmetinizdeyiz.'}</p>
                      </div>
                    </div>
                  ))}

                  {banners.length > 1 && (
                    <div className="absolute bottom-4 right-6 z-20 flex gap-2">
                      {banners.map((_, i) => (
                        <button
                          key={i}
                          onClick={() => setCurrentBannerIdx(i)}
                          className={`w-2.5 h-2.5 rounded-full transition ${
                            i === currentBannerIdx ? 'bg-sky-500 w-6' : 'bg-white/60'
                          }`}
                        />
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </div>
          </section>

          {/* Hakkımızda */}
          <section id="hakkimizda" className="py-20 bg-white border-b border-slate-200">
            <div className="max-w-7xl mx-auto px-6">
              <div className="text-center max-w-2xl mx-auto mb-16">
                <span className="text-sky-600 font-bold text-xs uppercase tracking-wider">Hakkımızda</span>
                <h2 className="text-3xl font-black text-slate-900 mt-2">Sağlığınız İçin En Yüksek Standartlar</h2>
                <p className="text-slate-600 text-sm mt-3">
                  E-Klinik, uluslararası akreditasyon standartlarına sahip medikal altyapısıyla güvenilir sağlık hizmeti sunar.
                </p>
              </div>

              <div className="grid md:grid-cols-3 gap-8">
                <div className="p-8 rounded-3xl bg-slate-50 border border-slate-200 hover:shadow-lg transition">
                  <div className="w-12 h-12 rounded-2xl bg-sky-100 text-sky-600 flex items-center justify-center mb-6">
                    <Activity className="w-6 h-6" />
                  </div>
                  <h3 className="font-bold text-lg text-slate-900 mb-2">İleri Tanı Teknolojileri</h3>
                  <p className="text-xs text-slate-600 leading-relaxed">
                    Dijital retina tomografisi, kornea topografisi ve bilgisayarlı görme testleriyle erken teşhis imkanı.
                  </p>
                </div>

                <div className="p-8 rounded-3xl bg-slate-50 border border-slate-200 hover:shadow-lg transition">
                  <div className="w-12 h-12 rounded-2xl bg-emerald-100 text-emerald-600 flex items-center justify-center mb-6">
                    <ShieldCheck className="w-6 h-6" />
                  </div>
                  <h3 className="font-bold text-lg text-slate-900 mb-2">Steril Cerrahi Ameliyathane</h3>
                  <p className="text-xs text-slate-600 leading-relaxed">
                    Laminar hava akımlı HEPA filtreli ameliyathanelerimizde katarakt ve refraktif cerrahi en üst hijyenle yapılır.
                  </p>
                </div>

                <div className="p-8 rounded-3xl bg-slate-50 border border-slate-200 hover:shadow-lg transition">
                  <div className="w-12 h-12 rounded-2xl bg-purple-100 text-purple-600 flex items-center justify-center mb-6">
                    <Award className="w-6 h-6" />
                  </div>
                  <h3 className="font-bold text-lg text-slate-900 mb-2">Hasta Odaklı Süreç</h3>
                  <p className="text-xs text-slate-600 leading-relaxed">
                    Online randevu alma, dijital tahlil/röntgen arşivi ve düzenli hekim takibi ile konforlu deneyim.
                  </p>
                </div>
              </div>
            </div>
          </section>

          {/* Hekim Kadromuz */}
          <section id="doktorlar" className="py-20 bg-slate-50 border-b border-slate-200">
            <div className="max-w-7xl mx-auto px-6">
              <div className="text-center max-w-2xl mx-auto mb-16">
                <span className="text-sky-600 font-bold text-xs uppercase tracking-wider">Uzman Kadromuz</span>
                <h2 className="text-3xl font-black text-slate-900 mt-2">Deneyimli Hekimlerimizle Tanışın</h2>
                <p className="text-slate-600 text-sm mt-3">
                  Alanında tecrübeli uzman doktorlarımızdan randevunuzu doğrudan seçip oluşturabilirsiniz.
                </p>
              </div>

              <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-8">
                {doctors.map(doc => (
                  <div key={doc.id} className="bg-white rounded-3xl border border-slate-200 p-6 shadow-sm hover:shadow-md transition">
                    <div className="w-20 h-20 rounded-2xl bg-sky-100 text-sky-700 flex items-center justify-center font-black text-2xl mb-4">
                      {doc.fullName ? doc.fullName.replace('Op. Dr. ', '').slice(0, 2).toUpperCase() : 'DR'}
                    </div>
                    <h3 className="font-extrabold text-lg text-slate-900">{doc.fullName}</h3>
                    <p className="text-xs font-semibold text-sky-600 mt-0.5">{doc.specialty}</p>
                    <p className="text-xs text-slate-500 mt-3 line-clamp-3">
                      {doc.biography || 'Katarakt cerrahisi, akıllı lens uygulamaları ve retina tanı ve tedavisi uzmanı.'}
                    </p>
                    <div className="mt-6 pt-4 border-t border-slate-100 flex items-center justify-between">
                      <span className="text-xs font-bold text-slate-700">Muayene: {doc.consultationFee} ₺</span>
                      <a 
                        href="#randevu" 
                        onClick={() => setSelectedDoctor(doc)}
                        className="bg-sky-50 hover:bg-sky-600 hover:text-white text-sky-700 px-3.5 py-1.5 rounded-xl font-bold text-xs transition"
                      >
                        Randevu Seç
                      </a>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </section>

          {/* Online Randevu */}
          <section id="randevu" className="py-20 max-w-7xl mx-auto px-6">
            <div className="text-center max-w-2xl mx-auto mb-12">
              <span className="text-sky-600 font-bold text-xs uppercase tracking-wider">Hızlı & Kolay</span>
              <h2 className="text-3xl font-black text-slate-900 mt-2">Online Randevu Oluşturun</h2>
              <p className="text-slate-600 text-sm mt-2">Hekiminizi ve saatinizi seçin; NVI onaylı güvenli randevunuzu hemen alın.</p>
            </div>

            <div className="grid lg:grid-cols-12 gap-8 items-start">
              {/* Sol: Doktor ve Saat Seçimi */}
              <div className="lg:col-span-5 bg-white p-6 rounded-3xl shadow-sm border border-slate-200">
                <h3 className="font-bold text-base text-slate-900 flex items-center gap-2 mb-4">
                  <User className="w-5 h-5 text-sky-600" /> 1. Hekim ve Tarih Seçimi
                </h3>

                <div className="space-y-3 mb-6">
                  {doctors.map(doc => (
                    <div 
                      key={doc.id}
                      onClick={() => setSelectedDoctor(doc)}
                      className={`p-3.5 rounded-2xl border cursor-pointer transition flex items-center justify-between ${
                        selectedDoctor?.id === doc.id ? 'border-sky-600 bg-sky-50/50 shadow-sm' : 'border-slate-200 hover:border-slate-300'
                      }`}
                    >
                      <div>
                        <p className="font-bold text-sm text-slate-900">{doc.fullName}</p>
                        <p className="text-xs text-slate-500">{doc.specialty}</p>
                      </div>
                      <span className="text-xs font-bold px-2.5 py-1 bg-white border border-slate-200 rounded-lg text-sky-700">
                        {doc.consultationFee} ₺
                      </span>
                    </div>
                  ))}
                </div>

                <div className="mb-6">
                  <label className="block text-xs font-bold text-slate-700 mb-2">Randevu Tarihi</label>
                  <input 
                    type="date" 
                    value={selectedDate}
                    min={new Date().toLocaleDateString('en-CA')}
                    onChange={(e) => setSelectedDate(e.target.value)}
                    className="w-full p-3 rounded-xl border border-slate-300 font-semibold text-sm outline-none"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-2">Uygun Saatler ({selectedDate})</label>
                  {loadingSlots ? (
                    <p className="text-xs text-slate-400 py-4 text-center">Saatler yükleniyor...</p>
                  ) : slots.length === 0 ? (
                    <div className="text-xs text-amber-700 bg-amber-50 p-4 rounded-xl border border-amber-200">
                      Seçili tarihte randevuya açık saat bulunmuyor.
                    </div>
                  ) : (
                    <div className="grid grid-cols-4 gap-2">
                      {slots.map(slot => {
                        const isPast = isSlotInPast(slot.formattedTime);
                        const isAvailable = slot.isAvailable && !isPast;

                        return (
                          <button
                            key={slot.formattedTime}
                            type="button"
                            disabled={!isAvailable}
                            onClick={() => setSelectedSlot(slot)}
                            className={`py-2 text-xs font-bold rounded-xl transition ${
                              !isAvailable
                                ? 'bg-slate-100 text-slate-400 cursor-not-allowed line-through'
                                : selectedSlot?.formattedTime === slot.formattedTime
                                  ? 'bg-sky-600 text-white shadow-md'
                                  : 'bg-slate-50 text-slate-700 border border-slate-200 hover:border-sky-500 hover:bg-sky-50'
                            }`}
                          >
                            {slot.formattedTime}
                          </button>
                        );
                      })}
                    </div>
                  )}
                </div>
              </div>

              {/* Sağ: Hasta Bilgileri */}
              <div className="lg:col-span-7 bg-white p-8 rounded-3xl shadow-sm border border-slate-200">
                <h3 className="font-bold text-base text-slate-900 flex items-center gap-2 mb-2">
                  <ShieldCheck className="w-5 h-5 text-emerald-600" /> 2. Hasta Kimlik & İletişim Bilgileri
                </h3>
                <p className="text-xs text-slate-500 mb-6">T.C. Kimlik No, Ad, Soyad ve Doğum Yılı NVI ile doğrulanır.</p>

                <form onSubmit={handleBookSubmit} className="space-y-4">
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1">T.C. Kimlik No *</label>
                      <input 
                        type="text" 
                        maxLength={11}
                        required
                        value={formData.nationalId}
                        onChange={(e) => setFormData({...formData, nationalId: e.target.value.replace(/\D/g, '')})}
                        placeholder="11 Haneli T.C."
                        className="w-full p-3 rounded-xl border border-slate-300 text-sm outline-none"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1">Doğum Yılı *</label>
                      <input 
                        type="number" 
                        required
                        placeholder="Örn: 1995"
                        value={formData.birthYear}
                        onChange={(e) => setFormData({...formData, birthYear: e.target.value})}
                        className="w-full p-3 rounded-xl border border-slate-300 text-sm outline-none"
                      />
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1">Adınız *</label>
                      <input 
                        type="text" 
                        required
                        value={formData.firstName}
                        onChange={(e) => setFormData({...formData, firstName: e.target.value})}
                        placeholder="ADINIZ"
                        className="w-full p-3 rounded-xl border border-slate-300 text-sm outline-none uppercase"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1">Soyadınız *</label>
                      <input 
                        type="text" 
                        required
                        value={formData.lastName}
                        onChange={(e) => setFormData({...formData, lastName: e.target.value})}
                        placeholder="SOYADINIZ"
                        className="w-full p-3 rounded-xl border border-slate-300 text-sm outline-none uppercase"
                      />
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1">Telefon Numarası *</label>
                      <input 
                        type="tel" 
                        required
                        placeholder="05xxxxxxxxx"
                        value={formData.phoneNumber}
                        onChange={(e) => setFormData({...formData, phoneNumber: e.target.value})}
                        className="w-full p-3 rounded-xl border border-slate-300 text-sm outline-none"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1">E-Posta (Bildirim İçin) *</label>
                      <input 
                        type="email" 
                        required
                        placeholder="ornek@mail.com"
                        value={formData.email}
                        onChange={(e) => setFormData({...formData, email: e.target.value})}
                        className="w-full p-3 rounded-xl border border-slate-300 text-sm outline-none"
                      />
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-slate-700 mb-1">Şikayet / Not</label>
                    <textarea 
                      rows={2}
                      placeholder="Şikayetinizi kısaca belirtin..."
                      value={formData.patientComplaint}
                      onChange={(e) => setFormData({...formData, patientComplaint: e.target.value})}
                      className="w-full p-3 rounded-xl border border-slate-300 text-sm outline-none"
                    />
                  </div>

                  {bookingStatus.message && (
                    <div className={`p-4 rounded-xl text-xs font-semibold flex items-center gap-3 ${
                      bookingStatus.success ? 'bg-emerald-50 text-emerald-800 border border-emerald-200' : 'bg-rose-50 text-rose-800 border border-rose-200'
                    }`}>
                      {bookingStatus.success ? <CheckCircle className="w-5 h-5 text-emerald-600 shrink-0" /> : <AlertCircle className="w-5 h-5 text-rose-600 shrink-0" />}
                      <span>{bookingStatus.message}</span>
                    </div>
                  )}

                  <button
                    type="submit"
                    disabled={bookingStatus.loading || !selectedSlot}
                    className={`w-full py-4 rounded-xl font-bold text-white shadow-lg transition ${
                      bookingStatus.loading || !selectedSlot ? 'bg-slate-400 cursor-not-allowed' : 'bg-sky-600 hover:bg-sky-700 shadow-sky-200'
                    }`}
                  >
                    {bookingStatus.loading ? 'NVI Doğrulanıyor...' : selectedSlot ? `${selectedDate} - ${selectedSlot.formattedTime} Randevusunu Onayla` : 'Saat Seçiniz'}
                  </button>
                </form>
              </div>
            </div>
          </section>

          {/* İletişim & Footer */}
          <footer id="iletisim" className="bg-slate-900 text-slate-400 py-16 border-t border-slate-800">
            <div className="max-w-7xl mx-auto px-6 grid md:grid-cols-4 gap-10 text-sm">
              <div className="md:col-span-2">
                <div className="flex items-center gap-3 mb-4">
                  <div className="bg-sky-600 text-white p-2 rounded-xl">
                    <Stethoscope className="w-5 h-5" />
                  </div>
                  <h4 className="text-white font-extrabold text-lg">E-KLİNİK GÖZ & CERRAHİ TIP MERKEZİ</h4>
                </div>
                <p className="text-slate-400 leading-relaxed text-xs max-w-md">
                  Sağlık standartlarında, göz hastalıkları ve cerrahisinde 15 yıllık tecrübemizle hizmetinizdeyiz.
                </p>
                <p className="mt-6 text-xs text-slate-500">© 2026 E-Klinik Yönetim Sistemi. Tüm hakları saklıdır.</p>
              </div>

              <div>
                <h5 className="text-white font-bold text-sm mb-4">İletişim & Lokasyon</h5>
                <p className="flex items-center gap-2 text-xs text-slate-300 mb-2">
                  <MapPin className="w-4 h-4 text-sky-500 shrink-0" /> Tunalı Hilmi Cad. No: 124/B, Çankaya / Ankara
                </p>
                <p className="flex items-center gap-2 text-xs text-slate-300 mb-2">
                  <Phone className="w-4 h-4 text-sky-500 shrink-0" /> (0312) 444 0 555
                </p>
                <p className="flex items-center gap-2 text-xs text-slate-300">
                  <Mail className="w-4 h-4 text-sky-500 shrink-0" /> info@eklinik.com
                </p>
              </div>

              <div>
                <h5 className="text-white font-bold text-sm mb-4">Çalışma Saatleri</h5>
                <p className="text-xs text-slate-300 mb-1">Hafta İçi: 09:00 - 17:00</p>
                <p className="text-xs text-slate-300 mb-1">Cumartesi: 09:00 - 14:00</p>
                <p className="text-xs text-rose-400 font-bold">Pazar: Kapalı</p>
              </div>
            </div>
          </footer>
        </div>
      )}

      {/* ========================================================================= */}
      {/* 2. DOKTOR VE SEKRETER PANELİ                                             */}
      {/* ========================================================================= */}
      {currentView === 'panel' && (
        <div className="min-h-screen bg-slate-100 flex">
          
          {/* Sol Menü */}
          <aside className="w-64 bg-white border-r border-slate-200 flex flex-col justify-between shrink-0">
            <div>
              <div className="p-6 border-b border-slate-100 flex items-center gap-3">
                <div className="bg-sky-600 text-white p-2 rounded-xl">
                  <Stethoscope className="w-6 h-6" />
                </div>
                <div>
                  <h2 className="font-extrabold text-lg text-slate-900">E-Klinik</h2>
                  <span className="text-xs text-emerald-600 font-bold">● Yetkili Paneli</span>
                </div>
              </div>

              <div className="p-4 space-y-1">
                <p className="px-3 text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-2">Klinik Yönetimi</p>
                <button 
                  onClick={() => setPanelTab('appointments')}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl font-semibold text-xs transition ${
                    panelTab === 'appointments' ? 'bg-sky-50 text-sky-700' : 'text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  <CalendarDays className="w-4 h-4" /> Günlük Muayene Listesi
                </button>
                <button 
                  onClick={() => setPanelTab('revenue')}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl font-semibold text-xs transition ${
                    panelTab === 'revenue' ? 'bg-sky-50 text-sky-700' : 'text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  <DollarSign className="w-4 h-4" /> Kasa & Günlük Ciro
                </button>
                <button 
                  onClick={() => setPanelTab('schedule')}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl font-semibold text-xs transition ${
                    panelTab === 'schedule' ? 'bg-sky-50 text-sky-700' : 'text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  <Settings className="w-4 h-4" /> Takvim & Saat Kapatma
                </button>
                <button 
                  onClick={() => setPanelTab('history')}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl font-semibold text-xs transition ${
                    panelTab === 'history' ? 'bg-sky-50 text-sky-700' : 'text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  <FolderOpen className="w-4 h-4" /> Tahlil / Röntgen Arşivi
                </button>

                <p className="px-3 text-[11px] font-bold text-slate-400 uppercase tracking-wider mt-6 mb-2">Ayarlar & Personel</p>
                <button 
                  onClick={() => setPanelTab('staff')}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl font-semibold text-xs transition ${
                    panelTab === 'staff' ? 'bg-sky-50 text-sky-700' : 'text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  <Users className="w-4 h-4" /> Personel & Sekreter Yönetimi
                </button>
                <button 
                  onClick={() => setPanelTab('banners')}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl font-semibold text-xs transition ${
                    panelTab === 'banners' ? 'bg-sky-50 text-sky-700' : 'text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  <Layers className="w-4 h-4" /> Vitrin Resimleri (Slider)
                </button>
                <button 
                  onClick={() => setPanelTab('profile')}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl font-semibold text-xs transition ${
                    panelTab === 'profile' ? 'bg-sky-50 text-sky-700' : 'text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  <Lock className="w-4 h-4" /> Profil & Şifre Değiştir
                </button>
              </div>
            </div>

            <div className="p-4 border-t border-slate-100">
              <div className="p-3 bg-slate-50 rounded-2xl flex items-center justify-between">
                <div>
                  <p className="font-bold text-xs text-slate-900">{loggedUser?.fullName || selectedDoctor?.fullName}</p>
                  <p className="text-[10px] text-slate-500">{loggedUser?.email}</p>
                </div>
                <button 
                  onClick={handleLogout}
                  className="p-2 hover:bg-white rounded-xl text-rose-600 transition"
                  title="Güvenli Çıkış Yap"
                >
                  <LogOut className="w-4 h-4" />
                </button>
              </div>
            </div>
          </aside>

          {/* Sağ İçerik Alanı */}
          <main className="flex-1 flex flex-col min-w-0">
            <header className="h-20 bg-white border-b border-slate-200 px-8 flex items-center justify-between">
              <div className="relative w-80">
                <Search className="w-4 h-4 text-slate-400 absolute left-3 top-3.5" />
                <input 
                  type="text" 
                  placeholder="Hasta ara (11 haneli T.C.)..."
                  value={searchTc}
                  onChange={(e) => setSearchTc(e.target.value)}
                  className="w-full pl-9 pr-4 py-2.5 rounded-xl bg-slate-50 border border-slate-200 text-xs font-medium focus:bg-white outline-none"
                />
              </div>

              <div className="flex items-center gap-4">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-semibold text-slate-500">Tarih:</span>
                  <input 
                    type="date" 
                    value={panelDate}
                    onChange={(e) => setPanelDate(e.target.value)}
                    className="p-2 rounded-xl border border-slate-200 text-xs font-bold bg-white"
                  />
                </div>
                <button 
                  onClick={() => setCurrentView('public')}
                  className="bg-sky-600 hover:bg-sky-700 text-white px-4 py-2 rounded-xl text-xs font-bold transition shadow-sm"
                >
                  + Randevu Ekranı
                </button>
              </div>
            </header>

            <div className="p-8 overflow-y-auto space-y-8">
              
              {/* TAB 1: GÜNLÜK MUAYENE İKİ AYRI LİSTE */}
              {panelTab === 'appointments' && (
                <div className="space-y-8">
                  
                  {/* BÖLÜM 1: BUGÜN BEKLEYEN AKTİF RANDEVULAR */}
                  <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
                    <div className="p-6 border-b border-slate-100 flex items-center justify-between bg-sky-50/40">
                      <div>
                        <h3 className="font-extrabold text-base text-slate-900 flex items-center gap-2">
                          <Clock className="w-5 h-5 text-sky-600" /> Bugün Bekleyen Aktif Randevular ({panelDate})
                        </h3>
                        <p className="text-xs text-slate-500 mt-0.5">Henüz muayenesi yapılmamış, sıradaki hastalar.</p>
                      </div>
                      <span className="text-xs font-bold bg-sky-100 text-sky-800 px-3 py-1.5 rounded-xl">
                        {pendingAppointments.length} Bekleyen Hasta
                      </span>
                    </div>

                    <div className="divide-y divide-slate-100">
                      {pendingAppointments.length === 0 ? (
                        <div className="p-10 text-center text-slate-400 text-xs">
                          Şu an bekleyen randevu bulunmuyor (Tüm hastalar muayene edildi veya randevu yok).
                        </div>
                      ) : (
                        pendingAppointments.map((app) => (
                          <div key={app.id} className="p-5 flex items-center justify-between hover:bg-slate-50 transition">
                            <div className="flex items-center gap-4">
                              <span className="text-sm font-black text-sky-600 bg-sky-50 px-3 py-2 rounded-xl border border-sky-200">
                                {app.time}
                              </span>
                              <div>
                                <div className="flex items-center gap-2">
                                  <p className="font-bold text-sm text-slate-900">{app.patientName}</p>
                                  <button
                                    onClick={() => handleOpenPatientHistory(app.patientName, app.nationalId)}
                                    className="text-[10px] text-sky-600 hover:text-sky-800 font-bold bg-sky-50 px-2 py-0.5 rounded-md flex items-center gap-1 border border-sky-200"
                                  >
                                    <History className="w-3 h-3" /> Eski Geçmiş
                                  </button>
                                </div>
                                <p className="text-xs text-slate-400">T.C: {app.nationalId} • Tel: {app.phoneNumber}</p>
                              </div>
                              <span className={`text-[10px] font-bold px-2 py-0.5 rounded-lg ${
                                app.paymentStatus === 'Paid' ? 'bg-emerald-100 text-emerald-800' : 'bg-rose-100 text-rose-800'
                              }`}>
                                {app.paymentStatus === 'Paid' ? `✓ Ödendi (${app.paymentAmount} ₺)` : 'Ödeme Bekliyor'}
                              </span>
                            </div>

                            <div className="flex items-center gap-3">
                              {app.paymentStatus !== 'Paid' && (
                                <button
                                  onClick={() => {
                                    setChargingApp(app);
                                    setTreatmentItems([{ procedureName: 'Genel Muayene', price: app.paymentAmount || 1500 }]);
                                  }}
                                  className="px-3.5 py-2 rounded-xl text-xs font-bold border border-slate-200 hover:bg-emerald-50 hover:text-emerald-700 transition flex items-center gap-1.5"
                                >
                                  <CreditCard className="w-3.5 h-3.5" /> Ücret Tahsil Et
                                </button>
                              )}

                              <button 
                                onClick={() => {
                                  setActiveModalAppointment(app);
                                  setMedicalForm({
                                    diagnosis: '',
                                    clinicalNotes: '',
                                    prescription: '',
                                    followUpDate: ''
                                  });
                                }}
                                className="bg-sky-600 hover:bg-sky-700 text-white px-4 py-2 rounded-xl text-xs font-bold transition flex items-center gap-1.5 shadow-sm"
                              >
                                <Stethoscope className="w-3.5 h-3.5" /> Muayene Başlat & Not Ekle
                              </button>
                            </div>
                          </div>
                        ))
                      )}
                    </div>
                  </div>

                  {/* BÖLÜM 2: BUGÜN MUAYENESİ YAPILAN HASTALAR */}
                  <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
                    <div className="p-6 border-b border-slate-100 flex items-center justify-between bg-emerald-50/40">
                      <div>
                        <h3 className="font-extrabold text-base text-slate-900 flex items-center gap-2">
                          <CheckCircle className="w-5 h-5 text-emerald-600" /> Bugün Muayenesi Tamamlanan Hastalar ({panelDate})
                        </h3>
                        <p className="text-xs text-slate-500 mt-0.5">Teşhisi, notu ve reçetesi girilmiş hastalar. Faturaya ek işlem ekleyebilir, reçeteyi veya röntgenleri güncelleyebilirsiniz.</p>
                      </div>
                      <span className="text-xs font-bold bg-emerald-100 text-emerald-800 px-3 py-1.5 rounded-xl">
                        {completedAppointments.length} Muayene Tamamlandı
                      </span>
                    </div>

                    <div className="divide-y divide-slate-100">
                      {completedAppointments.length === 0 ? (
                        <div className="p-10 text-center text-slate-400 text-xs">
                          Henüz bugün muayenesi tamamlanan hasta bulunmuyor.
                        </div>
                      ) : (
                        completedAppointments.map((app) => (
                          <div key={app.id} className="p-6 hover:bg-slate-50/80 transition space-y-3">
                            <div className="flex items-center justify-between">
                              <div className="flex items-center gap-3">
                                <span className="text-sm font-black text-slate-700 bg-slate-100 px-3 py-1 rounded-xl">
                                  {app.time}
                                </span>
                                <div>
                                  <div className="flex items-center gap-2">
                                    <span className="font-extrabold text-sm text-slate-900">{app.patientName}</span>
                                    <button
                                      onClick={() => handleOpenPatientHistory(app.patientName, app.nationalId)}
                                      className="text-[10px] text-sky-600 hover:text-sky-800 font-bold bg-sky-50 px-2 py-0.5 rounded-md flex items-center gap-1 border border-sky-200"
                                    >
                                      <History className="w-3 h-3" /> Eski Geçmiş
                                    </button>
                                  </div>
                                  <span className="text-xs text-slate-400">T.C: {app.nationalId}</span>
                                </div>
                                
                                <span className={`text-[10px] font-bold px-2 py-0.5 rounded-lg ${
                                  app.paymentStatus === 'Paid' ? 'bg-emerald-100 text-emerald-800' : 'bg-rose-100 text-rose-800'
                                }`}>
                                  {app.paymentStatus === 'Paid' ? `✓ Ödendi (${app.paymentAmount} ₺)` : 'Ödeme Bekliyor'}
                                </span>
                              </div>

                              <div className="flex items-center gap-2">
                                {/* FATURAYA / TEDAVİYE EK İŞLEM KALEMİ EKLEME */}
                                <button
                                  onClick={() => {
                                    setChargingApp(app);
                                    setTreatmentItems([
                                      { procedureName: 'Ek Tedavi / Tahlil', price: 500 }
                                    ]);
                                  }}
                                  className="px-3 py-1.5 rounded-xl text-xs font-bold bg-sky-50 text-sky-700 border border-sky-200 hover:bg-sky-100 transition flex items-center gap-1"
                                >
                                  <PlusCircle className="w-3.5 h-3.5" /> + Faturaya Ekle
                                </button>

                                {app.paymentStatus !== 'Paid' && (
                                  <button
                                    onClick={() => {
                                      setChargingApp(app);
                                      setTreatmentItems([{ procedureName: 'Genel Muayene', price: app.paymentAmount || 1500 }]);
                                    }}
                                    className="px-3 py-1.5 rounded-xl text-xs font-bold bg-rose-50 text-rose-700 border border-rose-200 hover:bg-rose-100 transition flex items-center gap-1.5"
                                  >
                                    <CreditCard className="w-3.5 h-3.5" /> Ücret Al
                                  </button>
                                )}

                                <button 
                                  onClick={() => handleOpenMedicalModal(app)}
                                  className="border border-slate-200 hover:bg-sky-50 hover:text-sky-700 px-3.5 py-1.5 rounded-xl text-xs font-bold text-slate-700 transition flex items-center gap-1.5"
                                >
                                  <Stethoscope className="w-3.5 h-3.5 text-sky-600" /> Raporu İncele / Röntgen Ekle
                                </button>
                              </div>
                            </div>

                            {/* DOĞRUDAN SATIRDA GÖRÜNEN TANI, NOT, REÇETE, DOSYALAR VE FATURA KALEMLERİ */}
                            <div className="p-4 rounded-2xl bg-emerald-50/60 border border-emerald-100 text-xs space-y-2">
                              <div>
                                <span className="font-bold text-emerald-950">Teşhis / Tanı: </span>
                                <span className="font-semibold text-emerald-900">{app.diagnosis || 'Tanı girilmedi'}</span>
                              </div>

                              {/* KALICI FATURA VE YAPILAN İŞLEM DÖKÜMÜ */}
                              {app.treatmentDetails && (
                                <div className="p-2 bg-white/80 rounded-xl border border-emerald-200/60 flex items-center gap-2">
                                  <span className="font-bold text-emerald-900 shrink-0">💳 Yapılan İşlemler / Fatura:</span>
                                  <span className="font-semibold text-slate-700">{app.treatmentDetails}</span>
                                </div>
                              )}

                              {app.clinicalNotes ? (
                                <div>
                                  <span className="font-bold text-slate-700">Hekim Muayene Notu: </span>
                                  <span className="text-slate-600">{app.clinicalNotes}</span>
                                </div>
                              ) : (
                                <div className="text-slate-400 italic">Muayene notu girilmedi.</div>
                              )}
                              
                              {app.prescription ? (
                                <div>
                                  <span className="font-bold text-emerald-800">Reçete / İlaçlar: </span>
                                  <span className="text-emerald-700 font-mono bg-white px-2 py-0.5 rounded border border-emerald-200 inline-block">
                                    {app.prescription}
                                  </span>
                                </div>
                              ) : null}

                              {app.attachments && app.attachments.length > 0 && (
                                <div className="pt-2 border-t border-emerald-200/60 flex flex-wrap gap-2 items-center">
                                  <span className="font-bold text-slate-700 text-[11px]">Tahlil / Röntgen Dosyaları:</span>
                                  {app.attachments.map((att, i) => (
                                    <a
                                      key={i}
                                      href={getFullImageUrl(att.downloadUrl)}
                                      target="_blank"
                                      rel="noreferrer"
                                      className="flex items-center gap-1 px-2.5 py-1 bg-white border border-slate-300 rounded-lg text-sky-600 hover:bg-sky-50 font-bold transition text-[11px]"
                                    >
                                      <Eye className="w-3 h-3" /> {att.originalFileName || 'Görüntüle'}
                                    </a>
                                  ))}
                                </div>
                              )}
                            </div>
                          </div>
                        ))
                      )}
                    </div>
                  </div>

                </div>
              )}

             {/* TAB 2: KASA VE CİRO (4 KART: TOPLAM, NAKİT, KREDİ KARTI, HAVALE/EFT) */}
              {panelTab === 'revenue' && dailyRevenueData && (
                <div className="bg-white p-8 rounded-3xl border border-slate-200 shadow-sm space-y-6">
                  <h3 className="font-extrabold text-base text-slate-900">{panelDate} Kasa Raporu</h3>

                  <div className="grid grid-cols-4 gap-4">
                    <div className="bg-emerald-50 p-5 rounded-2xl border border-emerald-100">
                      <span className="text-[11px] font-bold text-emerald-800 uppercase">Toplam Ciro</span>
                      <h2 className="text-2xl font-black text-emerald-800 mt-2">{dailyRevenueData.totalRevenue} ₺</h2>
                    </div>
                    <div className="bg-sky-50 p-5 rounded-2xl border border-sky-100">
                      <span className="text-[11px] font-bold text-sky-800 uppercase">Nakit</span>
                      <h2 className="text-2xl font-black text-sky-800 mt-2">{dailyRevenueData.totalCash} ₺</h2>
                    </div>
                    <div className="bg-purple-50 p-5 rounded-2xl border border-purple-100">
                      <span className="text-[11px] font-bold text-purple-800 uppercase">Kredi Kartı</span>
                      <h2 className="text-2xl font-black text-purple-800 mt-2">{dailyRevenueData.totalCard} ₺</h2>
                    </div>
                    <div className="bg-amber-50 p-5 rounded-2xl border border-amber-100">
                      <span className="text-[11px] font-bold text-amber-800 uppercase">Havale / EFT</span>
                      <h2 className="text-2xl font-black text-amber-800 mt-2">{dailyRevenueData.totalTransfer || 0} ₺</h2>
                    </div>
                  </div>

                  <div className="mt-8">
                    <h4 className="font-bold text-sm text-slate-800 mb-3">Günün Tahsilat Hareketleri:</h4>
                    <div className="border border-slate-200 rounded-2xl overflow-hidden divide-y divide-slate-100 text-xs">
                      {dailyRevenueData.transactions.map((tr) => (
                        <div key={tr.id} className="p-4 flex justify-between items-center bg-slate-50/50">
                          <div>
                            <span className="font-bold text-slate-900">{tr.patientName}</span>
                            <span className="text-slate-400 ml-2 font-mono">{tr.time}</span>
                            {tr.treatmentDetails && (
                              <p className="text-[11px] text-slate-500 mt-0.5">{tr.treatmentDetails}</p>
                            )}
                          </div>
                          <div className="flex items-center gap-4">
                            <span className="px-2.5 py-1 rounded-lg bg-white border border-slate-200 font-semibold">{tr.paymentMethod}</span>
                            <span className="font-black text-emerald-600 text-sm">{tr.amount} ₺</span>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              )}

              {/* TAB 3: TAKVİM & SAAT KAPATMA */}
              {panelTab === 'schedule' && (
                <div className="space-y-8">
                  <div className="bg-white p-8 rounded-3xl border border-slate-200 shadow-sm space-y-6">
                    <div className="flex items-center justify-between">
                      <div>
                        <h3 className="font-extrabold text-base text-slate-900">1. Haftalık Düzenli Çalışma Şablonunuz</h3>
                        <p className="text-xs text-slate-500 mt-1">
                          Her hafta düzenli olarak hangi günlerde çalıştığınızı ve mesai saatlerinizi belirleyin.
                        </p>
                      </div>
                      <button
                        onClick={handleScheduleSave}
                        className="bg-sky-600 hover:bg-sky-700 text-white px-5 py-2.5 rounded-xl font-bold text-xs flex items-center gap-2 shadow-md transition"
                      >
                        <Save className="w-4 h-4" /> Şablonu Kaydet
                      </button>
                    </div>

                    <div className="space-y-3">
                      {DAYS_TR.map(day => {
                        const schedule = doctorScheduleList.find(s => s.dayOfWeek === day.id) || {
                          dayOfWeek: day.id,
                          startTime: '09:00',
                          endTime: '17:00',
                          slotDurationMinutes: 30,
                          isDayOff: day.id === 0
                        };

                        return (
                          <div key={day.id} className="p-4 rounded-2xl border border-slate-200 flex items-center justify-between">
                            <div className="flex items-center gap-4 w-44">
                              <input 
                                type="checkbox" 
                                id={`day-${day.id}`}
                                checked={!schedule.isDayOff}
                                onChange={(e) => {
                                  const exists = doctorScheduleList.some(s => s.dayOfWeek === day.id);
                                  let updated;
                                  if (exists) {
                                    updated = doctorScheduleList.map(s => 
                                      s.dayOfWeek === day.id ? { ...s, isDayOff: !e.target.checked } : s
                                    );
                                  } else {
                                    updated = [...doctorScheduleList, { ...schedule, isDayOff: !e.target.checked }];
                                  }
                                  setDoctorScheduleList(updated);
                                }}
                                className="w-5 h-5 text-sky-600 rounded cursor-pointer"
                              />
                              <label htmlFor={`day-${day.id}`} className="font-bold text-sm text-slate-800 cursor-pointer">
                                {day.name}
                              </label>
                            </div>

                            {!schedule.isDayOff ? (
                              <div className="flex items-center gap-6">
                                <div className="flex items-center gap-2">
                                  <span className="text-xs text-slate-500 font-semibold">Başlangıç:</span>
                                  <input 
                                    type="time" 
                                    value={schedule.startTime}
                                    onChange={(e) => {
                                      const updated = doctorScheduleList.map(s => 
                                        s.dayOfWeek === day.id ? { ...s, startTime: e.target.value } : s
                                      );
                                      setDoctorScheduleList(updated);
                                    }}
                                    className="p-2 border border-slate-200 rounded-xl text-xs font-bold"
                                  />
                                </div>

                                <div className="flex items-center gap-2">
                                  <span className="text-xs text-slate-500 font-semibold">Bitiş:</span>
                                  <input 
                                    type="time" 
                                    value={schedule.endTime}
                                    onChange={(e) => {
                                      const updated = doctorScheduleList.map(s => 
                                        s.dayOfWeek === day.id ? { ...s, endTime: e.target.value } : s
                                      );
                                      setDoctorScheduleList(updated);
                                    }}
                                    className="p-2 border border-slate-200 rounded-xl text-xs font-bold"
                                  />
                                </div>

                                <div className="flex items-center gap-2">
                                  <span className="text-xs text-slate-500 font-semibold">Randevu Aralığı:</span>
                                  <select 
                                    value={schedule.slotDurationMinutes}
                                    onChange={(e) => {
                                      const updated = doctorScheduleList.map(s => 
                                        s.dayOfWeek === day.id ? { ...s, slotDurationMinutes: parseInt(e.target.value, 10) } : s
                                      );
                                      setDoctorScheduleList(updated);
                                    }}
                                    className="p-2 border border-slate-200 rounded-xl text-xs font-bold"
                                  >
                                    <option value={15}>15 Dk</option>
                                    <option value={20}>20 Dk</option>
                                    <option value={30}>30 Dk</option>
                                    <option value={45}>45 Dk</option>
                                    <option value={60}>60 Dk</option>
                                  </select>
                                </div>
                              </div>
                            ) : (
                              <span className="text-xs font-bold text-rose-500 bg-rose-50 px-4 py-2 rounded-xl">
                                İzin Günü (Randevuya Kapalı)
                              </span>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  </div>

                  <div className="bg-white p-8 rounded-3xl border border-slate-200 shadow-sm space-y-6">
                    <div>
                      <h3 className="font-extrabold text-base text-slate-900 flex items-center gap-2">
                        <Ban className="w-5 h-5 text-rose-600" /> 2. Belirli Bir Günü veya Saat Aralığını Randevuya Kapatma
                      </h3>
                      <p className="text-xs text-slate-500 mt-1">
                        Örneğin: <i>"Perşembe saat 14:00 - 16:00 arası ameliyatım var"</i> diyerek o tarihleri anında kapatabilirsiniz.
                      </p>
                    </div>

                    <form onSubmit={handleAddTimeOff} className="p-5 bg-slate-50 rounded-2xl border border-slate-200 space-y-4">
                      <div className="grid grid-cols-4 gap-4 items-end">
                        <div>
                          <label className="block text-xs font-bold text-slate-700 mb-1">Tarih</label>
                          <input 
                            type="date" 
                            required
                            value={newTimeOff.date}
                            min={new Date().toLocaleDateString('en-CA')}
                            onChange={(e) => setNewTimeOff({...newTimeOff, date: e.target.value})}
                            className="w-full p-2.5 rounded-xl border border-slate-300 text-xs font-bold bg-white"
                          />
                        </div>

                        <div className="flex items-center gap-2 pb-2">
                          <input 
                            type="checkbox" 
                            id="isAllDay"
                            checked={newTimeOff.isAllDay}
                            onChange={(e) => setNewTimeOff({...newTimeOff, isAllDay: e.target.checked})}
                            className="w-4 h-4 text-sky-600 rounded cursor-pointer"
                          />
                          <label htmlFor="isAllDay" className="text-xs font-bold text-slate-700 cursor-pointer">
                            Tüm Gün Kapalı
                          </label>
                        </div>

                        {!newTimeOff.isAllDay && (
                          <>
                            <div>
                              <label className="block text-xs font-bold text-slate-700 mb-1">Başlangıç Saati</label>
                              <input 
                                type="time" 
                                value={newTimeOff.startTime}
                                onChange={(e) => setNewTimeOff({...newTimeOff, startTime: e.target.value})}
                                className="w-full p-2.5 rounded-xl border border-slate-300 text-xs font-bold bg-white"
                              />
                            </div>
                            <div>
                              <label className="block text-xs font-bold text-slate-700 mb-1">Bitiş Saati</label>
                              <input 
                                type="time" 
                                value={newTimeOff.endTime}
                                onChange={(e) => setNewTimeOff({...newTimeOff, endTime: e.target.value})}
                                className="w-full p-2.5 rounded-xl border border-slate-300 text-xs font-bold bg-white"
                              />
                            </div>
                          </>
                        )}
                      </div>

                      <div className="flex gap-4 items-center">
                        <input 
                          type="text" 
                          placeholder="Kapatma Sebebi (Örn: Ameliyat, Özel İzin, Kongre)"
                          value={newTimeOff.reason}
                          onChange={(e) => setNewTimeOff({...newTimeOff, reason: e.target.value})}
                          className="flex-1 p-2.5 rounded-xl border border-slate-300 text-xs bg-white"
                        />
                        <button
                          type="submit"
                          className="bg-rose-600 hover:bg-rose-700 text-white px-5 py-2.5 rounded-xl font-bold text-xs flex items-center gap-2 shadow-sm transition"
                        >
                          <PlusCircle className="w-4 h-4" /> Seçili Saati / Günü Kapat
                        </button>
                      </div>
                    </form>

                    <div className="space-y-2">
                      <p className="text-xs font-bold text-slate-700">Aktif Kapatılan Gün ve Saatleriniz:</p>
                      {timeOffList.length === 0 ? (
                        <p className="text-xs text-slate-400">Henüz kapatılmış bir gün veya saat bulunmuyor.</p>
                      ) : (
                        timeOffList.map(item => (
                          <div key={item.id} className="p-3.5 bg-rose-50/70 rounded-xl border border-rose-200 flex items-center justify-between">
                            <div className="flex items-center gap-4">
                              <span className="text-xs font-black text-rose-800 bg-rose-100 px-3 py-1 rounded-lg">
                                {item.date}
                              </span>
                              <span className="text-xs font-bold text-slate-700">
                                Saat: {item.startTime} - {item.endTime}
                              </span>
                              <span className="text-xs text-slate-500 italic">
                                ({item.reason || 'Belirtilmedi'})
                              </span>
                            </div>
                            <button
                              onClick={() => handleDeleteTimeOff(item.id)}
                              className="text-rose-600 hover:text-rose-800 p-1.5 hover:bg-rose-100 rounded-lg transition"
                              title="İzni Kaldır ve Saatleri Tekrar Aç"
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </div>
                        ))
                      )}
                    </div>
                  </div>
                </div>
              )}

              {/* TAB 4: PERSONEL & SEKRETER YÖNETİMİ */}
              {panelTab === 'staff' && (
                <div className="bg-white p-8 rounded-3xl border border-slate-200 shadow-sm space-y-6">
                  <div className="flex items-center justify-between">
                    <div>
                      <h3 className="font-extrabold text-base text-slate-900">Klinik Personeli & Doktor/Sekreter Kadrosu</h3>
                      <p className="text-xs text-slate-500 mt-1">
                        Yeni doktor veya sekreter hesabı açabilir, hekimlerin muayene ücretini anında güncelleyebilir veya personeli silebilirsiniz.
                      </p>
                    </div>

                    <button
                      onClick={() => setShowAddStaffModal(true)}
                      className="bg-sky-600 hover:bg-sky-700 text-white px-4 py-2.5 rounded-xl font-bold text-xs flex items-center gap-2 shadow-md transition"
                    >
                      <UserPlus className="w-4 h-4" /> + Yeni Doktor / Sekreter Ekle
                    </button>
                  </div>

                  <div className="divide-y divide-slate-100 border border-slate-200 rounded-2xl overflow-hidden">
                    {staffList.map((st) => (
                      <div key={st.id} className="p-5 flex items-center justify-between bg-white hover:bg-slate-50 transition">
                        <div>
                          <div className="flex items-center gap-2">
                            <p className="font-extrabold text-sm text-slate-900">{st.fullName}</p>
                            <span className="text-[10px] font-bold px-2 py-0.5 rounded-lg bg-sky-50 text-sky-700 border border-sky-200">
                              {st.role || 'Doktor'}
                            </span>
                          </div>
                          <p className="text-xs text-slate-500 mt-0.5">{st.email} • {st.specialty}</p>
                        </div>

                        <div className="flex items-center gap-3">
                          <div className="flex items-center gap-1.5">
                            <span className="text-xs font-bold text-slate-600">Ücret:</span>
                            <input 
                              type="number" 
                              defaultValue={st.consultationFee}
                              onBlur={async (e) => {
                                await updateDoctorFee(st.id, parseFloat(e.target.value));
                                alert("Muayene ücreti güncellendi!");
                                loadDoctors();
                              }}
                              className="w-24 p-2 rounded-xl border border-slate-300 text-xs font-bold text-center"
                            />
                            <span className="text-xs font-bold text-slate-600">₺</span>
                          </div>

                          <button 
                            onClick={async () => {
                              if (confirm(`${st.fullName} personelini sistemden silmek istediğinize emin misiniz?`)) {
                                await deleteDoctor(st.id);
                                loadStaff();
                                loadDoctors();
                              }
                            }}
                            className="p-2 text-rose-600 hover:bg-rose-50 rounded-xl transition"
                            title="Personeli Sil"
                          >
                            <Trash2 className="w-4 h-4" />
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* TAB 5: VİTRİN RESİMLERİ (SLIDER) */}
              {panelTab === 'banners' && (
                <div className="bg-white p-8 rounded-3xl border border-slate-200 shadow-sm space-y-6">
                  <div className="flex justify-between items-center">
                    <div>
                      <h3 className="font-extrabold text-base text-slate-900">Ana Sayfa Vitrin Resimleri (Slider)</h3>
                      <p className="text-xs text-slate-500 mt-1">Yükleyeceğiniz resimler ana sayfadaki otomatik dönen alanda gösterilir.</p>
                    </div>

                    <label className="bg-sky-600 hover:bg-sky-700 text-white px-4 py-2.5 rounded-xl font-bold text-xs flex items-center gap-2 cursor-pointer shadow-md transition">
                      <Upload className="w-4 h-4" /> Yeni Vitrin Resmi Yükle
                      <input type="file" accept="image/*" onChange={handleBannerUpload} className="hidden" />
                    </label>
                  </div>

                  <div className="grid grid-cols-3 gap-6">
                    {banners.map((b, i) => (
                      <div key={i} className="relative rounded-2xl overflow-hidden border border-slate-200 group h-48 bg-slate-100">
                        <img 
                          src={getFullImageUrl(b.imageUrl)} 
                          alt="Banner" 
                          className="w-full h-full object-cover"
                        />
                        {b.id && (
                          <button 
                            onClick={() => handleBannerDelete(b.id)}
                            className="absolute top-3 right-3 p-2 bg-rose-600 text-white rounded-xl shadow-lg opacity-0 group-hover:opacity-100 transition"
                          >
                            <Trash2 className="w-4 h-4" />
                          </button>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* TAB 6: PROFİL */}
              {panelTab === 'profile' && loggedUser && (
                <div className="bg-white p-8 rounded-3xl border border-slate-200 shadow-sm max-w-xl space-y-6">
                  <div>
                    <h3 className="font-extrabold text-base text-slate-900">Profil & Güvenlik Ayarları</h3>
                    <p className="text-xs text-slate-500 mt-1">Ad, soyad ve T.C. bilgileri NVI doğrulamalı olduğu için değiştirilemez. Eski şifrenizi girerek şifrenizi güncelleyebilirsiniz.</p>
                  </div>

                  <div className="p-4 bg-slate-50 rounded-2xl border border-slate-200 text-xs space-y-2">
                    <p><b>Ad Soyad:</b> {loggedUser.fullName} (Kilitli)</p>
                    <p><b>E-Posta:</b> {loggedUser.email}</p>
                    <p><b>Yetki:</b> {loggedUser.role}</p>
                  </div>

                  <form onSubmit={handleProfilePassChange} className="space-y-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1">Mevcut (Eski) Şifreniz *</label>
                      <input 
                        type="password" 
                        required
                        value={profilePass.oldPass}
                        onChange={(e) => setProfilePass({...profilePass, oldPass: e.target.value})}
                        className="w-full p-2.5 rounded-xl border border-slate-300 text-xs outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1">Yeni Şifre *</label>
                      <input 
                        type="password" 
                        required
                        value={profilePass.newPass}
                        onChange={(e) => setProfilePass({...profilePass, newPass: e.target.value})}
                        className="w-full p-2.5 rounded-xl border border-slate-300 text-xs outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1">Yeni Şifre Tekrar *</label>
                      <input 
                        type="password" 
                        required
                        value={profilePass.confirm}
                        onChange={(e) => setProfilePass({...profilePass, confirm: e.target.value})}
                        className="w-full p-2.5 rounded-xl border border-slate-300 text-xs outline-none"
                      />
                    </div>

                    <button
                      type="submit"
                      className="bg-sky-600 hover:bg-sky-700 text-white px-5 py-2.5 rounded-xl font-bold text-xs shadow-md transition"
                    >
                      Şifreyi Güncelle
                    </button>
                  </form>
                </div>
              )}

              {/* TAB 7: ARŞİV */}
              {panelTab === 'history' && (
                <div className="bg-white p-8 rounded-3xl border border-slate-200 shadow-sm space-y-6">
                  <h3 className="font-extrabold text-base text-slate-900">Geçmiş Muayene ve Röntgen Arşivi</h3>
                  <div className="flex gap-3 max-w-md">
                    <input 
                      type="text" 
                      placeholder="11 Haneli T.C. Giriniz" 
                      value={searchTc} 
                      onChange={(e) => setSearchTc(e.target.value)}
                      className="flex-1 p-2.5 rounded-xl border border-slate-300 text-xs font-bold outline-none"
                    />
                    <button 
                      onClick={async () => {
                        const res = await getPatientHistory(searchTc);
                        setPatientHistoryRecords(res.data);
                      }}
                      className="bg-sky-600 text-white px-4 py-2.5 rounded-xl font-bold text-xs"
                    >
                      Arşivden Getir
                    </button>
                  </div>

                  <div className="space-y-4 pt-4">
                    {patientHistoryRecords.map(rec => (
                      <div key={rec.id} className="p-5 rounded-2xl border border-slate-200 bg-slate-50 space-y-2">
                        <span className="text-xs font-bold text-sky-700 bg-sky-100 px-2.5 py-1 rounded-lg">{rec.date}</span>
                        <p className="text-sm font-bold text-slate-900">Tanı: {rec.diagnosis}</p>
                        {rec.treatmentDetails && (
                          <p className="text-xs text-emerald-800 font-semibold bg-emerald-50 px-2.5 py-1 rounded-lg border border-emerald-200 inline-block">
                            💳 Yapılan İşlemler: {rec.treatmentDetails} ({rec.paymentAmount} ₺)
                          </p>
                        )}
                        <p className="text-xs text-slate-600">Not: {rec.clinicalNotes}</p>
                        
                        {rec.attachments && rec.attachments.length > 0 && (
                          <div className="pt-2 flex flex-wrap gap-2">
                            {rec.attachments.map((att, idx) => (
                              <a 
                                key={idx} 
                                href={getFullImageUrl(att.downloadUrl)} 
                                target="_blank" 
                                rel="noreferrer" 
                                className="flex items-center gap-1.5 px-3 py-1.5 bg-white border border-slate-300 rounded-xl text-xs font-semibold text-sky-600 hover:bg-sky-50"
                              >
                                <Eye className="w-3.5 h-3.5" /> Resmi / Raporu Aç ({att.originalFileName})
                              </a>
                            ))}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}

            </div>
          </main>
        </div>
      )}

      {/* ========================================================================= */}
      {/* 3. YENİ DOKTOR / SEKRETER EKLEME MODALI                                   */}
      {/* ========================================================================= */}
      {showAddStaffModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-lg w-full shadow-2xl border border-slate-100 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center pb-4 border-b border-slate-100">
              <div className="flex items-center gap-3">
                <div className="bg-sky-600 text-white p-2 rounded-xl">
                  <UserPlus className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="font-extrabold text-base text-slate-900">Yeni Personel Ekle</h3>
                  <p className="text-xs text-slate-500">Doktor veya Sekreter Tanımlayın</p>
                </div>
              </div>
              <button onClick={() => setShowAddStaffModal(false)} className="text-slate-400 font-bold">✕</button>
            </div>

            <form onSubmit={handleCreateStaffSubmit} className="space-y-4 mt-6">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">Ad *</label>
                  <input 
                    type="text" 
                    required
                    value={newStaffForm.firstName}
                    onChange={(e) => setNewStaffForm({...newStaffForm, firstName: e.target.value})}
                    placeholder="Ad"
                    className="w-full p-2.5 rounded-xl border border-slate-300 text-xs font-bold outline-none"
                  />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">Soyad *</label>
                  <input 
                    type="text" 
                    required
                    value={newStaffForm.lastName}
                    onChange={(e) => setNewStaffForm({...newStaffForm, lastName: e.target.value})}
                    placeholder="Soyad"
                    className="w-full p-2.5 rounded-xl border border-slate-300 text-xs font-bold outline-none"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">E-Posta *</label>
                  <input 
                    type="email" 
                    required
                    value={newStaffForm.email}
                    onChange={(e) => setNewStaffForm({...newStaffForm, email: e.target.value})}
                    placeholder="doktor@eklinik.com"
                    className="w-full p-2.5 rounded-xl border border-slate-300 text-xs font-bold outline-none"
                  />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">Giriş Şifresi *</label>
                  <input 
                    type="password" 
                    required
                    value={newStaffForm.password}
                    onChange={(e) => setNewStaffForm({...newStaffForm, password: e.target.value})}
                    placeholder="En az 6 karakter"
                    className="w-full p-2.5 rounded-xl border border-slate-300 text-xs font-bold outline-none"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Personel Rolü *</label>
                <select 
                  value={newStaffForm.role}
                  onChange={(e) => setNewStaffForm({...newStaffForm, role: e.target.value})}
                  className="w-full p-2.5 rounded-xl border border-slate-300 text-xs font-bold"
                >
                  <option value="Doktor">Doktor (Randevu Alınabilir)</option>
                  <option value="Sekreter">Sekreter (Yönetim & Kasa Yetkilisi)</option>
                </select>
              </div>

              {newStaffForm.role === 'Doktor' && (
                <>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1">Unvan</label>
                      <input 
                        type="text" 
                        value={newStaffForm.title}
                        onChange={(e) => setNewStaffForm({...newStaffForm, title: e.target.value})}
                        placeholder="Op. Dr. / Prof. Dr."
                        className="w-full p-2.5 rounded-xl border border-slate-300 text-xs font-bold outline-none"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1">Muayene Ücreti (₺)</label>
                      <input 
                        type="number" 
                        value={newStaffForm.consultationFee}
                        onChange={(e) => setNewStaffForm({...newStaffForm, consultationFee: parseFloat(e.target.value)})}
                        className="w-full p-2.5 rounded-xl border border-slate-300 text-xs font-bold text-center outline-none"
                      />
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-slate-700 mb-1">Uzmanlık Alanı / Branş</label>
                    <input 
                      type="text" 
                      value={newStaffForm.specialty}
                      onChange={(e) => setNewStaffForm({...newStaffForm, specialty: e.target.value})}
                      placeholder="Göz Cerrahisi, Retina Uzmanı..."
                      className="w-full p-2.5 rounded-xl border border-slate-300 text-xs outline-none"
                    />
                  </div>
                </>
              )}

              <div className="pt-4 flex gap-3">
                <button 
                  type="button" 
                  onClick={() => setShowAddStaffModal(false)}
                  className="flex-1 py-3 rounded-xl border border-slate-300 text-xs font-bold"
                >
                  Vazgeç
                </button>
                <button 
                  type="submit" 
                  className="flex-1 py-3 rounded-xl bg-sky-600 hover:bg-sky-700 text-white text-xs font-bold shadow-md shadow-sky-200"
                >
                  Personeli Kaydet
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* 4. HASTA ESKİ GEÇMİŞİ MODALI                                              */}
      {/* ========================================================================= */}
      {activeHistoryModalPatient && (
        <div className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-2xl w-full shadow-2xl border border-slate-100 max-h-[85vh] overflow-y-auto space-y-4">
            <div className="flex justify-between items-center pb-4 border-b border-slate-100">
              <div>
                <h3 className="font-extrabold text-base text-slate-900">Hastanın Tüm Eski Muayene Geçmişi</h3>
                <p className="text-xs text-slate-500">{activeHistoryModalPatient.name} (T.C: {activeHistoryModalPatient.tc})</p>
              </div>
              <button onClick={() => setActiveHistoryModalPatient(null)} className="text-slate-400 font-bold">✕</button>
            </div>

            <div className="space-y-4">
              {patientHistoryList.length === 0 ? (
                <p className="text-xs text-slate-400 py-6 text-center">Bu hastanın daha önce kaydedilmiş başka bir muayene geçmişi bulunmamaktadır.</p>
              ) : (
                patientHistoryList.map(rec => (
                  <div key={rec.id} className="p-4 rounded-2xl bg-slate-50 border border-slate-200 space-y-2 text-xs">
                    <div className="flex justify-between items-center font-bold">
                      <span className="bg-sky-100 text-sky-800 px-2.5 py-0.5 rounded-lg">{rec.date}</span>
                      <span className="text-slate-500">{rec.doctor}</span>
                    </div>
                    <p><b className="text-slate-800">Teşhis:</b> {rec.diagnosis}</p>
                    {rec.treatmentDetails && (
                      <p><b className="text-slate-800">Uygulanan Tedavi & Ücret:</b> <span className="text-emerald-700 font-semibold">{rec.treatmentDetails} ({rec.paymentAmount} ₺)</span></p>
                    )}
                    {rec.clinicalNotes && <p><b className="text-slate-800">Not:</b> {rec.clinicalNotes}</p>}
                    {rec.prescription && (
                      <p><b className="text-slate-800">Reçete:</b> <span className="font-mono text-emerald-700 bg-white px-2 py-0.5 rounded border border-slate-200">{rec.prescription}</span></p>
                    )}
                    {rec.attachments && rec.attachments.length > 0 && (
                      <div className="flex flex-wrap gap-2 pt-1 border-t border-slate-200">
                        {rec.attachments.map((att, idx) => (
                          <a
                            key={idx}
                            href={getFullImageUrl(att.downloadUrl)}
                            target="_blank"
                            rel="noreferrer"
                            className="text-sky-600 hover:bg-sky-100 bg-white border border-slate-300 px-2 py-1 rounded-md text-[11px] font-bold flex items-center gap-1"
                          >
                            <Eye className="w-3 h-3" /> {att.originalFileName}
                          </a>
                        ))}
                      </div>
                    )}
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* 5. ÇOK KALEMLİ İŞLEM VE ÜCRET TAHSİLAT MODALI                             */}
      {/* ========================================================================= */}
      {chargingApp && (
        <div className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-lg w-full shadow-2xl border border-slate-100">
            <div className="flex justify-between items-center pb-4 border-b border-slate-100">
              <div>
                <h3 className="font-extrabold text-base text-slate-900">Klinik İşlem & Tahsilat</h3>
                <p className="text-xs text-slate-500">{chargingApp.patientName} (T.C: {chargingApp.nationalId})</p>
              </div>
              <button onClick={() => setChargingApp(null)} className="text-slate-400 hover:text-slate-600 font-bold">✕</button>
            </div>

            <form onSubmit={handleChargeSubmit} className="space-y-4 mt-6">
              <div className="space-y-3">
                <label className="block text-xs font-bold text-slate-700">Uygulanan Tedavi / İşlem Kalemleri</label>
                {treatmentItems.map((item, idx) => (
                  <div key={idx} className="flex gap-2 items-center">
                    <input 
                      type="text" 
                      required
                      placeholder="İşlem adı (Örn: Diş Çekimi, Dolgu, Lazer)"
                      value={item.procedureName}
                      onChange={(e) => {
                        const updated = [...treatmentItems];
                        updated[idx].procedureName = e.target.value;
                        setTreatmentItems(updated);
                      }}
                      className="flex-1 p-2.5 rounded-xl border border-slate-300 text-xs font-semibold outline-none"
                    />
                    <input 
                      type="number" 
                      required
                      placeholder="Tutar (₺)"
                      value={item.price}
                      onChange={(e) => {
                        const updated = [...treatmentItems];
                        updated[idx].price = e.target.value;
                        setTreatmentItems(updated);
                      }}
                      className="w-24 p-2.5 rounded-xl border border-slate-300 text-xs font-bold text-right outline-none"
                    />
                    {treatmentItems.length > 1 && (
                      <button 
                        type="button" 
                        onClick={() => handleRemoveProcedureRow(idx)}
                        className="text-rose-500 hover:text-rose-700 p-1"
                      >
                        ✕
                      </button>
                    )}
                  </div>
                ))}

                <button 
                  type="button" 
                  onClick={handleAddProcedureRow}
                  className="text-xs font-bold text-sky-600 hover:text-sky-700 flex items-center gap-1 mt-1"
                >
                  + Başka İşlem Kalemi Ekle
                </button>
              </div>

              <div className="p-4 bg-slate-50 rounded-2xl flex justify-between items-center border border-slate-200">
                <span className="text-xs font-bold text-slate-600">Ödenecek Toplam Tutar:</span>
                <span className="text-xl font-black text-emerald-600">
                  {treatmentItems.reduce((sum, item) => sum + (parseFloat(item.price) || 0), 0)} ₺
                </span>
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Ödeme Yöntemi</label>
                <select 
                  value={paymentMethod}
                  onChange={(e) => setPaymentMethod(e.target.value)}
                  className="w-full p-2.5 rounded-xl border border-slate-300 text-xs font-bold"
                >
                  <option value="Nakit">Nakit</option>
                  <option value="Kredi Kartı">Kredi Kartı</option>
                  <option value="Havale / EFT">Havale / EFT</option>
                </select>
              </div>

              <div className="pt-4 flex gap-3">
                <button 
                  type="button" 
                  onClick={() => setChargingApp(null)} 
                  className="flex-1 py-3 rounded-xl border border-slate-300 text-xs font-bold"
                >
                  Vazgeç
                </button>
                <button 
                  type="submit" 
                  className="flex-1 py-3 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold shadow-lg shadow-emerald-200"
                >
                  Tahsilatı Onayla ve Kaydet
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* 6. PERSONEL GİRİŞ MODALI                                                 */}
      {/* ========================================================================= */}
      {showLoginModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-md w-full shadow-2xl border border-slate-100">
            <div className="flex justify-between items-center pb-4 border-b border-slate-100">
              <h3 className="font-extrabold text-base text-slate-900">Personel Girişi</h3>
              <button onClick={() => setShowLoginModal(false)} className="text-slate-400 font-bold">✕</button>
            </div>

            <form onSubmit={handleLoginSubmit} className="space-y-4 mt-6">
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">E-Posta</label>
                <input 
                  type="text" 
                  required
                  placeholder="E-posta adresiniz..."
                  value={loginForm.email}
                  onChange={(e) => setLoginForm({...loginForm, email: e.target.value})}
                  className="w-full p-3 rounded-xl border border-slate-300 text-xs font-semibold outline-none"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Şifre</label>
                <input 
                  type="password" 
                  required
                  placeholder="Şifreniz..."
                  value={loginForm.password}
                  onChange={(e) => setLoginForm({...loginForm, password: e.target.value})}
                  className="w-full p-3 rounded-xl border border-slate-300 text-xs font-semibold outline-none"
                />
              </div>

              {loginError && (
                <div className="p-3 rounded-xl bg-rose-50 text-rose-700 text-xs font-semibold border border-rose-200">
                  {loginError}
                </div>
              )}

              <div className="flex justify-end">
                <button
                  type="button"
                  onClick={() => {
                    setShowLoginModal(false);
                    setShowForgotModal(true);
                    setForgotStep(1);
                  }}
                  className="text-xs font-bold text-sky-600 hover:text-sky-700"
                >
                  Şifremi Unuttum?
                </button>
              </div>

              <button
                type="submit"
                className="w-full py-3.5 rounded-xl bg-sky-600 hover:bg-sky-700 text-white text-xs font-bold shadow-lg shadow-sky-200 transition"
              >
                Giriş Yap
              </button>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* 7. ŞİFREMİ UNUTTUM MODALI                                                */}
      {/* ========================================================================= */}
      {showForgotModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-md w-full shadow-2xl border border-slate-100">
            <div className="flex justify-between items-center pb-4 border-b border-slate-100">
              <div className="flex items-center gap-3">
                <div className="bg-rose-100 text-rose-600 p-2 rounded-xl">
                  <KeyRound className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="font-extrabold text-base text-slate-900">Şifre Sıfırlama</h3>
                  <p className="text-xs text-slate-500">Güvenli Kod Doğrulaması</p>
                </div>
              </div>
              <button onClick={() => setShowForgotModal(false)} className="text-slate-400 font-bold">✕</button>
            </div>

            {forgotMsg && (
              <div className="mt-4 p-3 rounded-xl bg-rose-50 text-rose-700 text-xs font-semibold border border-rose-200">
                {forgotMsg}
              </div>
            )}

            {forgotStep === 1 ? (
              <form onSubmit={handleSendForgotCode} className="space-y-4 mt-6">
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">Kayıtlı E-Posta Adresiniz</label>
                  <input 
                    type="email" 
                    required
                    value={forgotEmail}
                    onChange={(e) => setForgotEmail(e.target.value)}
                    placeholder="ornek@eklinik.com"
                    className="w-full p-3 rounded-xl border border-slate-300 text-xs font-semibold outline-none"
                  />
                </div>
                <button
                  type="submit"
                  className="w-full py-3.5 rounded-xl bg-sky-600 hover:bg-sky-700 text-white text-xs font-bold shadow-md"
                >
                  Doğrulama Kodu Gönder
                </button>
              </form>
            ) : (
              <form onSubmit={handleResetPasswordSubmit} className="space-y-4 mt-6">
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">E-Postanıza Gelen 6 Haneli Kod</label>
                  <input 
                    type="text" 
                    maxLength={6}
                    required
                    value={resetCode}
                    onChange={(e) => setResetCode(e.target.value)}
                    placeholder="123456"
                    className="w-full p-3 rounded-xl border border-slate-300 text-center tracking-widest font-black text-base outline-none"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">Yeni Şifre</label>
                  <input 
                    type="password" 
                    required
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    className="w-full p-2.5 rounded-xl border border-slate-300 text-xs outline-none"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">Yeni Şifre Tekrar</label>
                  <input 
                    type="password" 
                    required
                    value={newPasswordConfirm}
                    onChange={(e) => setNewPasswordConfirm(e.target.value)}
                    className="w-full p-2.5 rounded-xl border border-slate-300 text-xs outline-none"
                  />
                </div>

                <button
                  type="submit"
                  className="w-full py-3.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold shadow-md"
                >
                  Şifremi Sıfırla ve Kilidi Aç
                </button>
              </form>
            )}
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* 8. MUAYENE TEŞHİS & RÖNTGEN MODALI                                       */}
      {/* ========================================================================= */}
      {activeModalAppointment && (
        <div className="fixed inset-0 z-50 bg-slate-900/50 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-xl w-full shadow-2xl border border-slate-100 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center pb-4 border-b border-slate-100">
              <div>
                <h3 className="font-extrabold text-base text-slate-900">Muayene Detayı & Teşhis Girişi</h3>
                <p className="text-xs text-slate-500">{activeModalAppointment.patientName} ({activeModalAppointment.time})</p>
              </div>
              <button onClick={() => setActiveModalAppointment(null)} className="text-slate-400 font-bold">✕</button>
            </div>

            <form onSubmit={handleMedicalSubmit} className="space-y-4 mt-6">
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Teşhis / Tanı *</label>
                <input 
                  type="text" 
                  required
                  placeholder="Örn: Miyopi, Konjonktivit"
                  value={medicalForm.diagnosis}
                  onChange={(e) => setMedicalForm({...medicalForm, diagnosis: e.target.value})}
                  className="w-full p-3 rounded-xl border border-slate-300 text-xs font-semibold outline-none"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Doktor Muayene Notu</label>
                <textarea 
                  rows={3} 
                  placeholder="Göz muayenesi bulguları, tansiyon değerleri..."
                  value={medicalForm.clinicalNotes}
                  onChange={(e) => setMedicalForm({...medicalForm, clinicalNotes: e.target.value})}
                  className="w-full p-3 rounded-xl border border-slate-300 text-xs outline-none"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Reçete / İlaçlar</label>
                <input 
                  type="text" 
                  placeholder="Örn: Göz damlası günde 3 damla..."
                  value={medicalForm.prescription}
                  onChange={(e) => setMedicalForm({...medicalForm, prescription: e.target.value})}
                  className="w-full p-3 rounded-xl border border-slate-300 text-xs outline-none"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Röntgen / Tahlil / Dosya Ekle</label>
                <input 
                  type="file" 
                  multiple 
                  accept=".jpg,.jpeg,.png,.pdf" 
                  onChange={(e) => setUploadedFiles(e.target.files)} 
                  className="w-full text-xs text-slate-500 file:mr-4 file:py-2 file:px-4 file:rounded-xl file:border-0 file:bg-sky-50 file:text-sky-700"
                />
              </div>

              <div className="pt-4 flex gap-3">
                <button 
                  type="button" 
                  onClick={() => setActiveModalAppointment(null)} 
                  className="flex-1 py-3 rounded-xl border border-slate-300 text-xs font-bold"
                >
                  Vazgeç
                </button>
                <button 
                  type="submit" 
                  className="flex-1 py-3 rounded-xl bg-sky-600 hover:bg-sky-700 text-white text-xs font-bold"
                >
                  Kaydı Tamamla
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

    </div>
  );
}