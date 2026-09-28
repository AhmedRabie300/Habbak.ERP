import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardHeader, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { ExportMenu } from '../../../ui-kit/ExportMenu';
import { useToastStore } from '../../../store/toastStore';
import { useFieldLabels } from '../../common/useFieldLabels';
import { getFieldErrorMessage } from '../../../app/api';
import { useCodingRule } from '../../settings/codingRules/api';
import { downloadImportTemplate, parseSpreadsheetFile } from '../../../lib/import';
import type { ExportColumn } from '../../../lib/export';
import { useAccountDetail, useAccountTree, useCreateAccount, useDeleteAccount, useUpdateAccount, type AccountTreeNode } from './api';
import { AccountCostCentersEditor } from './AccountCostCentersEditor';

const ACCOUNT_TYPES = ['Asset', 'Liability', 'Equity', 'Revenue', 'Expense'] as const;
const NATURES = ['Debit', 'Credit'] as const;

interface FormState {
  code: string;
  nameAr: string;
  nameEn: string;
  accountType: string;
  nature: string;
  isPostable: boolean;
  currencyCode: string;
  isShared: boolean;
  isActive: boolean;
}

const emptyForm = (): FormState => ({
  code: '', nameAr: '', nameEn: '', accountType: 'Asset', nature: 'Debit',
  isPostable: true, currencyCode: '', isShared: false, isActive: true
});

