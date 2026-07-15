import { Alert } from '../../../shared/components/ui/Alert';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getFavoriteRoomingHouses } from '../../rooming-houses/api';
import type { RoomingHouseListingItem } from '../../rooming-houses/types';
import { toAssetUrl } from '../../../shared/api/assets';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import FavoriteButton from '../../rooming-houses/components/FavoriteButton';
import { Button } from '../../../shared/components/ui/Button';
import './FavoriteRoomingHousesPage.css';

export default function FavoriteRoomingHousesPage() {
    const navigate = useNavigate();
    const [items, setItems] = useState<RoomingHouseListingItem[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState('');
    const [page, setPage] = useState(1);
    const [hasMore, setHasMore] = useState(false);

    useEffect(() => {
        let isMounted = true;

        async function fetchFavorites() {
            try {
                setIsLoading(true);
                const result = await getFavoriteRoomingHouses(page, 10);
                if (isMounted) {
                    if (page === 1) {
                        setItems(result.items);
                    } else {
                        setItems(prev => [...prev, ...result.items]);
                    }
                    setHasMore(result.page < result.totalPages);
                    setError('');
                }
            } catch (err) {
                if (isMounted) {
                    setError(getApiErrorMessage(err));
                }
            } finally {
                if (isMounted) {
                    setIsLoading(false);
                }
            }
        }

        fetchFavorites();

        return () => {
            isMounted = false;
        };
    }, [page]);

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
            <PageHeader
                icon={
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                        <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="#2563eb" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
                        </svg>
                    </div>
                }
                eyebrow="QUẢN LÝ"
                title="Khu trọ yêu thích"
                description="Danh sách các khu trọ bạn đã yêu thích để xem sau."
            />

            <div className="favorite-houses-page">

            {error && <Alert type="error">{error}</Alert>}

            {!isLoading && items.length === 0 ? (
                <div className="favorite-houses-empty">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
                    </svg>
                    <h3>Bạn chưa yêu thích khu trọ nào</h3>
                    <p>Hãy thả tim những khu trọ bạn quan tâm để dễ dàng xem lại sau.</p>
                    <Button type="button" onClick={() => navigate('/home')}>Khám phá ngay</Button>
                </div>
            ) : (
                <div className="favorite-houses-grid">
                    {items.map(item => (
                        <div key={item.id} className="favorite-house-card" onClick={() => navigate(`/rooming-houses/${item.id}`)}>
                            <div className="favorite-house-card__image">
                                {item.coverImageUrl ? (
                                    <img src={toAssetUrl(item.coverImageUrl)} alt={item.name} />
                                ) : (
                                    <div className="favorite-house-card__placeholder">Chưa có ảnh</div>
                                )}
                                <div className="favorite-house-card__action">
                                    <FavoriteButton roomingHouseId={item.id} />
                                </div>
                            </div>
                            <div className="favorite-house-card__content">
                                <h3 className="favorite-house-card__title">{item.name}</h3>
                                <p className="favorite-house-card__address">{item.addressDisplay}</p>
                                <div className="favorite-house-card__footer">
                                    <span className="favorite-house-card__price">
                                        {item.minMonthlyRent === item.maxMonthlyRent
                                            ? `${((item.minMonthlyRent || 0) / 1000000).toFixed(1)} tr/tháng`
                                            : `${((item.minMonthlyRent || 0) / 1000000).toFixed(1)} - ${((item.maxMonthlyRent || 0) / 1000000).toFixed(1)} tr/tháng`}
                                    </span>
                                    <span className="favorite-house-card__rooms">{item.availableRooms} phòng trống</span>
                                </div>
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {hasMore && (
                <div className="favorite-houses-load-more">
                    <Button 
                        type="button"
                        variant="outline" 
                        onClick={() => setPage(p => p + 1)}
                        disabled={isLoading}
                    >
                        {isLoading ? 'Đang tải...' : 'Xem thêm'}
                    </Button>
                </div>
            )}
        </div>
        </div>
    );
}
