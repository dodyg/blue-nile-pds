import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useCreateInviteCode, useCreateInviteCodes } from '../hooks/useInvites';

type Mode = 'single' | 'bulk';

export default function CreateInviteCodes() {
  const navigate = useNavigate();
  const [mode, setMode] = useState<Mode>('single');
  const [useCount, setUseCount] = useState(1);
  const [forAccount, setForAccount] = useState('');
  const [codeCount, setCodeCount] = useState(5);
  const [result, setResult] = useState<{ codes: string[]; forAccount: string } | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  const createSingle = useCreateInviteCode();
  const createBulk = useCreateInviteCodes();

  const isPending = createSingle.isPending || createBulk.isPending;
  const error = createSingle.error || createBulk.error;

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const trimmedFor = forAccount.trim();
    if (!trimmedFor) return;
    setResult(null);

    if (mode === 'single') {
      createSingle.mutate(
        { useCount, forAccount: trimmedFor },
        {
          onSuccess: (data) => setResult({ codes: [data.code], forAccount: trimmedFor }),
        },
      );
    } else {
      createBulk.mutate(
        { useCount, codeCount, forAccounts: [trimmedFor] },
        {
          onSuccess: (data) => {
            const allCodes = data.codes.flatMap(c => c.codes);
            setResult({ codes: allCodes, forAccount: trimmedFor });
          },
        },
      );
    }
  }

  async function copyCode(code: string) {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(code);
      setTimeout(() => setCopied(null), 2000);
    } catch {
      // clipboards may not be available
    }
  }

  function handleCreateAnother() {
    setResult(null);
  }

  return (
    <div>
      <button
        onClick={() => navigate('/invites')}
        className="text-blue-600 hover:text-blue-700 text-sm mb-4 font-medium"
      >
        ← Back to invite codes
      </button>

      <h1 className="text-2xl font-bold mb-5">Create Invite Codes</h1>

      {error && <p className="text-red-600 mb-4">{error.message}</p>}

      {result ? (
        <div className="bg-white border border-gray-200 shadow-sm rounded-lg p-5">
          <p className="text-green-700 font-semibold mb-4">
            {result.codes.length === 1
              ? 'Invite code created successfully'
              : `${result.codes.length} invite codes created`}
          </p>
          <div className="space-y-2 mb-5">
            {result.codes.map((code, i) => (
              <div key={i} className="flex items-center gap-2 font-mono text-sm bg-gray-50 border border-gray-200 rounded px-3 py-2">
                <span className="flex-1 break-all">{code}</span>
                <button
                  onClick={() => copyCode(code)}
                  className="text-xs px-2 py-1 rounded bg-white border border-gray-300 hover:bg-gray-100 shrink-0"
                >
                  {copied === code ? 'Copied!' : 'Copy'}
                </button>
              </div>
            ))}
          </div>
          <div className="flex gap-3">
            <button
              onClick={handleCreateAnother}
              className="px-4 py-2 bg-blue-600 text-white rounded text-sm hover:bg-blue-700"
            >
              Create another batch
            </button>
            <button
              onClick={() => navigate('/invites')}
              className="px-4 py-2 bg-white text-gray-700 border border-gray-300 rounded text-sm hover:bg-gray-50"
            >
              View all invite codes
            </button>
          </div>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="bg-white border border-gray-200 shadow-sm rounded-lg p-5 space-y-5 max-w-lg">
          <div className="flex gap-1 bg-gray-100 rounded-lg p-1 w-fit">
            <button
              type="button"
              onClick={() => setMode('single')}
              className={`px-4 py-1.5 rounded text-sm font-medium transition-colors ${
                mode === 'single' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              Single code
            </button>
            <button
              type="button"
              onClick={() => setMode('bulk')}
              className={`px-4 py-1.5 rounded text-sm font-medium transition-colors ${
                mode === 'bulk' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              Bulk codes
            </button>
          </div>

          <div>
            <label className="block text-sm text-gray-500 mb-1">Uses per code</label>
            <input
              type="number"
              min={1}
              value={useCount}
              onChange={e => setUseCount(Math.max(1, parseInt(e.target.value) || 1))}
              className="w-full px-3 py-2 rounded bg-white border border-gray-300 text-gray-900 focus:border-blue-500 focus:outline-none"
            />
          </div>

          {mode === 'bulk' && (
            <div>
              <label className="block text-sm text-gray-500 mb-1">Number of codes to generate</label>
              <input
                type="number"
                min={1}
                value={codeCount}
                onChange={e => setCodeCount(Math.max(1, parseInt(e.target.value) || 1))}
                className="w-full px-3 py-2 rounded bg-white border border-gray-300 text-gray-900 focus:border-blue-500 focus:outline-none"
              />
            </div>
          )}

          <div>
            <label className="block text-sm text-gray-500 mb-1">
              For account <span className="text-red-400">*</span>
            </label>
            <input
              type="text"
              placeholder="did:plc:..."
              value={forAccount}
              onChange={e => setForAccount(e.target.value)}
              className="w-full px-3 py-2 rounded bg-white border border-gray-300 text-gray-900 focus:border-blue-500 focus:outline-none"
            />
          </div>

          <button
            type="submit"
            disabled={isPending}
            className="w-full px-4 py-2 bg-blue-600 text-white rounded text-sm font-medium hover:bg-blue-700 disabled:opacity-50"
          >
            {isPending ? 'Generating...' : mode === 'single' ? 'Generate code' : `Generate ${codeCount} codes`}
          </button>
        </form>
      )}
    </div>
  );
}
