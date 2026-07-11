import { MouseEvent } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { useFavorites } from '../../../app/providers/FavoritesProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import './FavoriteButton.css';

interface FavoriteButtonProps {
    roomingHouseId: string;
    className?: string;
}

export default function FavoriteButton({ roomingHouseId, className = '' }: FavoriteButtonProps) {
    const { isAuthenticated } = useAuth();
    const { isFavorite, toggleFavorite, isLoading } = useFavorites();
    const navigate = useNavigate();
    const location = useLocation();

    const favorited = isFavorite(roomingHouseId);

    const handleClick = async (e: MouseEvent<HTMLButtonElement>) => {
        e.preventDefault();
        e.stopPropagation();

        if (!isAuthenticated) {
            // Redirect to login, but keep track of where to return
            navigate(`${ROUTE_PATHS.AUTH.LOGIN}?returnUrl=${encodeURIComponent(location.pathname + location.search)}`);
            return;
        }

        await toggleFavorite(roomingHouseId);
    };

    return (
        <button
            className={`favorite-button ${favorited ? 'favorite-button--active' : ''} ${className}`}
            onClick={handleClick}
            disabled={isLoading && !favorited && !isAuthenticated}
            aria-label={favorited ? "Gỡ khỏi danh sách yêu thích" : "Thêm vào danh sách yêu thích"}
            title={favorited ? "Đã lưu" : "Lưu khu trọ"}
        >
            <svg
                xmlns="http://www.w3.org/2000/svg"
                viewBox="0 0 24 24"
                fill={favorited ? "currentColor" : "none"}
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
                className="favorite-button__icon"
            >
                <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
            </svg>
        </button>
    );
}
