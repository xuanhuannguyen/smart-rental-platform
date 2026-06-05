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
        PROFILE: '/me/profile',
        CHANGE_PASSWORD: '/me/change-password',
        KYC: '/me/kyc',
        KYC_STATUS: '/me/kyc/status',
        INVOICES: '/me/invoices'
    },
    LANDLORD: {
        REGISTER: '/landlord/register',
        DASHBOARD: '/landlord/dashboard',
        ROOMING_HOUSES: '/landlord/rooming-houses',
        ROOMING_HOUSE_DETAIL: (id: string) => `/landlord/rooming-houses/${id}`,
        BILLING: (roomingHouseId: string) => `/landlord/rooming-houses/${roomingHouseId}/billing`,
        SERVICE_PRICES: (roomingHouseId: string) => `/landlord/rooming-houses/${roomingHouseId}/service-prices`,
        METER_READINGS: '/landlord/meter-readings',
        INVOICES: '/landlord/invoices',
        INVOICE_CREATE: '/landlord/invoices/create',
        INVOICE_DETAIL: (id: string) => `/landlord/invoices/${id}`
    },
    ADMIN: {
        ROOT: '/admin',
        APPROVALS: '/admin/approvals'
    }
} as const;
