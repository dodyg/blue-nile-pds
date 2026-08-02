import { useState, Fragment } from 'react';
import { useNavigate } from 'react-router-dom';
import DidLink from '../components/DidLink';
import { useInviteCodes, useDisableInviteCode } from '../hooks/useInvites';
import PageHeader from '../components/PageHeader';
import Button from '../components/Button';
import { TableBoard, Table, Th, Tr, Td } from '../components/Table';
import EmptyState from '../components/EmptyState';

export default function InviteCodes() {
  const navigate = useNavigate();
  const [expandedCode, setExpandedCode] = useState<string | null>(null);
  const [message, setMessage] = useState('');

  const { data, isPending, error, fetchNextPage, hasNextPage, isFetchingNextPage } = useInviteCodes();
  const disableMutation = useDisableInviteCode();

  const codes = data?.pages.flatMap(p => p.codes) ?? [];

  function disableCode(code: string) {
    disableMutation.mutate(code, {
      onSuccess: () => setMessage(`Code ${code} disabled`),
    });
  }

  return (
    <div>
      <PageHeader
        eyebrow="invites · register"
        title="Invite Codes"
        description="Issue and manage invite codes."
        actions={
          <Button variant="primary" onClick={() => navigate('/invites/create')}>
            Create invite codes
          </Button>
        }
      />

      {message && <p className="mb-4 text-sm text-success-deep dark:text-success">{message}</p>}
      {error && <p className="mb-4 text-sm text-danger">{error.message}</p>}
      {isPending && codes.length === 0 && <p className="mb-4 text-sm text-secondary">Loading...</p>}

      {codes.length === 0 && !isPending ? (
        <EmptyState
          title="No invite codes"
          description="No invite codes have been issued yet."
          action={<Button variant="primary" onClick={() => navigate('/invites/create')}>Create invite codes</Button>}
        />
      ) : (
        <TableBoard>
          <Table>
            <thead>
              <Tr>
                <Th>Code</Th>
                <Th>Available</Th>
                <Th>Disabled</Th>
                <Th>Uses</Th>
                <Th>For Account</Th>
                <Th>Created By</Th>
                <Th>Created At</Th>
                <Th />
              </Tr>
            </thead>
            <tbody>
              {codes.map(c => (
                <Fragment key={c.code}>
                  <Tr>
                    <Td className="font-mono text-xs">
                      <button
                        onClick={() => setExpandedCode(expandedCode === c.code ? null : c.code)}
                        className="mr-2 text-xs text-muted hover:text-secondary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
                      >
                        {expandedCode === c.code ? '▾' : '▸'}
                      </button>
                      {c.code}
                    </Td>
                    <Td>{c.available}</Td>
                    <Td>{c.disabled ? 'Yes' : 'No'}</Td>
                    <Td>{c.uses?.length || 0}</Td>
                    <Td className="max-w-[150px] truncate font-mono text-xs" title={c.forAccount}>
                      {c.forAccount ? <DidLink did={c.forAccount} /> : '—'}
                    </Td>
                    <Td className="max-w-[150px] truncate font-mono text-xs" title={c.createdBy}>
                      {c.createdBy ? <DidLink did={c.createdBy} /> : '—'}
                    </Td>
                    <Td className="text-xs">{c.createdAt ? new Date(c.createdAt).toLocaleString() : '—'}</Td>
                    <Td>
                      {!c.disabled && (
                        <Button variant="ghost" size="sm" onClick={() => disableCode(c.code)} className="text-danger hover:text-danger-deep">
                          Disable
                        </Button>
                      )}
                    </Td>
                  </Tr>
                  {expandedCode === c.code && c.uses && c.uses.length > 0 && (
                    <Tr className="bg-page hover:bg-page">
                      <Td colSpan={8} className="p-3">
                        <Table>
                          <thead>
                            <Tr className="hover:bg-transparent">
                              <Th className="pb-1 pr-4">Used By</Th>
                              <Th className="pb-1">Used At</Th>
                            </Tr>
                          </thead>
                          <tbody>
                            {c.uses.map((u, i) => (
                              <Tr key={i} className="border-b border-hover hover:bg-page">
                                <Td className="py-1.5 pr-4 font-mono text-xs">
                                  <DidLink did={u.usedBy} />
                                </Td>
                                <Td className="py-1.5 text-xs">{new Date(u.usedAt).toLocaleString()}</Td>
                              </Tr>
                            ))}
                          </tbody>
                        </Table>
                      </Td>
                    </Tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </Table>
        </TableBoard>
      )}

      {hasNextPage && (
        <Button variant="secondary" className="mt-4" onClick={() => fetchNextPage()} disabled={isFetchingNextPage}>
          {isFetchingNextPage ? 'Loading...' : 'Load more'}
        </Button>
      )}
    </div>
  );
}