/** /accounting/chart-of-accounts — the Chart of Accounts tree (01-Module-Accounting.md, section 5, screen 1). */
export function ChartOfAccountsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: tree } = useAccountTree();
  const { label } = useFieldLabels('ACCOUNTING_CHART_OF_ACCOUNTS');
  const { data: codingRule } = useCodingRule('ACCOUNTING_CHART_OF_ACCOUNTS');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;
  const [selectedId, setSelectedId] = useState<number | undefined>();
  const [creatingParentId, setCreatingParentId] = useState<number | null | undefined>(undefined);
  const { data: selected } = useAccountDetail(selectedId);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const [form, setForm] = useState<FormState>(emptyForm());
  const [importing, setImporting] = useState(false);
  const [importPreview, setImportPreview] = useState<Record<string, string>[] | null>(null);
  const [search, setSearch] = useState('');

  const createAccount = useCreateAccount();
  const updateAccount = useUpdateAccount(selectedId ?? 0);
  const deleteAccount = useDeleteAccount();

  // Matches + their ancestor chain, so a hit stays reachable through its full tree path
  // instead of only the leaf itself.
  const visibleTree = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!tree || !query) return tree;

    const byId = new Map(tree.map((n) => [n.id, n]));
    const visibleIds = new Set<number>();

    for (const node of tree) {
      const isMatch =
        node.code.toLowerCase().includes(query) ||
        node.nameAr.toLowerCase().includes(query) ||
        node.nameEn.toLowerCase().includes(query) ||
        node.accountType.toLowerCase().includes(query) ||
        node.nature.toLowerCase().includes(query) ||
        (node.isPostable ? t('common.yes') : t('common.no')).toLowerCase().includes(query);
      if (!isMatch) continue;

      let current: AccountTreeNode | undefined = node;
      while (current && !visibleIds.has(current.id)) {
        visibleIds.add(current.id);
        current = current.parentId ? byId.get(current.parentId) : undefined;
      }
    }

    return tree.filter((n) => visibleIds.has(n.id));
  }, [tree, search, t]);

  const byParent = useMemo(() => {
    const map = new Map<number | 'root', AccountTreeNode[]>();
    for (const node of visibleTree ?? []) {
      const key = node.parentId ?? 'root';
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(node);
    }
    return map;
  }, [visibleTree]);

  // While searching, every visible node auto-expands so a match is reachable without
  // manually clicking through its ancestors first.
  const effectiveExpanded = search.trim() ? new Set(visibleTree?.map((n) => n.id)) : expanded;

  const isCreating = creatingParentId !== undefined;

  const startCreate = (parentId: number | null) => {
    setSelectedId(undefined);
    setCreatingParentId(parentId);
    setForm(emptyForm());
  };

  const selectAccount = (node: AccountTreeNode) => {
    setCreatingParentId(undefined);
    setSelectedId(node.id);
  };

  useEffect(() => {
    if (selected) {
      setForm({
        code: selected.code, nameAr: selected.nameAr, nameEn: selected.nameEn,
        accountType: selected.accountType, nature: selected.nature, isPostable: selected.isPostable,
        currencyCode: selected.currencyCode ?? '', isShared: selected.isSharedAcrossCompanies, isActive: selected.isActive
      });
    }
  }, [selected]);

  const toggleExpand = (id: number) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const handleSave = async () => {
    try {
      if (isCreating) {
        await createAccount.mutateAsync({
          code: codeIsAutomatic ? undefined : form.code, nameAr: form.nameAr, nameEn: form.nameEn, parentId: creatingParentId ?? undefined,
          accountType: form.accountType, nature: form.nature, isPostable: form.isPostable,
          currencyCode: form.currencyCode || undefined, isSharedAcrossCompanies: form.isShared
        });
        showToast(t('accounts.createSuccess'), 'success');
        setCreatingParentId(undefined);
        setForm(emptyForm());
      } else if (selected) {
        await updateAccount.mutateAsync({
          rowVersion: selected.rowVersion, nameAr: form.nameAr, nameEn: form.nameEn,
          accountType: form.accountType, nature: form.nature, isPostable: form.isPostable,
          currencyCode: form.currencyCode || undefined, isActive: form.isActive
        });
        showToast(t('accounts.updateSuccess'), 'success');
      }
    } catch (error) {
      // Field-level errors (e.g. rule 20's approval-required message) aren't toasted centrally.
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!selectedId) return;
    // Fix for the spurious "not found" toast on delete: clear selectedId (which drives
    // useAccountDetail's `enabled`) BEFORE the request starts, rather than after it resolves —
    // otherwise the query is still enabled while the mutation's own onSuccess invalidates/removes
    // the now-deleted account's cache entry, and it immediately refetches into a 404.
    const idToDelete = selectedId;
    setSelectedId(undefined);
    setForm(emptyForm());
    try {
      await deleteAccount.mutateAsync(idToDelete);
      showToast(t('accounts.deleteSuccess'), 'success');
    } catch (error) {
      setSelectedId(idToDelete);
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const exportColumns: ExportColumn<AccountTreeNode>[] = [
    { header: t('accounts.code'), value: (a) => a.code },
    { header: t('accounts.nameAr'), value: (a) => a.nameAr },
    { header: t('accounts.nameEn'), value: (a) => a.nameEn },
    { header: t('accounts.accountType'), value: (a) => a.accountType },
    { header: t('accounts.nature'), value: (a) => a.nature },
    { header: t('accounts.isPostable'), value: (a) => (a.isPostable ? t('common.yes') : t('common.no')) }
  ];

  const handleImportFile = async (file: File) => {
    const rows = await parseSpreadsheetFile(file);
    setImportPreview(rows);
  };

  const confirmImport = async () => {
    if (!importPreview) return;
    const codeToId = new Map<string, number>((tree ?? []).map((a) => [a.code, a.id]));

    for (const row of importPreview) {
      const parentCode = row['ParentCode'] || row['parentCode'];
      const parentId = parentCode ? codeToId.get(parentCode) : undefined;
      try {
        const newId = await createAccount.mutateAsync({
          code: row['Code'] || row['code'],
          nameAr: row['NameAr'] || row['nameAr'],
          nameEn: row['NameEn'] || row['nameEn'],
          parentId,
          accountType: row['AccountType'] || row['accountType'] || 'Asset',
          nature: row['Nature'] || row['nature'] || 'Debit',
          isPostable: String(row['IsPostable'] ?? row['isPostable'] ?? 'true').toLowerCase() === 'true',
          isSharedAcrossCompanies: false
        });
        codeToId.set(row['Code'] || row['code'], newId);
      } catch (error) {
        // Field-level errors aren't toasted centrally — tag with the row's code and keep going.
        const message = getFieldErrorMessage(error);
        if (message) showToast(`${row['Code'] || row['code']}: ${message}`, 'error');
      }
    }

    showToast(t('accounts.createSuccess'), 'success');
    setImportPreview(null);
    setImporting(false);
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('accounts.title')}</h2>

      <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
        <div style={{ flex: 1 }}>
          <ActionBar
            primary={isCreating || selected ? { key: 'save', label: t('common.save'), onClick: handleSave } : undefined}
            secondary={[
              { key: 'newAccount', label: t('accounts.newAccount'), onClick: () => startCreate(null) },
              { key: 'importFromExcel', label: t('common.importFromExcel'), onClick: () => setImporting((v) => !v) },
              ...(isCreating || selected
                ? [{ key: 'cancel', label: t('common.cancel'), onClick: () => { setSelectedId(undefined); setCreatingParentId(undefined); setForm(emptyForm()); } }]
                : [])
            ]}
            destructive={
              !isCreating && selected
                ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('accounts.deleteConfirm') }]
                : []
            }
          />
        </div>
        {tree && <ExportMenu rows={tree} columns={exportColumns} fileName={t('accounts.title')} title={t('accounts.title')} />}
      </div>

      {importing && (
        <Card>
          <CardBody>
            <p style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>
              Code, NameAr, NameEn, ParentCode, AccountType, Nature, IsPostable
            </p>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
              <input
                type="file"
                accept=".xlsx,.xls,.csv"
                onChange={(e) => e.target.files?.[0] && handleImportFile(e.target.files[0])}
              />
              <Button
                type="button"
                variant="secondary"
                onClick={() => downloadImportTemplate(
                  [
                    { header: 'Code', example: '1101' }, { header: 'NameAr', example: 'حساب تجريبي' }, { header: 'NameEn', example: 'Sample Account' },
                    { header: 'ParentCode' }, { header: 'AccountType', example: 'Asset' }, { header: 'Nature', example: 'Debit' }, { header: 'IsPostable', example: 'true' }
                  ],
                  t('accounts.title')
                )}
              >
                {t('common.downloadTemplate')}
              </Button>
            </div>
            {importPreview && (
              <div style={{ marginTop: 12 }}>
                <p style={{ fontSize: 13 }}>{importPreview.length} {t('common.rowsFound')}</p>
                <Button variant="primary" onClick={confirmImport}>{t('common.confirmImport')}</Button>
              </div>
            )}
          </CardBody>
        </Card>
      )}

      <div style={{ display: 'flex', gap: 24 }}>
        <Card style={{ flex: 1 }}>
          <CardBody style={{ maxHeight: 640, overflowY: 'auto' }}>
            <Input
              placeholder={t('common.search')}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{ width: '100%', marginBottom: 12 }}
            />
            {search.trim() && (byParent.get('root') ?? []).length === 0 && (
              <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>
            )}
            {(byParent.get('root') ?? []).map((node) => (
              <TreeNode
                key={node.id}
                node={node}
                byParent={byParent}
                expanded={effectiveExpanded}
                onToggle={toggleExpand}
                onSelect={selectAccount}
                onAddChild={startCreate}
                selectedId={selectedId}
              />
            ))}
          </CardBody>
        </Card>

        <Card style={{ flex: 1 }}>
          {!isCreating && !selected ? (
            <CardBody>
              <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('accounts.selectToEdit')}</p>
            </CardBody>
          ) : (
            <>
              <CardHeader>{isCreating ? t('accounts.newAccount') : `${t('accounts.detailTitle')} — ${selected?.code}`}</CardHeader>
              <CardBody style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>

              <FieldWrapper label={label('code', t('accounts.code'))}>
                {isCreating && codeIsAutomatic ? (
                  <Input value={t('codingRules.autoGeneratedPlaceholder')} disabled />
                ) : (
                  <Input value={form.code} disabled={!isCreating} onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))} />
                )}
              </FieldWrapper>
              <FieldWrapper label={label('nameAr', t('accounts.nameAr'))}>
                <Input value={form.nameAr} onChange={(e) => setForm((f) => ({ ...f, nameAr: e.target.value }))} />
              </FieldWrapper>
              <FieldWrapper label={label('nameEn', t('accounts.nameEn'))}>
                <Input value={form.nameEn} onChange={(e) => setForm((f) => ({ ...f, nameEn: e.target.value }))} />
              </FieldWrapper>
              <FieldWrapper label={label('accountType', t('accounts.accountType'))}>
                <SearchableSelect
                  value={form.accountType}
                  onChange={(v) => setForm((f) => ({ ...f, accountType: v }))}
                  options={ACCOUNT_TYPES.map((type) => ({ value: type, label: t(`accounts.${type.charAt(0).toLowerCase() + type.slice(1)}Type`) }))}
                />
              </FieldWrapper>
              <FieldWrapper label={label('nature', t('accounts.nature'))}>
                <SearchableSelect
                  value={form.nature}
                  onChange={(v) => setForm((f) => ({ ...f, nature: v }))}
                  options={NATURES.map((n) => ({ value: n, label: t(`accounts.${n.toLowerCase()}Nature`) }))}
                />
              </FieldWrapper>
              <label style={{ fontSize: 13, display: 'flex', alignItems: 'center', gap: 6 }}>
                <input type="checkbox" checked={form.isPostable} onChange={(e) => setForm((f) => ({ ...f, isPostable: e.target.checked }))} />
                {label('isPostable', t('accounts.isPostable'))}
              </label>
              {isCreating && (
                <label style={{ fontSize: 13, display: 'flex', alignItems: 'center', gap: 6 }}>
                  <input type="checkbox" checked={form.isShared} onChange={(e) => setForm((f) => ({ ...f, isShared: e.target.checked }))} />
                  {label('shared', t('accounts.shared'))}
                </label>
              )}
              {!isCreating && (
                <label style={{ fontSize: 13, display: 'flex', alignItems: 'center', gap: 6 }}>
                  <input type="checkbox" checked={form.isActive} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))} />
                  {label('isActive', t('accounts.isActive'))}
                </label>
              )}
              </CardBody>
            </>
          )}
        </Card>
      </div>

      {!isCreating && selected && <AccountCostCentersEditor accountId={selected.id} />}
    </div>
  );
}

