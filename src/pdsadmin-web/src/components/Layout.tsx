import { useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { clearAdminPassword } from '../stores/auth';

const nav = [
  { to: '/', label: 'Dashboard' },
  { to: '/accounts', label: 'Accounts' },
  { to: '/invites', label: 'Invites' },
  { to: '/subjects', label: 'Subjects' },
];

export default function Layout() {
  const navigate = useNavigate();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  function handleLogout() {
    clearAdminPassword();
    navigate('/login', { replace: true });
  }

  function closeMobileMenu() {
    setMobileMenuOpen(false);
  }

  function NavLinks({ onClick }: { onClick?: () => void }) {
    return (
      <>
        {nav.map(item => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === '/'}
            onClick={onClick}
            className={({ isActive }) =>
              `flex items-center gap-3 px-3 py-2 rounded-sm text-sm transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring ${
                isActive ? 'bg-primary text-surface' : 'text-secondary hover:bg-hover'
              }`
            }
          >
            {item.label}
          </NavLink>
        ))}
      </>
    );
  }

  return (
    <div className="min-h-screen flex bg-page text-ink">
      <aside
        className={`
          fixed inset-y-0 left-0 z-40 w-56 bg-surface border-r border-subtle
          flex flex-col transform transition-transform duration-200 ease-in-out shadow-card
          ${mobileMenuOpen ? 'translate-x-0' : '-translate-x-full'}
          md:static md:translate-x-0 md:shadow-none
        `}
      >
        <div className="px-4 py-5 text-lg font-bold border-b border-subtle">
          PDS Admin
        </div>
        <nav className="flex-1 p-2 space-y-1">
          <NavLinks onClick={closeMobileMenu} />
        </nav>
        <div className="p-3 border-t border-subtle">
          <button
            onClick={() => { handleLogout(); closeMobileMenu(); }}
            className="w-full text-left px-3 py-2 text-sm text-muted hover:text-ink rounded-sm hover:bg-hover transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
          >
            Logout
          </button>
        </div>
      </aside>

      {mobileMenuOpen && (
        <div
          className="fixed inset-0 z-30 bg-overlay md:hidden"
          onClick={closeMobileMenu}
        />
      )}

      <main className="flex-1 min-w-0 p-4 md:p-6 overflow-auto">
        <div className="flex items-center gap-3 mb-4 md:hidden">
          <button
            onClick={() => setMobileMenuOpen(true)}
            className="p-2 -ml-2 text-secondary hover:text-ink rounded-sm hover:bg-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
            aria-label="Open menu"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
            </svg>
          </button>
          <span className="text-lg font-bold">PDS Admin</span>
        </div>
        <Outlet />
      </main>
    </div>
  );
}
