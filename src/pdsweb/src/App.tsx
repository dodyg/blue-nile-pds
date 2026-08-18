import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import UserLayout from './components/UserLayout';
import AdminLayout from './components/AdminLayout';
import ProtectedRoute from './components/ProtectedRoute';
import Home from './pages/Home';
import Register from './pages/Register';
import SignIn from './pages/SignIn';
import ForgotPassword from './pages/ForgotPassword';
import Profile from './pages/Profile';
import ProfileView from './pages/ProfileView';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import Accounts from './pages/Accounts';
import AccountDetail from './pages/AccountDetail';
import CollectionRecords from './pages/CollectionRecords';
import RecordDetail from './pages/RecordDetail';
import InviteCodes from './pages/InviteCodes';
import CreateInviteCodes from './pages/CreateInviteCodes';
import SubjectStatus from './pages/SubjectStatus';
import Approvals from './pages/Approvals';
import ApprovalDetail from './pages/ApprovalDetail';
import Backup from './pages/Backup';
import RepoResync from './pages/RepoResync';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<UserLayout />}>
          <Route index element={<Home />} />
          <Route path="register" element={<Register />} />
          <Route path="profile/login" element={<SignIn />} />
          <Route path="profile/forgot-password" element={<ForgotPassword />} />
          <Route path="profile" element={<Profile />} />
          <Route path="profile/:did" element={<ProfileView />} />
        </Route>

        <Route path="/admin/login" element={<Login />} />
        <Route path="/admin" element={<ProtectedRoute />}>
          <Route element={<AdminLayout />}>
            <Route index element={<Dashboard />} />
            <Route path="accounts" element={<Accounts />} />
            <Route path="accounts/:did" element={<AccountDetail />} />
            <Route path="accounts/:did/collections/:collection" element={<CollectionRecords />} />
            <Route path="accounts/:did/collections/:collection/:rkey" element={<RecordDetail />} />
            <Route path="invites/create" element={<CreateInviteCodes />} />
            <Route path="invites" element={<InviteCodes />} />
            <Route path="subjects" element={<SubjectStatus />} />
            <Route path="approvals" element={<Approvals />} />
            <Route path="approvals/:did" element={<ApprovalDetail />} />
            <Route path="backup" element={<Backup />} />
            <Route path="repo/resync" element={<RepoResync />} />
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}