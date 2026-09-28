import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { usePOSTerminalsList } from '../terminals/api';
import { useOpenCheckForTable } from '../checks/api';
import { useCreateTable, useSetTableStatus, useTableBoard } from './api';
import type { TableBoardItem, TableStatus } from './types';

/** ألوان الطرابيزات — نفس منطق tchip-lg في الموك أب (Coffee_ERP_Full_System_Mockup.html): تعبئة
 * صلبة بلون الحالة + نص أبيض، مش تلميحة لون خفيفة. */
const STATUS_FILL: Record<TableStatus, string> = {
  Free: 'var(--color-success)',
  Busy: 'var(--color-gold-500)',
  Reserved: 'var(--color-warning)',
  Cleaning: 'var(--color-text-muted)'
};

const LEGEND: { status: TableStatus; labelKey: string }[] = [
  { status: 'Free', labelKey: 'status.Free' },
  { status: 'Busy', labelKey: 'status.Busy' },
  { status: 'Reserved', labelKey: 'status.Reserved' },
  { status: 'Cleaning', labelKey: 'status.Cleaning' }
];

/** /pos/table-board — screen #8 (05-Module-POS-Shifts.md)، مصمَّمة على غرار شاشة "الطرابيزات" في
 * Coffee_ERP_Full_System_Mockup.html (شريط ألوان الحالة + شبكة بلاطات كبيرة صلبة اللون). حالة كل
 * طرابيزة حيًا، الضغط عليها يفتح أو يسترجع الشيك المرتبط (قاعدة 7). بتحدّث نفسها كل 5 ثواني
 * (polling) عشان تعكس حركة باقي الكاشيرات على نفس الفرع بدون الحاجة لـWebSocket في هذه المرحلة.
 */
