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
              `flex items-center gap-3 px-3 py-2 rounded text-sm transition-colors ${
                isActive ? 'bg-blue-600 text-white' : 'text-gray-600 hover:bg-gray-100'
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
    <div className="min-h-screen flex bg-gray-50 text-gray-900">
      <aside
        className={`
          fixed inset-y-0 left-0 z-40 w-56 bg-white border-r border-gray-200
          flex flex-col transform transition-transform duration-200 ease-in-out shadow-sm
          ${mobileMenuOpen ? 'translate-x-0' : '-translate-x-full'}
          md:static md:translate-x-0 md:shadow-none
        `}
      >
        <div className="px-4 py-5 text-lg font-bold border-b border-gray-200">
          PDS Admin
        </div>
        <nav className="flex-1 p-2 space-y-1">
          <NavLinks onClick={closeMobileMenu} />
        </nav>
        <div className="p-3 border-t border-gray-200">
          <button
            onClick={() => { handleLogout(); closeMobileMenu(); }}
            className="w-full text-left px-3 py-2 text-sm text-gray-500 hover:text-gray-900 rounded hover:bg-gray-100 transition-colors"
          >
            Logout
          </button>
        </div>
      </aside>

      {mobileMenuOpen && (
        <div
          className="fixed inset-0 z-30 bg-black/40 md:hidden"
          onClick={closeMobileMenu}
        />
      )}

      <main className="flex-1 min-w-0 p-4 md:p-6 overflow-auto">
        <div className="flex items-center gap-3 mb-4 md:hidden">
          <button
            onClick={() => setMobileMenuOpen(true)}
            className="p-2 -ml-2 text-gray-600 hover:text-gray-900 rounded hover:bg-gray-100"
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
