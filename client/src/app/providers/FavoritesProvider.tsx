import {
    createContext,
    useCallback,
    useContext,
    useEffect,
    useMemo,
    useState,
    type ReactNode
} from 'react';
import { getFavoriteRoomingHouseIds, toggleFavoriteRoomingHouse } from '../../features/rooming-houses/api';
import { useAuth } from './AuthProvider';

interface FavoritesContextValue {
    favoriteIds: Set<string>;
    isLoading: boolean;
    toggleFavorite: (roomingHouseId: string) => Promise<void>;
    isFavorite: (roomingHouseId: string) => boolean;
}

const FavoritesContext = createContext<FavoritesContextValue | null>(null);

interface FavoritesProviderProps {
    children: ReactNode;
}

export function FavoritesProvider({ children }: FavoritesProviderProps) {
    const { isAuthenticated } = useAuth();
    const [favoriteIds, setFavoriteIds] = useState<Set<string>>(new Set());
    const [isLoading, setIsLoading] = useState(false);

    useEffect(() => {
        let isMounted = true;

        async function loadFavorites() {
            if (!isAuthenticated) {
                setFavoriteIds(new Set());
                return;
            }

            setIsLoading(true);
            try {
                const ids = await getFavoriteRoomingHouseIds();
                if (isMounted) {
                    setFavoriteIds(new Set(ids));
                }
            } catch (error) {
                console.error('Failed to load favorite rooming houses:', error);
            } finally {
                if (isMounted) {
                    setIsLoading(false);
                }
            }
        }

        loadFavorites();

        return () => {
            isMounted = false;
        };
    }, [isAuthenticated]);

    const toggleFavorite = useCallback(async (roomingHouseId: string) => {
        if (!isAuthenticated) return;

        // Optimistic update
        setFavoriteIds((prev) => {
            const next = new Set(prev);
            if (next.has(roomingHouseId)) {
                next.delete(roomingHouseId);
            } else {
                next.add(roomingHouseId);
            }
            return next;
        });

        try {
            const isFavorited = await toggleFavoriteRoomingHouse(roomingHouseId);
            
            // Sync with server state just in case it differs
            setFavoriteIds((prev) => {
                const next = new Set(prev);
                if (isFavorited) {
                    next.add(roomingHouseId);
                } else {
                    next.delete(roomingHouseId);
                }
                return next;
            });
        } catch (error) {
            console.error('Failed to toggle favorite:', error);
            // Revert optimistic update on failure
            setFavoriteIds((prev) => {
                const next = new Set(prev);
                if (next.has(roomingHouseId)) {
                    next.delete(roomingHouseId);
                } else {
                    next.add(roomingHouseId);
                }
                return next;
            });
        }
    }, [isAuthenticated]);

    const isFavorite = useCallback((roomingHouseId: string) => {
        return favoriteIds.has(roomingHouseId);
    }, [favoriteIds]);

    const value = useMemo<FavoritesContextValue>(
        () => ({
            favoriteIds,
            isLoading,
            toggleFavorite,
            isFavorite
        }),
        [favoriteIds, isLoading, toggleFavorite, isFavorite]
    );

    return <FavoritesContext.Provider value={value}>{children}</FavoritesContext.Provider>;
}

export function useFavorites() {
    const context = useContext(FavoritesContext);

    if (!context) {
        throw new Error('useFavorites must be used inside FavoritesProvider.');
    }

    return context;
}
