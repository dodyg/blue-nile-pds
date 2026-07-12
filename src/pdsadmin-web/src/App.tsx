import { BrowserRouter, Routes, Route } from 'react-router-dom';
import ProtectedRoute from './components/ProtectedRoute';
import Layout from './components/Layout';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import Accounts from './pages/Accounts';
import AccountDetail from './pages/AccountDetail';
import InviteCodes from './pages/InviteCodes';
import SubjectStatus from './pages/SubjectStatus';

export default function App() {
  return (
    <BrowserRouter basename="/admin">
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route element={<ProtectedRoute />}>
          <Route element={<Layout />}>
            <Route index element={<Dashboard />} />
            <Route path="accounts" element={<Accounts />} />
            <Route path="accounts/:did" element={<AccountDetail />} />
            <Route path="invites" element={<InviteCodes />} />
            <Route path="subjects" element={<SubjectStatus />} />
          </Route>
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
