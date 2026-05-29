import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../providers/AuthProvider';
import { ROUTE_PATHS } from './routePaths';

interface RoleGuardProps {
  allowedRoles: string[];
  fallbackPath?: string;
}

export function RoleGuard({ allowedRoles, fallbackPath = ROUTE_PATHS.ME.ROOT }: RoleGuardProps) {
  const { currentUser } = useAuth();

  const hasAllowedRole = currentUser?.roles.some((role) => allowedRoles.includes(role)) ?? false;

  if (!hasAllowedRole) {
    return <Navigate to={fallbackPath} replace />;
  }

  return <Outlet />;
}
