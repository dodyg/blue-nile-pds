import { useState, type ReactNode } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import DidLink from '../components/DidLink';
import Modal from '../components/Modal';
import ConfirmDialog from '../components/ConfirmDialog';
import { useAccountInfo, useSubjectStatus, useUpdateSubjectStatus, useDeleteAccount, useEnableInvites, useDisableInvites, useUpdateAccountPassword, useUpdateAccountEmail, useUpdateAccountHandle } from '../hooks/useAccounts';

export default function AccountDetail() {
  const { did } = useParams<{ did: string }>();
  const navigate = useNavigate();
  const [message, setMessage] = useState('');
  const [expandedCode, setExpandedCode] = useState<string | null>(null);
  const [modal, setModal] = useState<{
    action: 'updateEmail' | 'updateHandle' | 'resetPassword';
    title: string;
    label: string;
    initialValue?: string;
    inputType?: 'text' | 'password';
  } | null>(null);
  const [confirm, setConfirm] = useState<{
    title: string;
    message: string;
    confirmLabel: string;
    confirmClass: string;
    action: () => void;
  } | null>(null);

  const { data: info, isPending, error: infoError } = useAccountInfo(did ?? '');
  const { data: subjectStatus } = useSubjectStatus(did ?? '');

  const updateSubjectStatus = useUpdateSubjectStatus();
  const deleteAccount = useDeleteAccount();
  const enableInvites = useEnableInvites();
  const disableInvites = useDisableInvites();
  const updatePassword = useUpdateAccountPassword();
  const updateEmail = useUpdateAccountEmail();
  const updateHandle = useUpdateAccountHandle();

  const takedownRef = subjectStatus?.takedown?.ref ?? null;
  const isTakenDown = !!takedownRef;

  if (isPending) return <div className="text-gray-500">Loading...</div>;
  if (infoError) return <div className="text-red-600">{infoError.message}</div>;
  if (!info) return <div className="text-gray-500">Account not found</div>;

  const inviteCount = info.invites?.length ?? 0;
  const inviteUseCount = info.invites?.reduce((s, c) => s + c.uses.length, 0) ?? 0;

  return (
    <div>
      <button
        onClick={() => navigate('/accounts')}
        className="text-blue-600 hover:text-blue-700 text-sm mb-4 font-medium"
      >
        ← Back to accounts
      </button>
      <h1 className="text-2xl font-bold mb-4">{info.handle}</h1>

      <div className="bg-white border border-gray-200 shadow-sm rounded-lg p-5 space-y-3 mb-6">
        <Field label="DID" mono>{info.did}</Field>
        <Field label="Handle">{info.handle}</Field>
        <Field label="Email">{info.email || '—'}</Field>
        <Field label="Email Confirmed">{info.emailConfirmedAt ? new Date(info.emailConfirmedAt).toLocaleString() : '—'}</Field>
        <Field label="Indexed At">{new Date(info.indexedAt).toLocaleString()}</Field>
        <Field label="Deactivated">{info.deactivatedAt ? new Date(info.deactivatedAt).toLocaleString() : 'No'}</Field>
        <Field label="Invites">{info.invitesDisabled ? 'Disabled' : 'Enabled'}</Field>
        <Field label="Takedown">{takedownRef ? 'Active' : 'None'}</Field>
        <Field label="Invited By">{info.invitedBy ? <DidLink did={info.invitedBy.createdBy} /> : '—'}</Field>
        <Field label="Invite Codes">{inviteCount > 0 ? `${inviteCount} codes (${inviteUseCount} uses)` : 'None'}</Field>
        {info.inviteNote && <Field label="Invite Note">{info.inviteNote}</Field>}
        {info.threatSignatures && info.threatSignatures.length > 0 && (
          <Field label="Threat Signatures">
            {info.threatSignatures.map((ts, i) => (
              <div key={i} className="text-xs">{ts.property}: {ts.value}</div>
            ))}
          </Field>
        )}
      </div>

      {inviteCount > 0 && info.invites && (
        <div className="bg-white border border-gray-200 shadow-sm rounded-lg p-5 mb-6">
          <h2 className="text-lg font-semibold mb-3">Account Invite Codes</h2>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-200 text-left text-gray-500">
                <th className="p-2 font-medium">Code</th>
                <th className="p-2 font-medium">Available</th>
                <th className="p-2 font-medium">Disabled</th>
                <th className="p-2 font-medium">Uses</th>
              </tr>
            </thead>
            <tbody>
              {info.invites.map(ic => (
                <tr key={ic.code} className="border-b border-gray-200">
                  <td className="p-2 font-mono text-xs">
                    <button
                      onClick={() => setExpandedCode(expandedCode === ic.code ? null : ic.code)}
                      className="mr-1.5 text-gray-400 hover:text-gray-600 text-xs"
                    >
                      {expandedCode === ic.code ? '▼' : '▶'}
                    </button>
                    {ic.code}
                  </td>
                  <td className="p-2">{ic.available}</td>
                  <td className="p-2">{ic.disabled ? 'Yes' : 'No'}</td>
                  <td className="p-2">{ic.uses.length}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {message && <p className="text-green-600 mb-4">{message}</p>}

      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {isTakenDown ? (
          <button
            onClick={() => setConfirm({
              title: 'Remove takedown',
              message: `Remove takedown for ${info.handle}?`,
              confirmLabel: 'Remove takedown',
              confirmClass: 'bg-gray-600 hover:bg-gray-700',
              action: () => updateSubjectStatus.mutate(
                { subject: { did: info.did }, takedown: { applied: false } },
                { onSuccess: () => setMessage('Takedown removed') },
              ),
            })}
            className="w-full px-4 py-2 bg-gray-100 text-gray-700 border border-gray-300 rounded text-sm hover:bg-gray-200"
          >
            Remove takedown
          </button>
        ) : (
          <button
            onClick={() => setConfirm({
              title: 'Apply takedown',
              message: `Apply takedown for ${info.handle}? This hides the account from public views.`,
              confirmLabel: 'Apply takedown',
              confirmClass: 'bg-red-600 hover:bg-red-700',
              action: () => updateSubjectStatus.mutate(
                { subject: { did: info.did }, takedown: { applied: true } },
                { onSuccess: () => setMessage('Takedown applied') },
              ),
            })}
            className="w-full px-4 py-2 bg-red-600 text-white rounded text-sm hover:bg-red-700"
          >
            Apply takedown
          </button>
        )}
        <button
          onClick={() => setConfirm({
            title: 'Delete account',
            message: `Permanently delete account ${info.handle} (${info.did})? This action cannot be undone.`,
            confirmLabel: 'Delete permanently',
            confirmClass: 'bg-red-700 hover:bg-red-800',
            action: () => deleteAccount.mutate(info.did, { onSuccess: () => setMessage('Account deleted') }),
          })}
          className="w-full px-4 py-2 bg-red-700 text-white rounded text-sm hover:bg-red-800"
        >
          Delete account
        </button>
        {info.invitesDisabled ? (
          <button
            onClick={() => enableInvites.mutate(info.did, { onSuccess: () => setMessage('Invites enabled') })}
            className="w-full px-4 py-2 bg-green-600 text-white rounded text-sm hover:bg-green-700"
          >
            Enable invites
          </button>
        ) : (
          <button
            onClick={() => disableInvites.mutate(info.did, { onSuccess: () => setMessage('Invites disabled') })}
            className="w-full px-4 py-2 bg-gray-100 text-gray-700 border border-gray-300 rounded text-sm hover:bg-gray-200"
          >
            Disable invites
          </button>
        )}
        <button
          onClick={() => setModal({ action: 'resetPassword', title: 'Reset password', label: 'New password', inputType: 'password' })}
          className="w-full px-4 py-2 bg-yellow-500 text-white rounded text-sm hover:bg-yellow-600"
        >
          Reset password
        </button>
        <button
          onClick={() => setModal({ action: 'updateEmail', title: 'Update email', label: 'Email', initialValue: info.email })}
          className="w-full px-4 py-2 bg-indigo-500 text-white rounded text-sm hover:bg-indigo-600"
        >
          Update email
        </button>
        <button
          onClick={() => setModal({ action: 'updateHandle', title: 'Update handle', label: 'Handle', initialValue: info.handle })}
          className="w-full px-4 py-2 bg-indigo-500 text-white rounded text-sm hover:bg-indigo-600"
        >
          Update handle
        </button>
      </div>

      {modal && (
        <Modal
          open
          title={modal.title}
          label={modal.label}
          initialValue={modal.initialValue}
          inputType={modal.inputType}
          onConfirm={value => {
            if (modal.action === 'updateEmail') {
              updateEmail.mutate(
                { account: info.did, email: value },
                { onSuccess: () => setMessage('Email updated') },
              );
            } else if (modal.action === 'updateHandle') {
              updateHandle.mutate(
                { did: info.did, handle: value },
                { onSuccess: () => setMessage('Handle updated') },
              );
            } else {
              updatePassword.mutate(
                { did: info.did, password: value },
                { onSuccess: () => setMessage('Password reset') },
              );
            }
            setModal(null);
          }}
          onClose={() => setModal(null)}
        />
      )}
      {confirm && (
        <ConfirmDialog
          open
          title={confirm.title}
          message={confirm.message}
          confirmLabel={confirm.confirmLabel}
          confirmClass={confirm.confirmClass}
          onConfirm={() => {
            confirm.action();
            setConfirm(null);
          }}
          onCancel={() => setConfirm(null)}
        />
      )}
    </div>
  );
}

function Field({ label, mono, children }: { label: string; mono?: boolean; children: ReactNode }) {
  return (
    <div>
      <span className="text-gray-500 text-sm">{label}</span>
      <div className={`mt-0.5 text-gray-900 ${mono ? 'font-mono text-xs break-all' : ''}`}>{children}</div>
    </div>
  );
}
