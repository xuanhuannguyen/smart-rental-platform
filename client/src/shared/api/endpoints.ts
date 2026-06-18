export const ENDPOINTS = {
  AUTH: {
    REGISTER: '/api/auth/register',
    VERIFY_EMAIL_OTP: '/api/auth/verify-email-otp',
    RESEND_EMAIL_OTP: '/api/auth/resend-email-otp',
    LOGIN: '/api/auth/login',
    GOOGLE_LOGIN: '/api/auth/google-login',
    FORGOT_PASSWORD: '/api/auth/forgot-password',
    RESET_PASSWORD: '/api/auth/reset-password',
    VERIFY_RESET_OTP: '/api/auth/verify-reset-otp',
    CHANGE_PASSWORD: '/api/auth/change-password',
    REFRESH_TOKEN: '/api/auth/refresh-token',
    LOGOUT: '/api/auth/logout',
    LOGOUT_ALL: '/api/auth/logout-all'
  },
  USERS: {
    ME: '/api/users/me',
    PROFILE: '/api/users/me/profile',
    LANDLORD_ELIGIBILITY: '/api/users/me/landlord-eligibility',
    SESSIONS: '/api/users/me/sessions',
    REVOKE_SESSION: (id: string) => `/api/users/me/sessions/${id}`
  },
  KYC: {
    SUBMISSIONS: '/api/kyc/submissions',
    MY_STATUS: '/api/kyc/my-status',
    MY_HISTORY: '/api/kyc/my-history'
  },
  ADMINISTRATIVE: {
    PROVINCES: '/api/administrative/provinces',
    WARDS_BY_PROVINCE: (provinceCode: string) => `/api/administrative/provinces/${provinceCode}/wards`
  },
  AMENITIES: {
    ROOT: '/api/amenities'
  },
  FILES: {
    IMAGES: '/api/files/images'
  },
  ROOMING_HOUSES: {
    ROOT: '/api/rooming-houses',
    MY_ONBOARDING: '/api/rooming-houses/my/onboarding',
    DRAFT: '/api/rooming-houses/draft',
    BY_ID: (id: string) => `/api/rooming-houses/${id}`,
    AMENITIES: (id: string) => `/api/rooming-houses/${id}/amenities`,
    IMAGES: (id: string) => `/api/rooming-houses/${id}/images`,
    LEGAL_DOCUMENT: (id: string) => `/api/rooming-houses/${id}/legal-document`,
    LEASE_POLICY: (id: string) => `/api/rooming-houses/${id}/lease-policy`,
    SUBMIT: (id: string) => `/api/rooming-houses/${id}/submit`
  },
  BILLING: {
    SERVICE_PRICES: (roomingHouseId: string) => `/api/landlord/rooming-houses/${roomingHouseId}/service-prices`,
    ROOM_BILLING_CONTEXT: (roomId: string) => `/api/landlord/rooms/${roomId}/billing-context`,
    METER_READINGS: '/api/landlord/meter-readings',
    LANDLORD_INVOICES: '/api/landlord/invoices',
    GENERATE_DRAFT: '/api/landlord/invoices/generate-draft',
    LANDLORD_INVOICE: (id: string) => `/api/landlord/invoices/${id}`,
    ISSUE_INVOICE: (id: string) => `/api/landlord/invoices/${id}/issue`,
    CANCEL_INVOICE: (id: string) => `/api/landlord/invoices/${id}/cancel`,
    MY_INVOICES: '/api/me/invoices',
    MY_INVOICE: (id: string) => `/api/me/invoices/${id}`,
    PAY_INVOICE: (id: string) => `/api/me/invoices/${id}/pay`
  },
  PUBLIC: {
    ROOMING_HOUSES: '/api/public/rooming-houses'
  },
  ADMIN: {
    KYC_PENDING: '/api/admin/kyc/pending',
    KYC_DETAIL: (id: string) => `/api/admin/kyc/${id}`,
    KYC_APPROVE: (id: string) => `/api/admin/kyc/${id}/approve`,
    KYC_REJECT: (id: string) => `/api/admin/kyc/${id}/reject`,
    KYC_HISTORY: (userId: string) => `/api/admin/kyc/history/${userId}`,
    ROOMING_HOUSES_PENDING: '/api/admin/rooming-houses/pending',
    ROOMING_HOUSES_PUBLIC: '/api/admin/rooming-houses/public',
    ROOMING_HOUSE_DETAIL: (id: string) => `/api/admin/rooming-houses/${id}`,
    ROOMING_HOUSE_APPROVE: (id: string) => `/api/admin/rooming-houses/${id}/approve`,
    ROOMING_HOUSE_REJECT: (id: string) => `/api/admin/rooming-houses/${id}/reject`,
    USERS: '/api/admin/users',
    USER_DETAIL: (id: string) => `/api/admin/users/${id}`
  },
  VIEWING_APPOINTMENTS: {
    CREATE: '/api/viewing-appointments',
    MY_APPOINTMENTS: '/api/me/viewing-appointments',
    LANDLORD_APPOINTMENTS: '/api/landlord/viewing-appointments',
    CONFLICT_CHECK: (id: string) => `/api/landlord/viewing-appointments/${id}/conflict-check`,
    CONFIRM: (id: string) => `/api/landlord/viewing-appointments/${id}/confirm`,
    REJECT: (id: string) => `/api/landlord/viewing-appointments/${id}/reject`,
    CANCEL_BY_TENANT: (id: string) => `/api/viewing-appointments/${id}/cancel`,
    CANCEL_BY_LANDLORD: (id: string) => `/api/landlord/viewing-appointments/${id}/cancel`,
    COMPLETE: (id: string) => `/api/landlord/viewing-appointments/${id}/complete`
  }
} as const;
