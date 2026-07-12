import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { xrpcGet, xrpcPost } from '../api/client';
import type { GetAccountInfoResponse } from '../types/admin';
import Modal from '../components/Modal';

export default function AccountDetail() {
  const { did } = useParams<{ did: string }>();
  const navigate = useNavigate();
  const [info, setInfo] = useState<GetAccountInfoResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [modal, setModal] = useState<{
    action: 'updateEmail' | 'updateHandle' | 'resetPassword';
    title: string;
    label: string;
    initialValue?: string;
    inputType?: 'text' | 'password';
  } | null>(null);

  async function fetchInfo() {
    if (!did) return;
    const info = await xrpcGet<GetAccountInfoResponse>('com.atproto.admin.getAccountInfo', { did });
    setInfo(info);
  }

  useEffect(() => {
    if (!did) return;
    setLoading(true);
    xrpcGet<GetAccountInfoResponse>('com.atproto.admin.getAccountInfo', { did })
      .then(setInfo)
      .catch(e => setError(e.message))
      .finally(() => setLoading(false));
  }, [did]);

  async function doAction(action: string, body?: unknown) {
    setMessage('');
    setError('');
    try {
      const nsids: Record<string, string> = {
        takedown: 'com.atproto.admin.updateSubjectStatus',
        untakedown: 'com.atproto.admin.updateSubjectStatus',
        deleteAccount: 'com.atproto.admin.deleteAccount',
        enableInvites: 'com.atproto.admin.enableAccountInvites',
        disableInvites: 'com.atproto.admin.disableAccountInvites',
        resetPassword: 'com.atproto.admin.updateAccountPassword',
        updateEmail: 'com.atproto.admin.updateAccountEmail',
        updateHandle: 'com.atproto.admin.updateAccountHandle',
      };
      await xrpcPost(nsids[action], body);
      setMessage(`${action} successful`);
      await fetchInfo().catch(() => {});
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : `${action} failed`);
    }
  }

  if (loading) return <div className="text-gray-500">Loading...</div>;
  if (error) return <div className="text-red-600">{error}</div>;
  if (!info) return <div className="text-gray-500">Account not found</div>;

  const isTakenDown = !!info.takedownRef;

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
        <div>
          <span className="text-gray-500 text-sm">DID</span>
          <div className="font-mono text-xs mt-0.5 text-gray-900 break-all">{info.did}</div>
        </div>
        <div>
          <span className="text-gray-500 text-sm">Email</span>
          <div className="mt-0.5 text-gray-900">{info.email || '—'}</div>
        </div>
        <div>
          <span className="text-gray-500 text-sm">Invites</span>
          <div className="mt-0.5 text-gray-900">{info.invitesDisabled ? 'Disabled' : 'Enabled'}</div>
        </div>
        <div>
          <span className="text-gray-500 text-sm">Takedown</span>
          <div className="mt-0.5 text-gray-900">{isTakenDown ? 'Active' : 'None'}</div>
        </div>
      </div>

      {message && <p className="text-green-600 mb-4">{message}</p>}
      {error && <p className="text-red-600 mb-4">{error}</p>}

      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {isTakenDown ? (
          <button
            onClick={() => doAction('untakedown', { subject: { did: info.did }, takedown: { applied: false } })}
            className="w-full px-4 py-2 bg-gray-100 text-gray-700 border border-gray-300 rounded text-sm hover:bg-gray-200"
          >
            Remove takedown
          </button>
        ) : (
          <button
            onClick={() => doAction('takedown', { subject: { did: info.did }, takedown: { applied: true } })}
            className="w-full px-4 py-2 bg-red-600 text-white rounded text-sm hover:bg-red-700"
          >
            Apply takedown
          </button>
        )}
        <button
          onClick={() => doAction('deleteAccount', { did: info.did })}
          className="w-full px-4 py-2 bg-red-700 text-white rounded text-sm hover:bg-red-800"
        >
          Delete account
        </button>
        {info.invitesDisabled ? (
          <button
            onClick={() => doAction('enableInvites', { did: info.did })}
            className="w-full px-4 py-2 bg-green-600 text-white rounded text-sm hover:bg-green-700"
          >
            Enable invites
          </button>
        ) : (
          <button
            onClick={() => doAction('disableInvites', { did: info.did })}
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
            const body = modal.action === 'updateEmail'
              ? { did: info!.did, email: value }
              : modal.action === 'updateHandle'
                ? { did: info!.did, handle: value }
                : { did: info!.did, password: value };
            doAction(modal.action, body);
            setModal(null);
          }}
          onClose={() => setModal(null)}
        />
      )}
    </div>
  );
}
