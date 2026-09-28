import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge } from '../../ui-kit/Badge';
import { Card, CardBody } from '../../ui-kit/Card';
import { money } from '../fixedAssets/listing';
import { useMaintenanceBoard, type MaintenanceBoardCard, type MaintenanceBoardColumn } from './api';

const columns: MaintenanceBoardColumn[] = ['Reported', 'Planned', 'InProgress', 'Done'];

const severityTone = { Low: 'neutral', Medium: 'info', High: 'warning', Critical: 'error' } as const;

/**
 * /maintenance/board — screen #12: the whole workshop at a glance. Reported faults on the left,
 * jobs moving right as they are planned, worked on and finished. A card opens its own screen, where
 * the buttons that move it live — the board shows where things stand, it does not post anything.
 */
export function MaintenanceBoardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data, isLoading } = useMaintenanceBoard();

  if (isLoading) return <div>{t('common.loading')}</div>;

  const cardsOf = (column: MaintenanceBoardColumn) => (data ?? []).filter((c) => c.column === column);

  const open = (card: MaintenanceBoardCard) =>
    navigate(card.kind === 'Issue' ? `/maintenance/issues/${card.id}` : `/maintenance/requests/${card.id}`);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('maintenance.boardTitle')}</h2>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, minmax(220px, 1fr))', gap: 12, alignItems: 'start' }}>
        {columns.map((column) => {
          const cards = cardsOf(column);
          return (
            <div key={column} style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  padding: '8px 12px',
                  background: 'var(--color-surface-muted, #f3f4f6)',
                  borderRadius: 8,
                  fontWeight: 600
                }}
              >
                <span>{t(`maintenance.boardColumns.${column}`)}</span>
                <span style={{ color: 'var(--color-text-muted)' }}>{cards.length}</span>
              </div>

              {cards.map((card) => (
                <Card key={`${card.kind}-${card.id}`}>
                  <CardBody>
                    <button
                      type="button"
                      onClick={() => open(card)}
                      style={{
                        all: 'unset',
                        cursor: 'pointer',
                        display: 'flex',
                        flexDirection: 'column',
                        gap: 6,
                        width: '100%'
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                        <strong>{card.number}</strong>
                        {card.severity && <Badge label={t(`maintenance.severities.${card.severity}`)} tone={severityTone[card.severity]} />}
                      </div>
                      <div>{card.title}</div>
                      <div style={{ color: 'var(--color-text-muted)', fontSize: 12 }}>
                        {card.assetNameAr ?? '—'}
                        {card.assetNumber ? ` (${card.assetNumber})` : ''}
                      </div>
                      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: 'var(--color-text-muted)' }}>
                        <span>{card.date ?? ''}</span>
                        <span>{card.cost != null ? money(card.cost) : ''}</span>
                      </div>
                      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                        {card.technicianName && <Badge label={card.technicianName} tone="neutral" />}
                        {card.fromSchedule && <Badge label={t('maintenance.preventive')} tone="info" />}
                        {card.needsApproval && <Badge label={t('maintenance.needsApproval')} tone="warning" />}
                      </div>
                    </button>
                  </CardBody>
                </Card>
              ))}

              {cards.length === 0 && <div style={{ color: 'var(--color-text-muted)', padding: 8 }}>{t('maintenance.boardEmpty')}</div>}
            </div>
          );
        })}
      </div>
    </div>
  );
}
