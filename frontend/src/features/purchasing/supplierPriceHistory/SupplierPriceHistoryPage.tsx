import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { Icon } from '../../../ui-kit/Icon';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { ExportMenu } from '../../../ui-kit/ExportMenu';
import { useFieldLabels } from '../../common/useFieldLabels';
import { printTable, type ExportColumn } from '../../../lib/export';
import { useSuppliersList } from '../suppliers/api';
import { useItemsList } from '../../inventory/items/api';
import { useSupplierPriceHistoryReport } from './api';
import type { SupplierPriceHistoryRow } from './types';

/** /purchasing/supplier-price-history — screen #13, report only (03-Module-Purchasing.md,
 * section 11.1). Rows are recorded automatically on every posted PurchaseInvoice line
 * (PostPurchaseInvoiceCommand) — no manual entry here. */
export function SupplierPriceHistoryPage() {
  const { t } = useTranslation();
  const [supplierId, setSupplierId] = useState<number | ''>('');
  const [itemId, setItemId] = useState<number | ''>('');
  const { label } = useFieldLabels('PURCHASING_SUPPLIER_PRICE_HISTORY');

  const { data: suppliers } = useSuppliersList();
  const { data: items } = useItemsList();
  const { data, isFetching } = useSupplierPriceHistoryReport(
    supplierId === '' ? undefined : supplierId,
    itemId === '' ? undefined : itemId
  );

  const supplierOptions = [{ value: '', label: t('supplierPriceHistory.allSuppliers') }, ...(suppliers ?? []).map((s) => ({ value: s.id, label: `${s.code} — ${s.nameAr}` }))];
  const itemOptions = [{ value: '', label: t('supplierPriceHistory.allItems') }, ...(items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }))];

  const columns: ExportColumn<SupplierPriceHistoryRow>[] = [
    { header: label('supplier', t('supplierPriceHistory.supplier')), value: (r) => `${r.supplierCode} — ${r.supplierNameAr}` },
    { header: label('item', t('supplierPriceHistory.item')), value: (r) => `${r.itemCode} — ${r.itemNameAr}` },
    { header: label('unitPrice', t('supplierPriceHistory.unitPrice')), value: (r) => r.unitPrice },
    { header: label('unit', t('supplierPriceHistory.unit')), value: (r) => r.unitCode },
    { header: label('effectiveDate', t('supplierPriceHistory.effectiveDate')), value: (r) => r.effectiveDate },
    { header: label('purchaseInvoice', t('supplierPriceHistory.purchaseInvoice')), value: (r) => r.purchaseInvoiceNumber ?? '' },
    { header: label('priceChange', t('supplierPriceHistory.priceChange')), value: (r) => r.priceChange ?? '' }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('supplierPriceHistory.title')}</h2>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 12, alignItems: 'end', flexWrap: 'wrap', marginBottom: 16 }}>
            <SearchableSelect value={supplierId} onChange={(v) => setSupplierId(v === '' ? '' : Number(v))} options={supplierOptions} style={{ minWidth: 220 }} />
            <SearchableSelect value={itemId} onChange={(v) => setItemId(v === '' ? '' : Number(v))} options={itemOptions} style={{ minWidth: 220 }} />
            {data && (
              <>
                <Button variant="secondary" onClick={() => printTable(data, columns, t('supplierPriceHistory.title'))}>
                  <Icon name="printer" size={14} />
                  {t('common.print')}
                </Button>
                <ExportMenu rows={data} columns={columns} fileName={t('supplierPriceHistory.title')} title={t('supplierPriceHistory.title')} />
              </>
            )}
          </div>

          {isFetching && <p>{t('common.loading')}</p>}
          {data && data.length === 0 && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('common.noData')}</p>}
          {data && data.length > 0 && (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', fontSize: 13, borderCollapse: 'collapse' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 6 }}>{label('supplier', t('supplierPriceHistory.supplier'))}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{label('item', t('supplierPriceHistory.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{label('unitPrice', t('supplierPriceHistory.unitPrice'))}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{label('unit', t('supplierPriceHistory.unit'))}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{label('effectiveDate', t('supplierPriceHistory.effectiveDate'))}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{label('purchaseInvoice', t('supplierPriceHistory.purchaseInvoice'))}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{label('priceChange', t('supplierPriceHistory.priceChange'))}</th>
                  </tr>
                </thead>
                <tbody>
                  {data.map((r) => (
                    <tr key={r.id}>
                      <td style={{ padding: 6 }}>{r.supplierCode} — {r.supplierNameAr}</td>
                      <td style={{ padding: 6 }}>{r.itemCode} — {r.itemNameAr}</td>
                      <td style={{ padding: 6 }}>{r.unitPrice.toFixed(2)}</td>
                      <td style={{ padding: 6 }}>{r.unitCode}</td>
                      <td style={{ padding: 6 }}>{r.effectiveDate}</td>
                      <td style={{ padding: 6 }}>{r.purchaseInvoiceNumber ?? '—'}</td>
                      <td style={{ padding: 6, color: r.priceChange == null ? undefined : r.priceChange > 0 ? 'var(--color-error)' : r.priceChange < 0 ? 'var(--color-success)' : undefined }}>
                        {r.priceChange == null ? '—' : `${r.priceChange > 0 ? '+' : ''}${r.priceChange.toFixed(2)}`}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardBody>
      </Card>
    </div>
  );
}
