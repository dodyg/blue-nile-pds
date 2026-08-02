import { useState, type ReactNode } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import DidLink from '../components/DidLink';
import Modal from '../components/Modal';
import ConfirmDialog from '../components/ConfirmDialog';
import { useAccountInfo, useSubjectStatus, useUpdateSubjectStatus, useDeleteAccount, useEnableInvites, useDisableInvites, useUpdateAccountPassword, useUpdateAccountEmail, useUpdateAccountHandle } from '../hooks/useAccounts';
import { useDescribeRepo } from '../hooks/useRepo';

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
  const { data: repoInfo } = useDescribeRepo(did ?? '');

  const updateSubjectStatus = useUpdateSubjectStatus();
  const deleteAccount = useDeleteAccount();
  const enableInvites = useEnableInvites();
  const disableInvites = useDisableInvites();
  const updatePassword = useUpdateAccountPassword();
  const updateEmail = useUpdateAccountEmail();
  const updateHandle = useUpdateAccountHandle();

  const takedownRef = subjectStatus?.takedown?.ref ?? null;
  const isTakenDown = !!takedownRef;

  if (isPending) return <div className="text-secondary">Loading...</div>;
  if (infoError) return <div className="text-danger">{infoError.message}</div>;
  if (!info) return <div className="text-secondary">Account not found</div>;

  const inviteCount = info.invites?.length ?? 0;
  const inviteUseCount = info.invites?.reduce((s, c) => s + c.uses.length, 0) ?? 0;

  return (
    <div>
      <button
        onClick={() => navigate('/accounts')}
        className="text-primary hover:text-primary-hover text-sm mb-4 font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
      >
        ← Back to accounts
      </button>
      <h1 className="text-2xl font-bold mb-4">{info.handle}</h1>

      <div className="bg-surface border border-subtle shadow-card rounded-md p-5 space-y-3 mb-6">
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
        <div className="bg-surface border border-subtle shadow-card rounded-md p-5 mb-6">
          <h2 className="text-lg font-semibold mb-3">Account Invite Codes</h2>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-subtle text-left text-secondary">
                <th className="p-2 font-medium">Code</th>
                <th className="p-2 font-medium">Available</th>
                <th className="p-2 font-medium">Disabled</th>
                <th className="p-2 font-medium">Uses</th>
              </tr>
            </thead>
            <tbody>
              {info.invites.map(ic => (
                <tr key={ic.code} className="border-b border-subtle">
                  <td className="p-2 font-mono text-xs">
                    <button
                      onClick={() => setExpandedCode(expandedCode === ic.code ? null : ic.code)}
                      className="mr-1.5 text-muted hover:text-secondary text-xs focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
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

      {repoInfo && repoInfo.collections.length > 0 && (
        <div className="bg-surface border border-subtle shadow-card rounded-md p-5 mb-6">
          <h2 className="text-lg font-semibold mb-3">Repo Collections</h2>
          <div className="flex flex-wrap gap-2">
            {repoInfo.collections.sort().map(col => (
              <button
                key={col}
                onClick={() => navigate(`/accounts/${encodeURIComponent(info.did)}/collections/${encodeURIComponent(col)}`)}
                className="px-3 py-1.5 bg-page border border-subtle rounded-sm text-xs font-mono hover:bg-accent-soft hover:border-accent-ring transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
              >
                {col}
              </button>
            ))}
          </div>
        </div>
      )}

      {message && <p className="text-success mb-4">{message}</p>}

      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {isTakenDown ? (
          <button
            onClick={() => setConfirm({
              title: 'Remove takedown',
              message: `Remove takedown for ${info.handle}?`,
              confirmLabel: 'Remove takedown',
              confirmClass: 'bg-neutral hover:bg-ghost',
              action: () => updateSubjectStatus.mutate(
                { subject: { did: info.did }, takedown: { applied: false } },
                { onSuccess: () => setMessage('Takedown removed') },
              ),
            })}
            className="w-full px-4 py-2 bg-hover text-ghost border border-input rounded-md text-sm hover:bg-subtle focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
          >
            Remove takedown
          </button>
        ) : (
          <button
            onClick={() => setConfirm({
              title: 'Apply takedown',
              message: `Apply takedown for ${info.handle}? This hides the account from public views.`,
              confirmLabel: 'Apply takedown',
              confirmClass: 'bg-danger hover:bg-danger-hover',
              action: () => updateSubjectStatus.mutate(
                { subject: { did: info.did }, takedown: { applied: true } },
                { onSuccess: () => setMessage('Takedown applied') },
              ),
            })}
            className="w-full px-4 py-2 bg-danger text-surface rounded-md text-sm hover:bg-danger-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
          >
            Apply takedown
          </button>
        )}
        <button
          onClick={() => setConfirm({
            title: 'Delete account',
            message: `Permanently delete account ${info.handle} (${info.did})? This action cannot be undone.`,
            confirmLabel: 'Delete permanently',
            confirmClass: 'bg-danger-hover hover:bg-danger-deep',
            action: () => deleteAccount.mutate(info.did, { onSuccess: () => setMessage('Account deleted') }),
          })}
          className="w-full px-4 py-2 bg-danger-hover text-surface rounded-md text-sm hover:bg-danger-deep focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
        >
          Delete account
        </button>
        {info.invitesDisabled ? (
          <button
            onClick={() => enableInvites.mutate(info.did, { onSuccess: () => setMessage('Invites enabled') })}
            className="w-full px-4 py-2 bg-success text-surface rounded-md text-sm hover:bg-success-deep focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
          >
            Enable invites
          </button>
        ) : (
          <button
            onClick={() => disableInvites.mutate(info.did, { onSuccess: () => setMessage('Invites disabled') })}
            className="w-full px-4 py-2 bg-hover text-ghost border border-input rounded-md text-sm hover:bg-subtle focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
          >
            Disable invites
          </button>
        )}
        <button
          onClick={() => setModal({ action: 'resetPassword', title: 'Reset password', label: 'New password', inputType: 'password' })}
          className="w-full px-4 py-2 bg-warning text-surface rounded-md text-sm hover:bg-warning-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
        >
          Reset password
        </button>
        <button
          onClick={() => setModal({ action: 'updateEmail', title: 'Update email', label: 'Email', initialValue: info.email })}
          className="w-full px-4 py-2 bg-accent text-surface rounded-md text-sm hover:bg-accent-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
        >
          Update email
        </button>
        <button
          onClick={() => setModal({ action: 'updateHandle', title: 'Update handle', label: 'Handle', initialValue: info.handle })}
          className="w-full px-4 py-2 bg-accent text-surface rounded-md text-sm hover:bg-accent-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
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
      <span className="text-secondary text-sm">{label}</span>
      <div className={`mt-0.5 text-ink ${mono ? 'font-mono text-xs break-all' : ''}`}>{children}</div>
    </div>
  );
}
