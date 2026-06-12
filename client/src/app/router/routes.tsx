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
import LandlordDashboardPage from '../../features/landlord/pages/LandlordDashboardPage';
import { LandlordRentalRequestsPage } from '../../features/landlord/pages/LandlordRentalRequestsPage';
import RoomingHouseDetailPage from '../../features/landlord/pages/RoomingHouseDetailPage';
import { RentalFlowTestPage } from '../../features/rental/pages/RentalFlowTestPage';
import { AdminHomePage } from '../../features/admin/pages/AdminHomePage';
import { OnboardingGuard } from './OnboardingGuard';
import { ProtectedRoute } from './ProtectedRoute';
import { RoleGuard } from './RoleGuard';
import { ROUTE_PATHS } from './routePaths';
import { AccountLayout } from '../../shared/components/layout/AccountLayout';
import { ProfileInfoPage } from '../../features/profile/pages/ProfileInfoPage';
import { SecurityPage } from '../../features/profile/pages/SecurityPage';
import { SubmitRentalRequestPage } from '../../features/rental-requests/pages/SubmitRentalRequestPage';
import { TenantRentalRequestsPage } from '../../features/rental-requests/pages/TenantRentalRequestsPage';
import { ContractOccupantsSetupPage } from '../../features/rental-contracts/pages/ContractOccupantsSetupPage';

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
                                path: ROUTE_PATHS.ACCOUNT.RENTAL_REQUESTS,
                                element: <TenantRentalRequestsPage />
                            },
                            {
                                path: ROUTE_PATHS.ACCOUNT.CONTRACT_SETUP(':id'),
                                element: <ContractOccupantsSetupPage />
                            }
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
                        path: ROUTE_PATHS.LANDLORD.REGISTER,
                        element: <CreateRoomingHousePage />
                    },
                    {
                        path: ROUTE_PATHS.LANDLORD.DASHBOARD,
                        element: <LandlordDashboardPage />
                    },
                    {
                        path: ROUTE_PATHS.LANDLORD.ROOMING_HOUSES,
                        element: <Navigate to={ROUTE_PATHS.LANDLORD.DASHBOARD} replace />
                    },
                    {
                        path: '/landlord/rooming-houses/:id',
                        element: <RoomingHouseDetailPage />
                    },
                    {
                        path: ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS,
                        element: <LandlordRentalRequestsPage />
                    },
                    {
                        path: ROUTE_PATHS.TEST.RENTAL_FLOW,
                        element: <RentalFlowTestPage />
                    },
                    {
                        path: ROUTE_PATHS.RENTAL_REQUESTS.SUBMIT,
                        element: <SubmitRentalRequestPage />
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
