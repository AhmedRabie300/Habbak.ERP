import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { usePOSTerminalsList } from '../terminals/api';
import { useShiftsList } from '../shifts/api';
import { useAccountsList } from '../../accounting/accounts/api';
import { useCreateDrawerExpense, useDrawerExpensesList } from './api';
import type { DrawerExpense } from './types';
import type { PagedResult } from '../../../app/apiTypes';

/** /pos/drawer-expenses — screen #17 (05-Module-POS-Shifts.md، قاعدة 33): مصروف نقدي أثناء
 * الوردية يتطلب حساب مصروفات محدَّد. القيد المحاسبي الآلي مؤجَّل (نفس تأجيل الموديول كله). */
export function DrawerExpensesPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);

  const { data: terminals } = usePOSTerminalsList();
  const [posTerminalId, setPosTerminalId] = useState<number | ''>('');
  const { data: shifts } = useShiftsList(posTerminalId === '' ? undefined : posTerminalId, undefined);
  const [shiftId, setShiftId] = useState<number | ''>('');
  const { data: accounts } = useAccountsList(true);

  useEffect(() => {
    if (terminals && terminals.length > 0 && posTerminalId === '') {
      setPosTerminalId(terminals[0].id);
    }
  }, [terminals, posTerminalId]);

  useEffect(() => {
    if (shifts && shifts.length > 0) {
      const openShift = shifts.find((s) => s.status === 'Open');
      setShiftId(openShift?.id ?? shifts[0].id);
    } else {
      setShiftId('');
    }
  }, [shifts]);

  const { data: expenses, isLoading } = useDrawerExpensesList(shiftId === '' ? undefined : shiftId);
  const createMutation = useCreateDrawerExpense();

  const [expenseAccountId, setExpenseAccountId] = useState<number | ''>('');
  const [amount, setAmount] = useState('');
  const [description, setDescription] = useState('');

  const terminalOptions = (terminals ?? []).map((tItem) => ({ value: tItem.id, label: `${tItem.code} — ${tItem.nameAr}` }));
  const shiftOptions = (shifts ?? []).map((s) => ({ value: s.id, label: `#${s.id} — ${t(`status.${s.status}`, s.status)} — ${new Date(s.openedAtUtc).toLocaleString()}` }));
  const accountOptions = (accounts ?? []).map((a) => ({ value: a.id, label: `${a.code} — ${a.nameAr}` }));

  const handleAdd = async () => {
    if (shiftId === '' || expenseAccountId === '' || !amount || Number(amount) <= 0 || !description.trim()) return;
    try {
      await createMutation.mutateAsync({ shiftId, expenseAccountId, amount: Number(amount), description: description.trim() });
      showToast(t('drawerExpenses.createSuccess'), 'success');
      setAmount(''); setDescription('');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const data: PagedResult<DrawerExpense> = {
    items: expenses ?? [], totalCount: expenses?.length ?? 0, page: 1, pageSize: Math.max(expenses?.length ?? 1, 1)
  };

  const columns: DataGridColumn<DrawerExpense>[] = [
    { key: 'expenseAccountNameAr', label: t('drawerExpenses.account'), render: (r) => r.expenseAccountNameAr, exportValue: (r) => r.expenseAccountNameAr },
    { key: 'amount', label: t('drawerExpenses.amount'), render: (r) => r.amount.toFixed(2), exportValue: (r) => r.amount },
    { key: 'description', label: t('drawerExpenses.description'), render: (r) => r.description, exportValue: (r) => r.description },
    { key: 'createdAtUtc', label: t('drawerExpenses.createdAt'), render: (r) => new Date(r.createdAtUtc).toLocaleString(), exportValue: (r) => r.createdAtUtc }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('drawerExpenses.title')}</h2>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('drawerExpenses.terminal')}>
              <SearchableSelect style={{ minWidth: 220 }} value={posTerminalId} onChange={(v) => setPosTerminalId(v === '' ? '' : Number(v))} options={terminalOptions} />
            </FieldWrapper>
            <FieldWrapper label={t('drawerExpenses.shift')}>
              <SearchableSelect style={{ minWidth: 280 }} value={shiftId} onChange={(v) => setShiftId(v === '' ? '' : Number(v))} options={shiftOptions} />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      {shiftId !== '' && (
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
              <FieldWrapper label={t('drawerExpenses.account')}>
                <SearchableSelect style={{ minWidth: 240 }} value={expenseAccountId} onChange={(v) => setExpenseAccountId(v === '' ? '' : Number(v))} options={accountOptions} />
              </FieldWrapper>
              <FieldWrapper label={t('drawerExpenses.amount')}>
                <Input type="number" step="0.01" style={{ width: 120 }} value={amount} onChange={(e) => setAmount(e.target.value)} />
              </FieldWrapper>
              <FieldWrapper label={t('drawerExpenses.description')}>
                <Input style={{ width: 240 }} value={description} onChange={(e) => setDescription(e.target.value)} />
              </FieldWrapper>
              <Button variant="primary" onClick={handleAdd}>{t('drawerExpenses.addExpense')}</Button>
            </div>
          </CardBody>
        </Card>
      )}

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search=""
        onSearchChange={() => {}}
        page={1}
        onPageChange={() => {}}
        exportFileName={t('drawerExpenses.title')}
      />
    </div>
  );
}
