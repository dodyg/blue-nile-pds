import { useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { clearAdminPassword } from '../stores/auth';
import { useTheme } from '../hooks/useTheme';

const nav = [
  { to: '/admin', label: 'Dashboard' },
  { to: '/admin/accounts', label: 'Accounts' },
  { to: '/admin/invites', label: 'Invites' },
  { to: '/admin/subjects', label: 'Subjects' },
  { to: '/admin/approvals', label: 'Approvals' },
  { to: '/admin/backup', label: 'Backup' },
  { to: '/admin/repo/resync', label: 'Repo Resync' },
];

function SunIcon() {
  return (
    <svg className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <circle cx="12" cy="12" r="4" />
      <path strokeLinecap="round" d="M12 2v2m0 16v2M4.93 4.93l1.41 1.41m11.32 11.32 1.41 1.41M2 12h2m16 0h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
    </svg>
  );
}

function MoonIcon() {
  return (
    <svg className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
    </svg>
  );
}

export default function Layout() {
  const navigate = useNavigate();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const { theme, toggle } = useTheme();

  function handleLogout() {
    clearAdminPassword();
    navigate('/admin/login', { replace: true });
  }

  function closeMobileMenu() {
    setMobileMenuOpen(false);
  }

  function BrandChip() {
    return (
      <div className="rounded-sm border border-black/15 bg-board px-3 py-2 shadow-chip">
        <p className="font-display text-sm font-bold tracking-[0.18em] text-board-text uppercase">PDS Admin</p>
        <p className="mt-0.5 font-mono text-[9px] tracking-[0.14em] text-board-text-dim uppercase">atproto · pds</p>
      </div>
    );
  }

  function ThemeToggle({ onSwitch }: { onSwitch?: () => void }) {
    return (
      <button
        onClick={() => {
          toggle();
          onSwitch?.();
        }}
        className="inline-flex items-center gap-2 rounded-sm px-2 py-1.5 text-xs font-medium text-secondary transition-colors hover:bg-hover hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
        aria-label={`Switch to ${theme === 'dark' ? 'light' : 'dark'} theme`}
      >
        {theme === 'dark' ? <SunIcon /> : <MoonIcon />}
        <span className="font-mono text-[10px] uppercase tracking-[0.14em]">{theme}</span>
      </button>
    );
  }

  function NavLinks({ onClick }: { onClick?: () => void }) {
    return (
      <>
        {nav.map((item, i) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === '/admin'}
            onClick={onClick}
            className={({ isActive }) =>
              `flex items-center gap-2.5 rounded-sm px-3 py-2 text-sm transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring ${
                isActive ? 'bg-board text-board-text shadow-chip' : 'text-secondary hover:bg-hover hover:text-ink'
              }`
            }
          >
            <span className="font-display text-[10px] tabular-nums">{String(i + 1).padStart(2, '0')}</span>
            {item.label}
          </NavLink>
        ))}
      </>
    );
  }

  return (
    <div className="flex min-h-screen bg-page text-ink">
      <aside
        className={`
          fixed inset-y-0 left-0 z-40 flex w-60 flex-col border-r border-subtle bg-surface shadow-card
          transform transition-transform duration-200 ease-in-out
          ${mobileMenuOpen ? 'translate-x-0' : '-translate-x-full'}
          md:static md:translate-x-0 md:shadow-none
        `}
      >
        <div className="border-b border-subtle px-4 py-5">
          <BrandChip />
        </div>
        <nav className="flex-1 space-y-1 p-2">
          <p className="px-3 pb-1 pt-2 font-display text-[9px] font-semibold uppercase tracking-[0.22em] text-muted">Boards</p>
          <NavLinks onClick={closeMobileMenu} />
        </nav>
        <div className="flex items-center justify-between border-t border-subtle p-3">
          <ThemeToggle onSwitch={closeMobileMenu} />
          <button
            onClick={() => {
              handleLogout();
              closeMobileMenu();
            }}
            className="rounded-sm px-2 py-1.5 text-xs font-medium text-muted transition-colors hover:bg-hover hover:text-danger focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
          >
            Logout
          </button>
        </div>
      </aside>

      {mobileMenuOpen && (
        <div className="fixed inset-0 z-30 bg-overlay md:hidden" onClick={closeMobileMenu} />
      )}

      <main className="min-w-0 flex-1 overflow-auto p-4 md:p-6">
        <div className="mb-4 flex items-center gap-3 md:hidden">
          <button
            onClick={() => setMobileMenuOpen(true)}
            className="-ml-2 rounded-sm p-2 text-secondary transition-colors hover:bg-hover hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
            aria-label="Open menu"
          >
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
            </svg>
          </button>
          <BrandChip />
          <div className="ml-auto">
            <ThemeToggle />
          </div>
        </div>
        <Outlet />
      </main>
    </div>
  );
}
