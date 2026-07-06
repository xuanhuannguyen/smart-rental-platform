export const ROUTE_PATHS = {
    AUTH: {
        LOGIN: '/login',
        REGISTER: '/register',
        VERIFY_EMAIL: '/verify-email',
        FORGOT_PASSWORD: '/forgot-password',
        RESET_PASSWORD: '/reset-password'
    },
    ME: {
        ROOT: '/home',
        KYC: '/me/kyc',
        KYC_STATUS: '/me/kyc/status',
        ROOM_DETAIL: (houseId: string, roomId: string) => `/rooming-houses/${houseId}/rooms/${roomId}`
    },
    ACCOUNT: {
        ROOT: '/account',
        PROFILE: '/account/profile',
        SECURITY: '/account/security',
        WALLET: '/account/wallet',
        TRANSACTIONS: '/account/transactions',
        TOPUP_RESULT: '/account/wallet/topup-result',
        VIEWING_APPOINTMENTS: '/account/viewing-appointments',
        NOTIFICATIONS: '/notifications',
        MESSAGES: '/account/messages',
        RENTAL_REQUESTS: '/account/rental-requests',
        RENTAL_REQUEST_DETAIL: (id: string) => `/account/rental-requests/${id}`,
        RENTAL_HISTORY: '/account/rental-history',
        RENTAL_HISTORY_DETAIL: (id: string) => `/account/rental-history/${id}`,
        INVOICES: '/account/invoices',
        INVOICE_DETAIL: (id: string) => `/account/invoices/${id}`
    },
    LANDLORD: {
        REGISTER: '/landlord/register',
        DASHBOARD: '/landlord/dashboard',
        ROOMING_HOUSES: '/landlord/rooming-houses',
        ROOMING_HOUSE_DETAIL: (id: string) => `/landlord/rooming-houses/${id}`,
        ROOM_DETAIL: (houseId: string, roomId: string) => `/landlord/rooming-houses/${houseId}/rooms/${roomId}`,
        ROOM_CREATE: (houseId: string) => `/landlord/rooming-houses/${houseId}/rooms/create`,
        VIEWING_APPOINTMENTS: '/landlord/viewing-appointments',
        MESSAGES: '/landlord/messages',
        RENTAL_REQUESTS: '/landlord/rental-requests',
        RENTAL_REQUEST_DETAIL: (id: string) => `/landlord/rental-requests/${id}`,
        SERVICE_PRICES: (houseId: string) => `/landlord/rooming-houses/${houseId}/service-prices`,
        INVOICES: '/landlord/invoices',
        INVOICE_DETAIL: (id: string) => `/landlord/invoices/${id}`,
        CONTRACTS: '/landlord/contracts',
        CONTRACT_DETAIL: (id: string) => `/landlord/contracts/${id}`
    },
    ADMIN: {
        ROOT: '/admin',
        APPROVALS: '/admin/approvals'
    }
} as const;
