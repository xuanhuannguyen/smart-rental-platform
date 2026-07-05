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
    OCCUPANT_LOOKUP: '/api/users/occupants/lookup',
    LANDLORD_ELIGIBILITY: '/api/users/me/landlord-eligibility',
    SESSIONS: '/api/users/me/sessions',
    REVOKE_SESSION: (id: string) => `/api/users/me/sessions/${id}`
  },
  WALLET: {
    ROOT: '/api/me/wallet',
    TRANSACTIONS: '/api/me/wallet/transactions',
    TOP_UPS: '/api/me/wallet/topups',
    TOP_UP_BY_ID: (id: string) => `/api/me/wallet/topups/${id}`,
    CREATE_PAYOS_TOPUP: '/api/me/wallet/topups/payos'
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
    RENTAL_POLICY: (id: string) => `/api/rooming-houses/${id}/rental-policy`,
    SUBMIT: (id: string) => `/api/rooming-houses/${id}/submit`
  },
  BILLING: {
    SERVICE_TYPES: '/api/billing/service-types',
    SERVICE_PRICES: (roomingHouseId: string) => `/api/rooming-houses/${roomingHouseId}/service-prices`,
    ROOM_BILLING_CONTEXT: (roomId: string) => `/api/landlord/rooms/${roomId}/billing-context`,
    ROOM_INVOICE_PREVIEW: (roomId: string) => `/api/landlord/rooms/${roomId}/invoice-preview`,
    TERMINATION_INVOICE_PREVIEW: (contractId: string) => `/api/landlord/contracts/${contractId}/termination-invoice-preview`,
    CREATE_TERMINATION_INVOICE: (contractId: string) => `/api/landlord/contracts/${contractId}/termination-invoices`,
    LANDLORD_INVOICES: '/api/landlord/invoices',
      GENERATE_WITH_READINGS: '/api/landlord/invoices/generate-with-readings',
      GENERATE_BULK: '/api/landlord/invoices/generate-bulk',
    METER_READING_AI: '/api/landlord/meter-readings/ai',
    LANDLORD_INVOICE: (id: string) => `/api/landlord/invoices/${id}`,
    ISSUE_INVOICE: (id: string) => `/api/landlord/invoices/${id}/issue`,
    CANCEL_INVOICE: (id: string) => `/api/landlord/invoices/${id}/cancel`,
    MY_INVOICES: '/api/me/invoices',
    MY_INVOICE: (id: string) => `/api/me/invoices/${id}`,
    MY_CONTRACT_INVOICES: (contractId: string) => `/api/me/contracts/${contractId}/invoices`,
    PAY_INVOICE: (id: string) => `/api/me/invoices/${id}/pay`
  },
  PUBLIC: {
    ROOMING_HOUSES: '/api/public/rooming-houses',
    ROOMING_HOUSE_SEARCH: '/api/public/rooming-houses/search',
    ROOMING_HOUSE_AI_CHAT: '/api/public/rooming-houses/ai-chat',
    GUEST_ROOMING_HOUSE_RECOMMENDATIONS: '/api/public/rooming-houses/recommendations/guest',
    ROOMS: (houseId: string) => `/api/public/rooming-houses/${houseId}/rooms`,
    ROOM_BY_ID: (roomId: string) => `/api/public/rooms/${roomId}`
  },
  RENTAL_REQUESTS: {
    MY: '/api/rental-requests/my',
    INCOMING: '/api/rental-requests/incoming',
    BY_ID: (id: string) => `/api/rental-requests/${id}`,
    CREATE: (roomId: string) => `/api/rooms/${roomId}/rental-requests`,
    APPROVE: (id: string) => `/api/rental-requests/${id}/approve`,
    REJECT: (id: string) => `/api/rental-requests/${id}/reject`,
    CANCEL: (id: string) => `/api/rental-requests/${id}/cancel`
  },
  ROOM_DEPOSITS: {
    PAY: (id: string) => `/api/room-deposits/${id}/pay`
  },
  CONTRACTS: {
    MY_HISTORY: '/api/contracts/my-history',
    LANDLORD: '/api/contracts/landlord',
    BY_ID: (id: string) => `/api/contracts/${id}`,
    PREVIEW: (id: string) => `/api/contracts/${id}/preview`,
    PREVIEW_PDF: (id: string) => `/api/contracts/${id}/preview/pdf`,
    SUBMIT_OCCUPANTS: (id: string) => `/api/contracts/${id}/occupants/submit`,
    TERMS: (id: string) => `/api/contracts/${id}/terms`,
    LANDLORD_SIGN_OTP: (id: string) => `/api/contracts/${id}/landlord-sign/otp`,
    LANDLORD_SIGN: (id: string) => `/api/contracts/${id}/landlord-sign`,
    TENANT_SIGN_OTP: (id: string) => `/api/contracts/${id}/tenant-sign/otp`,
    TENANT_SIGN: (id: string) => `/api/contracts/${id}/tenant-sign`,
    FILES: (id: string) => `/api/contracts/${id}/files`,
    GENERATE_FILE: (id: string) => `/api/contracts/${id}/files/generate`,
    DOWNLOAD_FILE: (id: string, fileId: string) => `/api/contracts/${id}/files/${fileId}/download`,
    REVISION_REQUEST: (id: string) => `/api/contracts/${id}/revision-request`,
    REJECT: (id: string) => `/api/contracts/${id}/reject`,
    TERMINATE: (id: string) => `/api/contracts/${id}/terminate`,
    APPENDICES: (id: string) => `/api/contracts/${id}/appendices`,
    APPENDIX_BY_ID: (id: string, appendixId: string) => `/api/contracts/${id}/appendices/${appendixId}`,
    APPENDIX_PREVIEW: (id: string, appendixId: string) => `/api/contracts/${id}/appendices/${appendixId}/preview/pdf`,
    APPENDIX_SIGN_OTP: (id: string, appendixId: string) => `/api/contracts/${id}/appendices/${appendixId}/sign/otp`,
    APPENDIX_SIGN: (id: string, appendixId: string) => `/api/contracts/${id}/appendices/${appendixId}/sign`,
    APPENDIX_REJECT: (id: string, appendixId: string) => `/api/contracts/${id}/appendices/${appendixId}/reject`,
    APPENDIX_REVISION_REQUEST: (id: string, appendixId: string) => `/api/contracts/${id}/appendices/${appendixId}/revision-request`
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
    COMPLETE: (id: string) => `/api/landlord/viewing-appointments/${id}/complete`,
    ACCEPT_PROPOSAL: (id: string) => `/api/viewing-appointments/${id}/accept-proposal`,
    REJECT_PROPOSAL: (id: string) => `/api/viewing-appointments/${id}/reject-proposal`
  },
  NOTIFICATIONS: {
    LIST: '/api/notifications',
    UNREAD_COUNT: '/api/notifications/unread-count',
    MARK_READ: (id: string) => `/api/notifications/${id}/read`,
    MARK_ALL_READ: '/api/notifications/read-all',
    DELETE: (id: string) => `/api/notifications/${id}`,
  }
} as const;
