import { describe, expect, it, vi } from 'vitest';
import { normalizeLegacyActivationUrl } from './activation-url';

describe('normalizeLegacyActivationUrl', () => {
  it('repairs activation links whose question mark was encoded into the path', () => {
    const replaceState = vi.fn();

    expect(normalizeLegacyActivationUrl(
      { pathname: '/activate%3Ftoken=C20CFB', search: '' },
      { replaceState },
    )).toBe(true);

    expect(replaceState).toHaveBeenCalledWith(null, '', '/activate?token=C20CFB');
  });

  it('leaves correctly formed activation links unchanged', () => {
    const replaceState = vi.fn();

    expect(normalizeLegacyActivationUrl(
      { pathname: '/activate', search: '?token=C20CFB' },
      { replaceState },
    )).toBe(false);

    expect(replaceState).not.toHaveBeenCalled();
  });
});
