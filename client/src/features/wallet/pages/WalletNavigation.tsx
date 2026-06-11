import { useLocation, useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Button } from '../../../shared/components/ui/Button';

export function WalletNavigation() {
  const location = useLocation();
  const navigate = useNavigate();

  const navItems = [
    { label: 'Tổng quan ví', path: ROUTE_PATHS.ME.WALLET },
    { label: 'Nạp tiền', path: ROUTE_PATHS.ME.WALLET_TOPUP },
    { label: 'Lịch sử giao dịch', path: ROUTE_PATHS.ME.WALLET_TRANSACTIONS },
    { label: 'Trang chủ', path: ROUTE_PATHS.ME.ROOT }
  ];

  return (
    <nav className="wallet-nav" aria-label="Điều hướng ví">
      <div>
        <p className="wallet-nav-label">Smart Rental</p>
        <strong>Ví thanh toán</strong>
      </div>
      <div className="wallet-nav-actions">
        {navItems.map(item => {
          const isActive = location.pathname === item.path
            || (item.path === ROUTE_PATHS.ME.WALLET_TOPUP && location.pathname === ROUTE_PATHS.ME.WALLET_TOPUP_RETURN);

          return (
            <Button
              key={item.path}
              type="button"
              variant={isActive ? 'primary' : 'secondary'}
              onClick={() => navigate(item.path)}
            >
              {item.label}
            </Button>
          );
        })}
      </div>
    </nav>
  );
}