function TreeNode({
  node, byParent, expanded, onToggle, onSelect, onAddChild, selectedId
}: {
  node: AccountTreeNode;
  byParent: Map<number | 'root', AccountTreeNode[]>;
  expanded: Set<number>;
  onToggle: (id: number) => void;
  onSelect: (node: AccountTreeNode) => void;
  onAddChild: (parentId: number) => void;
  selectedId: number | undefined;
}) {
  const { t } = useTranslation();
  const children = byParent.get(node.id) ?? [];
  const isExpanded = expanded.has(node.id);

  return (
    <div>
      <div
        style={{
          display: 'flex', alignItems: 'center', gap: 6, padding: '5px 8px', cursor: 'pointer', fontSize: 13,
          borderInlineStart: selectedId === node.id ? '3px solid var(--color-gold-500)' : '3px solid transparent',
          borderRadius: '0 6px 6px 0',
          background: selectedId === node.id ? 'var(--color-surface-2)' : 'transparent',
          color: selectedId === node.id ? 'var(--color-navy-700)' : node.isPostable ? 'inherit' : 'var(--color-text-muted)',
          fontWeight: selectedId === node.id ? 700 : node.isPostable ? 400 : 600,
          marginInlineStart: node.level * 16
        }}
      >
        {children.length > 0 ? (
          <span onClick={() => onToggle(node.id)} style={{ width: 14 }}>{isExpanded ? '▾' : '▸'}</span>
        ) : (
          <span style={{ width: 14 }} />
        )}
        <span onClick={() => onSelect(node)} style={{ flex: 1 }}>
          {node.code} — {node.nameAr}
        </span>
        <span
          onClick={() => onAddChild(node.id)}
          title={t('accounts.newChildAccount')}
          style={{ opacity: 0.7, padding: '0 4px' }}
        >
          +
        </span>
      </div>
      {isExpanded && children.map((child) => (
        <TreeNode
          key={child.id}
          node={child}
          byParent={byParent}
          expanded={expanded}
          onToggle={onToggle}
          onSelect={onSelect}
          onAddChild={onAddChild}
          selectedId={selectedId}
        />
      ))}
    </div>
  );
}
