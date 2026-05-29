import { createBrowserRouter, Navigate } from 'react-router-dom';
import { LoginPage } from '../../features/auth/pages/LoginPage';
import { RegisterPage } from '../../features/auth/pages/RegisterPage';
import { VerifyEmailOtpPage } from '../../features/auth/pages/VerifyEmailOtpPage';
import { ForgotPasswordPage } from '../../features/auth/pages/ForgotPasswordPage';
import { ResetPasswordPage } from '../../features/auth/pages/ResetPasswordPage';
import { MePage } from '../../features/home/pages/MePage';
import { MyProfilePage } from '../../features/profile/pages/MyProfilePage';
import { KycSubmitPage } from '../../features/kyc/pages/KycSubmitPage';
import { KycStatusPage } from '../../features/kyc/pages/KycStatusPage';
import CreateRoomingHousePage from '../../features/rooming-houses/CreateRoomingHousePage';
import LandlordDashboardPage from '../../features/landlord/pages/LandlordDashboardPage';
import RoomingHouseDetailPage from '../../features/landlord/pages/RoomingHouseDetailPage';
import { AdminHomePage } from '../../features/admin/pages/AdminHomePage';
import { OnboardingGuard } from './OnboardingGuard';
import { ProtectedRoute } from './ProtectedRoute';
import { RoleGuard } from './RoleGuard';
import { ROUTE_PATHS } from './routePaths';

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
                        path: ROUTE_PATHS.ME.PROFILE,
                        element: <MyProfilePage />
                    },
                    {
                        path: ROUTE_PATHS.ME.CHANGE_PASSWORD,
                        element: <Navigate to="/me/profile?tab=security" replace />
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
