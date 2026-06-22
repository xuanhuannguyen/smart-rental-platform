import { createBrowserRouter, Navigate } from 'react-router-dom';
import { LoginPage } from '../../features/auth/pages/LoginPage';
import { RegisterPage } from '../../features/auth/pages/RegisterPage';
import { VerifyEmailOtpPage } from '../../features/auth/pages/VerifyEmailOtpPage';
import { ForgotPasswordPage } from '../../features/auth/pages/ForgotPasswordPage';
import { ResetPasswordPage } from '../../features/auth/pages/ResetPasswordPage';
import { MePage } from '../../features/home/pages/MePage';
import { KycSubmitPage } from '../../features/kyc/pages/KycSubmitPage';
import { KycStatusPage } from '../../features/kyc/pages/KycStatusPage';
import CreateRoomingHousePage from '../../features/rooming-houses/CreateRoomingHousePage';
import PublicRoomingHouseDetailPage from '../../features/rooming-houses/PublicRoomingHouseDetailPage';
import SearchRoomingHousesPage from '../../features/rooming-houses/SearchRoomingHousesPage';
import PublicRoomDetailPage from '../../features/rooms/pages/PublicRoomDetailPage';
import LandlordDashboardPage from '../../features/landlord/pages/LandlordDashboardPage';
import { LandlordRentalRequestsPage } from '../../features/landlord/pages/LandlordRentalRequestsPage';
import RoomingHouseDetailPage from '../../features/landlord/pages/RoomingHouseDetailPage';
import RoomDetailPage from '../../features/landlord/pages/RoomDetailPage';
import LandlordBillingPage from '../../features/billing/pages/LandlordBillingPage';
import { AccountTenantInvoicesPage } from '../../features/billing/pages/TenantInvoicesPage';
import TenantAppointmentsPage from '../../features/viewing-appointments/pages/TenantAppointmentsPage';
import LandlordAppointmentsPage from '../../features/viewing-appointments/pages/LandlordAppointmentsPage';
import NotificationsPage from '../../features/notifications/pages/NotificationsPage';
import LandlordContractsPage from '../../features/contracts/pages/LandlordContractsPage';
import LandlordContractDetailPage from '../../features/contracts/pages/LandlordContractDetailPage';
import { AdminHomePage } from '../../features/admin/pages/AdminHomePage';
import { OnboardingGuard } from './OnboardingGuard';
import { ProtectedRoute } from './ProtectedRoute';
import { RoleGuard } from './RoleGuard';
import { ROUTE_PATHS } from './routePaths';
import { AccountLayout } from '../../shared/components/layout/AccountLayout';
import { LandlordLayout } from '../../shared/components/layout/LandlordLayout';
import { ProfileInfoPage } from '../../features/profile/pages/ProfileInfoPage';
import { SecurityPage } from '../../features/profile/pages/SecurityPage';
import { TenantRentalRequestsPage } from '../../features/rental-requests/pages/TenantRentalRequestsPage';
import { TenantRentalRequestDetailPage } from '../../features/rental-requests/pages/TenantRentalRequestDetailPage';
import { TenantRentalHistoryPage } from '../../features/rental-history/pages/TenantRentalHistoryPage';
import { TenantRentalHistoryDetailPage } from '../../features/rental-history/pages/TenantRentalHistoryDetailPage';
import { LandlordRentalRequestDetailPage } from '../../features/landlord/pages/LandlordRentalRequestDetailPage';
import { MyWalletPage } from '../../features/wallet/pages/MyWalletPage';
import { TransactionHistoryPage } from '../../features/wallet/pages/TransactionHistoryPage';
import { TopUpResultPage } from '../../features/wallet/pages/TopUpResultPage';

