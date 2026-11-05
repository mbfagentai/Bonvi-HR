import { describe, expect, it } from 'vitest'

import { auditModuleOfEntity, canSeeAuditModule, isAuditModule } from './audit-keys'

describe('audit module «processes»', () => {
  it('is a module of the journal, opened by processes.view like on the server', () => {
    expect(isAuditModule('processes')).toBe(true)
    expect(canSeeAuditModule('processes', (code) => code === 'processes.view')).toBe(true)
    expect(canSeeAuditModule('processes', (code) => code === 'processes.work')).toBe(false)
  })

  it.each([
    'ProcessTemplate',
    'ProcessTemplateVersion',
    'ProcessCase',
    'ProcessCaseStep',
    'ProcessGateOverride',
    'ProcessStageBinding',
    'ProcessHoliday',
  ])('owns %s, as the server catalog does', (entity) => {
    expect(auditModuleOfEntity(entity)).toBe('processes')
  })
})
