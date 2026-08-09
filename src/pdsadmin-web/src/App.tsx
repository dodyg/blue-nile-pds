import { BrowserRouter, Routes, Route } from 'react-router-dom';
import ProtectedRoute from './components/ProtectedRoute';
import Layout from './components/Layout';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import Accounts from './pages/Accounts';
import AccountDetail from './pages/AccountDetail';
import CollectionRecords from './pages/CollectionRecords';
import RecordDetail from './pages/RecordDetail';
import InviteCodes from './pages/InviteCodes';
import CreateInviteCodes from './pages/CreateInviteCodes';
import SubjectStatus from './pages/SubjectStatus';
import Backup from './pages/Backup';
import RepoResync from './pages/RepoResync';

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
            <Route path="accounts/:did/collections/:collection" element={<CollectionRecords />} />
            <Route path="accounts/:did/collections/:collection/:rkey" element={<RecordDetail />} />
            <Route path="invites/create" element={<CreateInviteCodes />} />
            <Route path="invites" element={<InviteCodes />} />
            <Route path="subjects" element={<SubjectStatus />} />
            <Route path="backup" element={<Backup />} />
            <Route path="repo/resync" element={<RepoResync />} />
          </Route>
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