export const router = createBrowserRouter([
    {
        path: '/',
        element: <Navigate to={ROUTE_PATHS.ME.ROOT} replace />
    },
    {
        path: ROUTE_PATHS.ME.ROOT,
        element: <MePage />
    },
    {
        path: '/search',
        element: <SearchRoomingHousesPage />
    },
    {
        path: '/rooming-houses/:id',
        element: <PublicRoomingHouseDetailPage />
    },
    {
        path: '/rooming-houses/:houseId/rooms/:roomId',
        element: <PublicRoomDetailPage />
    },
    {
        path: ROUTE_PATHS.AUTH.LOGIN,
        element: <LoginPage />
    },
    {
        path: ROUTE_PATHS.AUTH.REGISTER,
        element: <RegisterPage />
    },
    {
        path: ROUTE_PATHS.AUTH.VERIFY_EMAIL,
        element: <VerifyEmailOtpPage />
    },
    {
        path: ROUTE_PATHS.AUTH.FORGOT_PASSWORD,
        element: <ForgotPasswordPage />
    },
    {
        path: ROUTE_PATHS.AUTH.RESET_PASSWORD,
        element: <ResetPasswordPage />
    },
    {
        element: <ProtectedRoute />,
        children: [
            {
                element: <OnboardingGuard />,
                children: [
                    {
                        path: '/me',
                        element: <Navigate to={ROUTE_PATHS.ME.ROOT} replace />
                    },
                    {
                        path: ROUTE_PATHS.ACCOUNT.ROOT,
                        element: <AccountLayout />,
                        children: [
                            {
                                path: ROUTE_PATHS.ACCOUNT.ROOT,
                                element: <Navigate to={ROUTE_PATHS.ACCOUNT.PROFILE} replace />
                            },
                            {
                                path: ROUTE_PATHS.ACCOUNT.PROFILE,
                                element: <ProfileInfoPage />
                            },
                            {
                                path: ROUTE_PATHS.ACCOUNT.SECURITY,
                                element: <SecurityPage />
                            },
                            {
                                path: ROUTE_PATHS.ACCOUNT.WALLET,
                                element: <MyWalletPage />
                            },
                            {
                                path: ROUTE_PATHS.ACCOUNT.TRANSACTIONS,
                                element: <TransactionHistoryPage />
                            },
                            {
                                path: ROUTE_PATHS.ACCOUNT.TOPUP_RESULT,
                                element: <TopUpResultPage />
                            },
                            {
                                path: ROUTE_PATHS.ACCOUNT.RENTAL_REQUESTS,
                                element: <TenantRentalRequestsPage />
                            },
                            {
                                path: '/account/rental-requests/:id',
                                element: <TenantRentalRequestDetailPage />
                            },
                            {
                                path: ROUTE_PATHS.ACCOUNT.RENTAL_HISTORY,
                                element: <TenantRentalHistoryPage />
                            },
                            {
                                path: '/account/rental-history/:id',
                                element: <TenantRentalHistoryDetailPage />
                            },
                            {
                                path: ROUTE_PATHS.ACCOUNT.INVOICES,
                                element: <AccountTenantInvoicesPage />
                            },
                            {
                                path: '/account/invoices/:invoiceId',
                                element: <AccountTenantInvoicesPage />
                            },
                            {
                                path: ROUTE_PATHS.ACCOUNT.VIEWING_APPOINTMENTS,
                                element: <TenantAppointmentsPage />
                            },
                        ]
                    },
                    {
                        path: ROUTE_PATHS.ME.KYC,
                        element: <KycSubmitPage />
                    },
                    {
                        path: ROUTE_PATHS.ME.KYC_STATUS,
                        element: <KycStatusPage />
                    },
                    {
                        path: ROUTE_PATHS.ACCOUNT.NOTIFICATIONS,
                        element: <NotificationsPage />
                    },
                    {
                        path: ROUTE_PATHS.ME.VIEWING_APPOINTMENTS,
                        element: <TenantAppointmentsPage />
                    },
                    {
                        path: ROUTE_PATHS.LANDLORD.REGISTER,
                        element: <CreateRoomingHousePage />
                    },
                    {
                        element: <LandlordLayout />,
                        children: [
                            {
                                path: ROUTE_PATHS.LANDLORD.DASHBOARD,
                                element: (
                                    <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
                                        <main className="dashboard-main">
                                            <div className="empty-panel">
                                                <h2>Thống kê</h2>
                                                <p>Tính năng thống kê đang được phát triển...</p>
                                            </div>
                                        </main>
                                    </div>
                                )
                            },
                            {
                                path: ROUTE_PATHS.LANDLORD.ROOMING_HOUSES,
                                element: <LandlordDashboardPage />
                            },
                            {
                                path: '/landlord/rooming-houses/:id',
                                element: <RoomingHouseDetailPage />
                            },
                            {
                                path: '/landlord/rooming-houses/:id/rooms/:roomId',
                                element: <RoomDetailPage />
                            },
                            {
                                path: ROUTE_PATHS.LANDLORD.VIEWING_APPOINTMENTS,
                                element: <LandlordAppointmentsPage />
                            },
                            {
                                path: ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS,
                                element: <LandlordRentalRequestsPage />
                            },
                            {
                                path: '/landlord/rental-requests/:id',
                                element: <LandlordRentalRequestDetailPage />
                            },
                            {
                                path: ROUTE_PATHS.LANDLORD.INVOICES,
                                element: <LandlordBillingPage />
                            },
                            {
                                path: '/landlord/invoices/:invoiceId',
                                element: <LandlordBillingPage />
                            },
                            {
                                path: ROUTE_PATHS.LANDLORD.CONTRACTS,
                                element: <LandlordContractsPage />
                            },
                            {
                                path: '/landlord/contracts/:id',
                                element: <LandlordContractDetailPage />
                            }
                        ]
                    },


                    {
                        element: <RoleGuard allowedRoles={['Admin']} />,
                        children: [
                            {
                                path: ROUTE_PATHS.ADMIN.ROOT,
                                element: <AdminHomePage />
                            },
                            {
                                path: ROUTE_PATHS.ADMIN.APPROVALS,
                                element: <Navigate to={ROUTE_PATHS.ADMIN.ROOT} replace />
                            }
                        ]
                    }
                ]
            }
        ]
    }
]);
