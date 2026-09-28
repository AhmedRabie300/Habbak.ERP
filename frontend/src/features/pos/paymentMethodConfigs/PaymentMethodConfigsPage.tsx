import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { usePOSTerminalsList } from '../terminals/api';
import { usePaymentMethodsList } from '../../accounting/paymentMethods/api';
import { useAccountsList } from '../../accounting/accounts/api';
import { useCreatePOSPaymentMethodConfig, useDeletePOSPaymentMethodConfig, usePOSPaymentMethodConfigsList, useUpdatePOSPaymentMethodConfig } from './api';
import type { POSPaymentMethodConfig } from './types';
import type { PagedResult } from '../../../app/apiTypes';

/** /pos/payment-method-configs — screen #12 (05-Module-POS-Shifts.md, section 2.3). قاعدة 25:
 * LinkedTreasuryAccountId إلزامي لكل طريقة دفع مفعّلة على الجهاز. */
export function PaymentMethodConfigsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);

  const { data: terminals } = usePOSTerminalsList();
  const [posTerminalId, setPosTerminalId] = useState<number | ''>('');

  useEffect(() => {
    if (terminals && terminals.length > 0 && posTerminalId === '') {
      setPosTerminalId(terminals[0].id);
    }
  }, [terminals, posTerminalId]);

  const { data: configs, isLoading } = usePOSPaymentMethodConfigsList(posTerminalId === '' ? undefined : posTerminalId);
  const { data: paymentMethods } = usePaymentMethodsList();
  const { data: accounts } = useAccountsList(true);

  const createMutation = useCreatePOSPaymentMethodConfig();
  const updateMutation = useUpdatePOSPaymentMethodConfig();
  const deleteMutation = useDeletePOSPaymentMethodConfig();

  const [paymentMethodId, setPaymentMethodId] = useState<number | ''>('');
  const [accountId, setAccountId] = useState<number | ''>('');

  const terminalOptions = (terminals ?? []).map((tItem) => ({ value: tItem.id, label: `${tItem.code} — ${tItem.nameAr}` }));
  const paymentMethodOptions = (paymentMethods ?? []).map((p) => ({ value: p.id, label: p.nameAr }));
  const accountOptions = (accounts ?? []).map((a) => ({ value: a.id, label: `${a.code} — ${a.nameAr}` }));

  const data: PagedResult<POSPaymentMethodConfig> = {
    items: configs ?? [], totalCount: configs?.length ?? 0, page: 1, pageSize: Math.max(configs?.length ?? 1, 1)
  };

  const handleAdd = async () => {
    if (posTerminalId === '' || paymentMethodId === '' || accountId === '') return;
    try {
      await createMutation.mutateAsync({ posTerminalId, paymentMethodId, linkedTreasuryAccountId: accountId, isEnabled: true });
      showToast(t('paymentMethodConfigs.createSuccess'), 'success');
      setPaymentMethodId(''); setAccountId('');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const columns: DataGridColumn<POSPaymentMethodConfig>[] = [
    { key: 'paymentMethodNameAr', label: t('paymentMethodConfigs.paymentMethod'), render: (r) => r.paymentMethodNameAr, exportValue: (r) => r.paymentMethodNameAr },
    { key: 'linkedTreasuryAccountNameAr', label: t('paymentMethodConfigs.treasuryAccount'), render: (r) => r.linkedTreasuryAccountNameAr, exportValue: (r) => r.linkedTreasuryAccountNameAr },
    {
      key: 'isEnabled', label: t('paymentMethodConfigs.isEnabled'),
      render: (r) => (
        <input
          type="checkbox" checked={r.isEnabled}
          onChange={(e) => updateMutation.mutateAsync({ id: r.id, linkedTreasuryAccountId: r.linkedTreasuryAccountId, isEnabled: e.target.checked })}
        />
      ),
      exportValue: (r) => (r.isEnabled ? t('common.yes') : t('common.no'))
    },
    {
      key: 'actions', label: '',
      render: (r) => (
        <Button
          variant="ghost"
          onClick={async () => {
            try {
              await deleteMutation.mutateAsync(r.id);
              showToast(t('paymentMethodConfigs.deleteSuccess'), 'success');
            } catch (error) {
              const message = getFieldErrorMessage(error);
              if (message) showToast(message, 'error');
            }
          }}
        >
          {t('common.remove')}
        </Button>
      ),
      exportValue: () => ''
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('paymentMethodConfigs.title')}</h2>

      <Card>
        <CardBody>
          <FieldWrapper label={t('paymentMethodConfigs.terminal')}>
            <SearchableSelect style={{ minWidth: 220 }} value={posTerminalId} onChange={(v) => setPosTerminalId(v === '' ? '' : Number(v))} options={terminalOptions} />
          </FieldWrapper>

          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end', marginTop: 16 }}>
            <FieldWrapper label={t('paymentMethodConfigs.paymentMethod')}>
              <SearchableSelect style={{ minWidth: 200 }} value={paymentMethodId} onChange={(v) => setPaymentMethodId(v === '' ? '' : Number(v))} options={paymentMethodOptions} />
            </FieldWrapper>
            <FieldWrapper label={t('paymentMethodConfigs.treasuryAccount')}>
              <SearchableSelect style={{ minWidth: 240 }} value={accountId} onChange={(v) => setAccountId(v === '' ? '' : Number(v))} options={accountOptions} />
            </FieldWrapper>
            <Button variant="primary" onClick={handleAdd}>{t('paymentMethodConfigs.addConfig')}</Button>
          </div>
        </CardBody>
      </Card>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search=""
        onSearchChange={() => {}}
        page={1}
        onPageChange={() => {}}
        exportFileName={t('paymentMethodConfigs.title')}
      />
    </div>
  );
}