export function TableBoardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: terminals } = usePOSTerminalsList();
  const [posTerminalId, setPosTerminalId] = useState<number | ''>('');

  useEffect(() => {
    if (terminals && terminals.length > 0 && posTerminalId === '') {
      setPosTerminalId(terminals[0].id);
    }
  }, [terminals, posTerminalId]);

  const selectedTerminal = terminals?.find((tItem) => tItem.id === posTerminalId);
  const { data: tables, isLoading } = useTableBoard(selectedTerminal?.branchId);

  const openCheckMutation = useOpenCheckForTable();
  const setStatusMutation = useSetTableStatus();
  const createTableMutation = useCreateTable();

  const [showAddTable, setShowAddTable] = useState(false);
  const [newCode, setNewCode] = useState('');
  const [newNameAr, setNewNameAr] = useState('');
  const [newNameEn, setNewNameEn] = useState('');

  const terminalOptions = (terminals ?? []).map((tItem) => ({ value: tItem.id, label: `${tItem.code} — ${tItem.nameAr}` }));

  const handleTableClick = async (table: TableBoardItem) => {
    if (posTerminalId === '') return;

    if (table.status === 'Free' || table.status === 'Busy') {
      try {
        const { id } = await openCheckMutation.mutateAsync({ posTerminalId, tableId: table.id });
        navigate(`/pos/checks/${id}`);
      } catch (error) {
        const message = getFieldErrorMessage(error);
        if (message) showToast(message, 'error');
      }
      return;
    }

    // Reserved/Cleaning: مفيش شيك يتفتح، بس زر سريع لإرجاعها Free (قاعدة 30 — تأكيد يدوي).
  };

  const handleMakeFree = async (tableId: number) => {
    try {
      await setStatusMutation.mutateAsync({ id: tableId, status: 'Free' });
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleAddTable = async () => {
    if (selectedTerminal === undefined || !newNameAr || !newNameEn) return;
    try {
      await createTableMutation.mutateAsync({
        code: newCode || undefined, nameAr: newNameAr, nameEn: newNameEn, branchId: selectedTerminal.branchId
      });
      showToast(t('tableBoard.addTableSuccess'), 'success');
      setNewCode(''); setNewNameAr(''); setNewNameEn(''); setShowAddTable(false);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
        <h2 style={{ margin: 0 }}>{t('tableBoard.title')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button variant="secondary" onClick={() => navigate('/pos/checks/open')}>{t('tableBoard.openChecks')}</Button>
          <Button variant="secondary" onClick={() => navigate('/pos/checks/held')}>{t('tableBoard.heldChecks')}</Button>
          <Button variant="primary" onClick={() => setShowAddTable((v) => !v)}>{t('tableBoard.addTable')}</Button>
        </div>
      </div>

      <Card>
        <CardBody>
          <FieldWrapper label={t('tableBoard.terminal')}>
            <SearchableSelect style={{ minWidth: 220 }} value={posTerminalId} onChange={(v) => setPosTerminalId(v === '' ? '' : Number(v))} options={terminalOptions} />
          </FieldWrapper>
        </CardBody>
      </Card>

      {showAddTable && (
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
              <FieldWrapper label={t('tableBoard.code')}>
                <Input placeholder={t('codingRules.autoGeneratedPlaceholder')} value={newCode} onChange={(e) => setNewCode(e.target.value)} style={{ width: 100 }} />
              </FieldWrapper>
              <FieldWrapper label={t('tableBoard.nameAr')}>
                <Input value={newNameAr} onChange={(e) => setNewNameAr(e.target.value)} style={{ width: 160 }} />
              </FieldWrapper>
              <FieldWrapper label={t('tableBoard.nameEn')}>
                <Input value={newNameEn} onChange={(e) => setNewNameEn(e.target.value)} style={{ width: 160 }} />
              </FieldWrapper>
              <Button variant="primary" onClick={handleAddTable}>{t('common.saveDraft')}</Button>
            </div>
          </CardBody>
        </Card>
      )}

      <div style={{ display: 'flex', gap: 18, flexWrap: 'wrap', fontSize: 12.5, color: 'var(--color-text-muted)' }}>
        {LEGEND.map((l) => (
          <span key={l.status} style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ width: 9, height: 9, borderRadius: '50%', background: STATUS_FILL[l.status], display: 'inline-block' }} />
            {t(l.labelKey)}
          </span>
        ))}
      </div>

      {isLoading && <div>{t('common.loading')}</div>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(120px, 1fr))', gap: 14 }}>
        {(tables ?? []).map((table) => (
          <div
            key={table.id}
            onClick={() => handleTableClick(table)}
            style={{
              background: STATUS_FILL[table.status], color: '#fff', borderRadius: 'var(--radius)', padding: '16px 10px',
              cursor: table.status === 'Free' || table.status === 'Busy' ? 'pointer' : 'default',
              display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4, textAlign: 'center', minHeight: 96,
              boxShadow: 'var(--shadow-1)'
            }}
          >
            <div style={{ fontWeight: 800, fontSize: 17 }}>{table.code}</div>
            <span style={{ fontSize: 11, opacity: 0.9 }}>{t(`status.${table.status}`, table.status)}</span>
            {table.openCheckCode && (
              <span style={{ fontSize: 10, opacity: 0.85 }}>
                {table.openCheckCode} · {(table.openCheckTotal ?? 0).toFixed(0)} {t('common.currency', 'ج.م')}
              </span>
            )}
            {table.status === 'Cleaning' && (
              <Button variant="ghost" size="sm" style={{ color: '#fff', borderColor: 'rgba(255,255,255,0.5)' }} onClick={(e) => { e.stopPropagation(); handleMakeFree(table.id); }}>
                {t('tableBoard.cleaningDone')}
              </Button>
            )}
            {table.status === 'Reserved' && (
              <Button variant="ghost" size="sm" style={{ color: '#fff', borderColor: 'rgba(255,255,255,0.5)' }} onClick={(e) => { e.stopPropagation(); handleMakeFree(table.id); }}>
                {t('tableBoard.cancelReservation')}
              </Button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
