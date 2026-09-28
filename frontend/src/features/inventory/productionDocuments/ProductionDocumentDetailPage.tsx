import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useWarehouseDocument } from '../warehouseDocuments/api';

/** Read-only trace view for a single ProductionIssue/ProductionReceipt row (screen #20). */
export function ProductionDocumentDetailPage({ kind }: { kind: 'production-issues' | 'production-receipts' }) {
  const { t } = useTranslation();
  const { id } = useParams();
  const { data: document, isLoading } = useWarehouseDocument(kind, Number(id));

  if (isLoading || !document) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{document.documentNumber}</h2>
        <StatusBadge status={document.status} />
      </div>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('productionDocuments.documentDate')}>
              <Input value={document.documentDate} disabled />
            </FieldWrapper>
          </div>

          <div style={{ overflowX: 'auto', marginTop: 16 }}>
            <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
              <thead>
                <tr>
                  <th style={{ textAlign: 'start', padding: 8 }}>{t('productionDocuments.item')}</th>
                  <th style={{ textAlign: 'start', padding: 8 }}>{t('productionDocuments.quantity')}</th>
                  <th style={{ textAlign: 'start', padding: 8 }}>{t('productionDocuments.unitCost')}</th>
                </tr>
              </thead>
              <tbody>
                {document.lines.map((line) => (
                  <tr key={line.id ?? line.lineNumber}>
                    <td style={{ padding: 8 }}>{line.itemCode} — {line.itemNameAr}</td>
                    <td style={{ padding: 8 }}>{line.quantity}</td>
                    <td style={{ padding: 8 }}>{line.unitCost}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
