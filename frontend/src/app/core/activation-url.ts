export function normalizeLegacyActivationUrl(
  location: Pick<Location, 'pathname' | 'search'>,
  history: Pick<History, 'replaceState'>,
): boolean {
  if (location.search) return false;

  const match = /^\/activate%3ftoken=([^/]+)$/i.exec(location.pathname);
  if (!match) return false;

  history.replaceState(null, '', `/activate?token=${encodeURIComponent(decodeURIComponent(match[1]))}`);
  return true;
}
