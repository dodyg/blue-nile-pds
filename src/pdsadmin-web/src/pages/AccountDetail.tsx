import { useState, type ReactNode } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import DidLink from '../components/DidLink';
import Modal from '../components/Modal';
import ConfirmDialog from '../components/ConfirmDialog';
import Button from '../components/Button';
import Badge from '../components/Badge';
import { Card, CardHeader } from '../components/Card';
import PageHeader from '../components/PageHeader';
import { Table, Th, Tr, Td } from '../components/Table';
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

  if (isPending) return <div className="text-sm text-secondary">Loading...</div>;
  if (infoError) return <div className="text-sm text-danger">{infoError.message}</div>;
  if (!info) return <div className="text-sm text-secondary">Account not found</div>;

  const inviteCount = info.invites?.length ?? 0;
  const inviteUseCount = info.invites?.reduce((s, c) => s + c.uses.length, 0) ?? 0;

  return (
    <div>
      <Button
        variant="ghost"
        size="sm"
        className="mb-3 -ml-2"
        onClick={() => navigate('/accounts')}
      >
        ← Back to accounts
      </Button>

      <PageHeader
        eyebrow="accounts · passenger record"
        title={info.handle}
        description={info.did}
        actions={info.invitesDisabled ? <Badge tone="warning">invites off</Badge> : <Badge tone="success">invites on</Badge>}
      />

      <Card className="mb-6 p-5">
        <div className="grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2">
          <Field label="DID" mono>{info.did}</Field>
          <Field label="Handle">{info.handle}</Field>
          <Field label="Email">{info.email || '—'}</Field>
          <Field label="Email Confirmed">{info.emailConfirmedAt ? new Date(info.emailConfirmedAt).toLocaleString() : '—'}</Field>
          <Field label="Indexed At">{new Date(info.indexedAt).toLocaleString()}</Field>
          <Field label="Deactivated">{info.deactivatedAt ? new Date(info.deactivatedAt).toLocaleString() : 'No'}</Field>
          <Field label="Invites">{info.invitesDisabled ? 'Disabled' : 'Enabled'}</Field>
          <Field label="Takedown">{takedownRef ? <Badge tone="danger">active</Badge> : <Badge tone="success">none</Badge>}</Field>
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
      </Card>

      {inviteCount > 0 && info.invites && (
        <Card className="mb-6 overflow-x-auto">
          <CardHeader title="Account Invite Codes" subtitle={`${inviteCount} codes · ${inviteUseCount} uses`} />
          <Table>
            <thead>
              <Tr>
                <Th>Code</Th>
                <Th>Available</Th>
                <Th>Disabled</Th>
                <Th>Uses</Th>
              </Tr>
            </thead>
            <tbody>
              {info.invites.map(ic => (
                <Tr key={ic.code}>
                  <Td className="font-mono text-xs">
                    <button
                      onClick={() => setExpandedCode(expandedCode === ic.code ? null : ic.code)}
                      className="mr-1.5 text-xs text-muted hover:text-secondary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
                    >
                      {expandedCode === ic.code ? '▾' : '▸'}
                    </button>
                    {ic.code}
                  </Td>
                  <Td>{ic.available}</Td>
                  <Td>{ic.disabled ? 'Yes' : 'No'}</Td>
                  <Td>{ic.uses.length}</Td>
                </Tr>
              ))}
            </tbody>
          </Table>
        </Card>
      )}

      {repoInfo && repoInfo.collections.length > 0 && (
        <Card className="mb-6">
          <CardHeader title="Repo Collections" subtitle={`${repoInfo.collections.length} collections`} />
          <div className="flex flex-wrap gap-2 p-4">
            {repoInfo.collections.sort().map(col => (
              <button
                key={col}
                onClick={() => navigate(`/accounts/${encodeURIComponent(info.did)}/collections/${encodeURIComponent(col)}`)}
                className="rounded-sm border border-subtle bg-page px-3 py-1.5 font-mono text-xs text-ghost transition-colors hover:border-accent-ring hover:bg-accent-soft hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
              >
                {col}
              </button>
            ))}
          </div>
        </Card>
      )}

      {message && <p className="mb-4 text-sm text-success-deep dark:text-success">{message}</p>}

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {isTakenDown ? (
          <Button
            variant="secondary"
            onClick={() => setConfirm({
              title: 'Remove takedown',
              message: `Remove takedown for ${info.handle}?`,
              confirmLabel: 'Remove takedown',
              confirmClass: 'bg-neutral text-white hover:opacity-90',
              action: () => updateSubjectStatus.mutate(
                { subject: { did: info.did }, takedown: { applied: false } },
                { onSuccess: () => setMessage('Takedown removed') },
              ),
            })}
          >
            Remove takedown
          </Button>
        ) : (
          <Button
            variant="danger"
            onClick={() => setConfirm({
              title: 'Apply takedown',
              message: `Apply takedown for ${info.handle}? This hides the account from public views.`,
              confirmLabel: 'Apply takedown',
              confirmClass: 'bg-danger text-white dark:bg-danger-deep hover:opacity-90',
              action: () => updateSubjectStatus.mutate(
                { subject: { did: info.did }, takedown: { applied: true } },
                { onSuccess: () => setMessage('Takedown applied') },
              ),
            })}
          >
            Apply takedown
          </Button>
        )}
        <Button
          variant="danger"
          onClick={() => setConfirm({
            title: 'Delete account',
            message: `Permanently delete account ${info.handle} (${info.did})? This action cannot be undone.`,
            confirmLabel: 'Delete permanently',
            confirmClass: 'bg-danger text-white dark:bg-danger-deep hover:opacity-90',
            action: () => deleteAccount.mutate(info.did, { onSuccess: () => setMessage('Account deleted') }),
          })}
        >
          Delete account
        </Button>
        {info.invitesDisabled ? (
          <Button
            variant="secondary"
            onClick={() => enableInvites.mutate(info.did, { onSuccess: () => setMessage('Invites enabled') })}
          >
            Enable invites
          </Button>
        ) : (
          <Button
            variant="secondary"
            onClick={() => disableInvites.mutate(info.did, { onSuccess: () => setMessage('Invites disabled') })}
          >
            Disable invites
          </Button>
        )}
        <Button
          variant="secondary"
          onClick={() => setModal({ action: 'resetPassword', title: 'Reset password', label: 'New password', inputType: 'password' })}
        >
          Reset password
        </Button>
        <Button
          variant="secondary"
          onClick={() => setModal({ action: 'updateEmail', title: 'Update email', label: 'Email', initialValue: info.email })}
        >
          Update email
        </Button>
        <Button
          variant="secondary"
          onClick={() => setModal({ action: 'updateHandle', title: 'Update handle', label: 'Handle', initialValue: info.handle })}
        >
          Update handle
        </Button>
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
      <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">{label}</span>
      <div className={`mt-0.5 text-sm text-ink ${mono ? 'font-mono text-xs break-all' : ''}`}>{children}</div>
    </div>
  );
}
