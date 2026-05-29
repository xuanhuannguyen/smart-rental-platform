import {
    createContext,
    useCallback,
    useContext,
    useEffect,
    useMemo,
    useState,
    type ReactNode
} from 'react';
import { authApi } from '../../features/auth/services/authApi';
import type { AuthUser } from '../../features/auth/types/auth.types';
import { tokenStorage } from '../../shared/api/tokenStorage';

interface AuthContextValue {
    currentUser: AuthUser | null;
    isAuthenticated: boolean;
    isLoading: boolean;
    refreshMe: () => Promise<AuthUser | null>;
    setSession: (accessToken: string, refreshToken: string) => Promise<AuthUser | null>;
    clearSession: () => void;
    logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

interface AuthProviderProps {
    children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
    const [currentUser, setCurrentUser] = useState<AuthUser | null>(null);
    const [isLoading, setIsLoading] = useState(true);

    const clearSession = useCallback(() => {
        tokenStorage.clear();
        setCurrentUser(null);
    }, []);

    const refreshMe = useCallback(async () => {
        const accessToken = tokenStorage.getAccessToken();

        if (!accessToken) {
            setCurrentUser(null);
            return null;
        }

        try {
            const response = await authApi.getMe();
            setCurrentUser(response.data);
            return response.data;
        } catch {
            clearSession();
            return null;
        }
    }, [clearSession]);

    const setSession = useCallback(
        async (accessToken: string, refreshToken: string) => {
            tokenStorage.setTokens(accessToken, refreshToken);
            return refreshMe();
        },
        [refreshMe]
    );

    const logout = useCallback(async () => {
        const refreshToken = tokenStorage.getRefreshToken();

        try {
            if (refreshToken) {
                await authApi.logout({ refreshToken });
            }
        } finally {
            clearSession();
        }
    }, [clearSession]);

    useEffect(() => {
        let isMounted = true;

        async function bootstrapAuth() {
            setIsLoading(true);

            await refreshMe();

            if (isMounted) {
                setIsLoading(false);
            }
        }

        bootstrapAuth();

        return () => {
            isMounted = false;
        };
    }, [refreshMe]);

    const value = useMemo<AuthContextValue>(
        () => ({
            currentUser,
            isAuthenticated: Boolean(currentUser),
            isLoading,
            refreshMe,
            setSession,
            clearSession,
            logout
        }),
        [currentUser, isLoading, refreshMe, setSession, clearSession, logout]
    );

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
    const context = useContext(AuthContext);

    if (!context) {
        throw new Error('useAuth must be used inside AuthProvider.');
    }

    return context;
}