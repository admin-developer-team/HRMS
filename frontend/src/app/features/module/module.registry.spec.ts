import { MODULES } from './module.registry';

describe('HRMS module registry', () => {
  it('provides a data-backed view for every enterprise module', () => {
    const expected = [
      'platform',
      'organization',
      'leave',
      'attendance',
      'workforce',
      'payroll',
      'recruitment',
      'performance',
      'assets',
      'expenses',
      'training',
      'identity',
      'audit',
    ];
    expect(Object.keys(MODULES)).toEqual(expect.arrayContaining(expected));
    for (const module of Object.values(MODULES)) {
      expect(module.views.length).toBeGreaterThan(0);
      for (const view of module.views) {
        expect(view.endpoint.startsWith('/')).toBe(true);
        expect(view.columns.length).toBeGreaterThan(0);
      }
    }
  });

  it('connects all state-changing controls to API routes', () => {
    for (const module of Object.values(MODULES)) {
      for (const view of module.views) {
        if (view.createLabel) expect(view.createEndpoint).toBeTruthy();
        for (const action of [...(view.toolbarActions ?? []), ...(view.rowActions ?? [])]) {
          if (action.path) {
            expect(action.path.startsWith('/')).toBe(true);
            expect(['get', 'post', 'put', 'delete']).toContain(action.method);
          } else {
            expect(action.method).toBe('documents');
            expect(action.documentOwnerType).toBeTruthy();
          }
        }
      }
    }
  });
});
