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

  function handleLogout() {
    clearAdminPassword();
    navigate('/login', { replace: true });
  }

  return (
    <div className="min-h-screen flex bg-gray-50 text-gray-900">
      <aside className="w-56 bg-white border-r border-gray-200 shadow-sm flex flex-col">
        <div className="px-4 py-5 text-lg font-bold border-b border-gray-200">
          PDS Admin
        </div>
        <nav className="flex-1 p-2 space-y-1">
          {nav.map(item => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2 rounded text-sm transition-colors ${
                  isActive ? 'bg-blue-600 text-white' : 'text-gray-600 hover:bg-gray-100'
                }`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="p-3 border-t border-gray-200">
          <button
            onClick={handleLogout}
            className="w-full text-left px-3 py-2 text-sm text-gray-500 hover:text-gray-900 rounded hover:bg-gray-100 transition-colors"
          >
            Logout
          </button>
        </div>
      </aside>
      <main className="flex-1 p-6 overflow-auto">
        <Outlet />
      </main>
    </div>
  );
}
