import { Link, NavLink, Outlet } from 'react-router-dom';
import { useLogout } from '../hooks/useAccount';
import { useIsSignedIn } from '../stores/userAuth';

const isActive = ({ isActive }: { isActive: boolean }) =>
  `rounded-sm px-2 py-1 text-sm transition-colors ${isActive ? 'text-ink font-semibold' : 'text-secondary hover:text-ink'}`;

const navLink =
  'rounded-sm px-2 py-1 text-sm text-secondary transition-colors hover:text-ink';

export default function Layout() {
  const isSignedIn = useIsSignedIn();
  const logout = useLogout();

  return (
    <div className="min-h-screen bg-page text-ink">
      <header className="border-b border-subtle bg-surface">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <Link to="/" className="font-display text-sm font-bold tracking-[0.14em] text-ink uppercase">
            Blue Nile <span className="text-board-text">PDS</span>
          </Link>
          <nav className="flex items-center gap-1">
            <NavLink to="/" end className={isActive}>
              Directory
            </NavLink>
            {!isSignedIn && (
              <NavLink to="/register" className={isActive}>
                Register
              </NavLink>
            )}
            {isSignedIn ? (
              <>
                <NavLink to="/profile" className={isActive}>
                  Profile
                </NavLink>
                <button type="button" onClick={logout} className={navLink}>
                  Sign out
                </button>
              </>
            ) : (
              <NavLink to="/profile/login" className={isActive}>
                Sign in
              </NavLink>
            )}
          </nav>
        </div>
      </header>
      <main className="mx-auto max-w-5xl px-4 py-8">
        <Outlet />
      </main>
      <footer className="border-t border-subtle">
        <div className="mx-auto max-w-5xl px-4 py-4 text-center text-xs text-muted">
          Blue Nile PDS · built with atompds
        </div>
      </footer>
    </div>
  );
}