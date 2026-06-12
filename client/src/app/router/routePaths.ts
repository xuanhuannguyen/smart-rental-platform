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
        ROOM_DETAIL: (houseId: string, roomId: string) => `/rooming-houses/${houseId}/rooms/${roomId}`,
        VIEWING_APPOINTMENTS: '/me/viewing-appointments'
    },
    ACCOUNT: {
        ROOT: '/account',
        PROFILE: '/account/profile',
        SECURITY: '/account/security',
        RENTAL_REQUESTS: '/account/rental-requests',
        CONTRACT_SETUP: (id: string) => `/account/contracts/${id}/setup`
    },
    LANDLORD: {
        REGISTER: '/landlord/register',
        DASHBOARD: '/landlord/dashboard',
        ROOMING_HOUSES: '/landlord/rooming-houses',
        ROOMING_HOUSE_DETAIL: (id: string) => `/landlord/rooming-houses/${id}`,
        VIEWING_APPOINTMENTS: '/landlord/viewing-appointments',
        RENTAL_REQUESTS: '/landlord/rental-requests'
    },
    TEST: {
        RENTAL_FLOW: '/test/rental-flow'
    },
    RENTAL_REQUESTS: {
        SUBMIT: '/rental-requests/submit'
    },
    ADMIN: {
        ROOT: '/admin',
        APPROVALS: '/admin/approvals'
    }
} as const;
