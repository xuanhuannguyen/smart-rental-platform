import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../providers/AuthProvider';
import { ROUTE_PATHS } from './routePaths';

export function OnboardingGuard() {
    const { currentUser } = useAuth();
    const location = useLocation();

    if (!currentUser) {
        return null;
    }

    const isAdmin = currentUser.roles.includes('Admin');

    if (isAdmin) {
        if (!location.pathname.startsWith(ROUTE_PATHS.ADMIN.ROOT)) {
            return <Navigate to={ROUTE_PATHS.ADMIN.ROOT} replace />;
        }

        return <Outlet />;
    }

    if (!currentUser.emailConfirmed && location.pathname !== ROUTE_PATHS.AUTH.VERIFY_EMAIL) {
        return <Navigate to={ROUTE_PATHS.AUTH.VERIFY_EMAIL} replace />;
    }

    if (
        currentUser.emailConfirmed &&
        currentUser.onboardingStatus === 'KycPending' &&
        location.pathname === ROUTE_PATHS.ME.KYC
    ) {
        return <Navigate to={ROUTE_PATHS.ME.KYC_STATUS} replace />;
    }

    return <Outlet />;
}
