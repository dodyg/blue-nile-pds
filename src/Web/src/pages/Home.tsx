import { useDeferredValue, useState } from 'react';
import { useDirectory, useLookup } from '../hooks/useDirectory';
import AsyncState from '../components/AsyncState';
import { Card, CardHeader } from '../components/Card';
import { Input } from '../components/Input';
import Button from '../components/Button';
import PersonCard from '../components/PersonCard';

export default function Home() {
  const [search, setSearch] = useState('');
  const deferred = useDeferredValue(search);
  const { data, isPending, isError, hasNextPage, isFetchingNextPage, fetchNextPage } = useDirectory();
  const lookup = useLookup(deferred);

  const people = data?.pages.flatMap((page) => page.people) ?? [];
  const canLoadMore = !!hasNextPage && !isFetchingNextPage;

  return (
    <div className="space-y-6">
      <section>
        <h1 className="font-display text-xl font-bold tracking-[0.06em]">People on this PDS</h1>
        <p className="mt-1 text-sm text-secondary">
          Search by handle or did, or browse the directory below.
        </p>
        <form
          className="mt-4 flex max-w-lg gap-2"
          onSubmit={(e) => e.preventDefault()}
          role="search"
        >
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="alice.example or did:plc:…"
            autoComplete="off"
          />
          <Button type="button" disabled={!search.trim()}>
            Search
          </Button>
        </form>
      </section>

      {deferred.trim() && (
        <section>
          <Card>
            <div className="space-y-3 px-4 py-4">
              <h2 className="font-display text-xs font-bold uppercase tracking-[0.14em] text-secondary">
                Search results
              </h2>
              <AsyncState loading={lookup.isPending} error={lookup.isError}>
                {lookup.data && <PersonCard person={lookup.data} />}
                {lookup.isSuccess && !lookup.data && (
                  <p className="text-sm text-secondary">No account found for “{deferred}”.</p>
                )}
              </AsyncState>
            </div>
          </Card>
        </section>
      )}

      <section>
        <Card>
          <CardHeader
            title="Directory"
            actions={
              <Button variant="secondary" size="sm" disabled={!canLoadMore} onClick={() => fetchNextPage()}>
                {isFetchingNextPage ? 'Loading…' : hasNextPage ? 'Load more' : ''}
              </Button>
            }
          />
          <AsyncState loading={isPending} error={isError}>
            <div className="grid grid-cols-1 gap-3 p-4 sm:grid-cols-2">
              {people.map((person) => (
                <PersonCard key={person.did} person={person} />
              ))}
            </div>
          </AsyncState>
        </Card>
      </section>
    </div>
  );
}