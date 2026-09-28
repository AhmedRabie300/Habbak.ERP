import { useTranslation } from 'react-i18next';
import { Alert } from '../../../ui-kit/Alert';
import { Button } from '../../../ui-kit/Button';

export interface CustodyMigrationBannerProps {
  /** Number of CustodyRegister rows still on the old, unlinked EmployeeId (Phase 1.2, Option A). Nothing renders when 0. */
  count: number;
  /** Opens the manual linking flow — left to the caller since it doesn't exist yet (see file-level note). */
  onMigrate: () => void;
}

// TODO: Integrate when the CustodyRegister screen is built (Docs/Implementation/HR-MASTER-PLAN.md
// §Phase 1.2, sub-batch 1.2.4 — deferred per Phase-1.2-Research.md §6 question 1: no frontend screen
// exists yet for the accounting module's CustodyRegister, only its backend). Render this banner at the
// top of that screen, passing the unlinked-row count from the diagnostic report
// (GET /api/v1/hr/free-field-migration/diagnostic-report) and an onMigrate that opens the manual
// linking UI once it is built. Isolated on purpose: it must not block or reference a screen that isn't
// there yet.
export function CustodyMigrationBanner({ count, onMigrate }: CustodyMigrationBannerProps) {
  const { t } = useTranslation();

  if (count <= 0) return null;

  return (
    <Alert tone="warning">
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16 }}>
        <div>
          <div style={{ fontWeight: 600 }}>{t('custody.migrationBanner.title')}</div>
          <div>{t('custody.migrationBanner.description', { count })}</div>
        </div>
        <Button variant="secondary" size="sm" onClick={onMigrate}>
          {t('custody.migrationBanner.action')}
        </Button>
      </div>
    </Alert>
  );
}
