import { useCallback, useEffect, useState } from 'react';
import { useLocation, useSearchParams } from 'react-router-dom';
import {
  cleanSearch,
  parseUsersParameters,
  usersSearchParameters,
  type UsersParameters,
} from './parameters';

export function useUsersParameters() {
  const location = useLocation();
  const [query, setQuery] = useSearchParams();
  const values = parseUsersParameters(query);
  const canonical = usersSearchParameters(values).toString();
  const [draft, setDraft] = useState({
    source: location.key,
    value: values.search,
  });
  if (draft.source !== location.key) {
    setDraft({ source: location.key, value: values.search });
  }
  // Back/forward (including a navigation with the same search) cancels pending typing.
  const search = draft.source === location.key ? draft.value : values.search;
  const update = useCallback(
    (changes: Partial<UsersParameters>) => {
      const current = parseUsersParameters(new URLSearchParams(canonical));
      const next = { ...current, search: cleanSearch(search), ...changes };
      if (next.search !== current.search) next.page = 1;
      setQuery(usersSearchParameters(next));
    },
    [canonical, setQuery, search],
  );
  useEffect(() => {
    if (query.toString() !== canonical) setQuery(canonical, { replace: true });
  }, [query, canonical, setQuery]);
  useEffect(() => {
    if (cleanSearch(search) === values.search) return;
    const timer = setTimeout(
      () => update({ search: cleanSearch(search), page: 1 }),
      400,
    );
    return () => clearTimeout(timer);
  }, [search, values.search, update]);
  return {
    values,
    update,
    search,
    setSearch: (value: string) => setDraft({ source: location.key, value }),
    reset: () => {
      setDraft({ source: location.key, value: '' });
      setQuery('');
    },
  };
}
