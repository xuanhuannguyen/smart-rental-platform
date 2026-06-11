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
        ROOM_DETAIL: (houseId: string, roomId: string) => `/rooming-houses/${houseId}/rooms/${roomId}`
    },
    LANDLORD: {
        REGISTER: '/landlord/register',
        DASHBOARD: '/landlord/dashboard',
        ROOMING_HOUSES: '/landlord/rooming-houses',
        ROOMING_HOUSE_DETAIL: (id: string) => `/landlord/rooming-houses/${id}`
    },
    ADMIN: {
        ROOT: '/admin',
        APPROVALS: '/admin/approvals'
    }
} as const;
