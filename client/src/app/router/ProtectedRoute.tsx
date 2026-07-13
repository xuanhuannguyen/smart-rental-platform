import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../providers/AuthProvider';
import { LoadingState } from '../../shared/components/feedback/LoadingState';
import { ROUTE_PATHS } from './routePaths';
import { FloatingChatContainer } from '../../features/chat/components/FloatingChatContainer';

export function ProtectedRoute() {
    const { isAuthenticated, isLoading } = useAuth();
    const location = useLocation();

    if (isLoading) {
        return <LoadingState message="Đang kiểm tra đăng nhập..." />;
    }

    if (!isAuthenticated) {
        return (
            <Navigate
                to={ROUTE_PATHS.AUTH.LOGIN}
                replace
                state={{ from: location.pathname }}
            />
        );
    }

    return (
        <>
            <Outlet />
            <FloatingChatContainer />
        </>
    );
}