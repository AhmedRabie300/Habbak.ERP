import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardHeader, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useCodingRule } from '../../settings/codingRules/api';
import { useBranchesList } from '../../organization/branches/api';
import { useDeleteOrgUnit, useOrgUnits, useSaveOrgUnit, type OrgUnit } from './api';

interface FormState {
  code: string;
  nameAr: string;
  nameEn: string;
  branchId: number | '';
  isActive: boolean;
}

const emptyForm = (): FormState => ({ code: '', nameAr: '', nameEn: '', branchId: '', isActive: true });

/** /hr/org-units — screen HR_ORG_UNITS. Tree pattern ported from
 * features/accounting/chartOfAccounts/ChartOfAccountsPage.tsx (Docs/Implementation/HR-MASTER-PLAN.md
 * §Phase 1.5, Sub-Batch 1.5.2) — OrgUnit has no server-computed `level`, so depth is tracked by the
 * recursive TreeNode itself instead of coming from the API. */
export function OrgUnitsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: units } = useOrgUnits();
  const { data: branches } = useBranchesList();
  const { data: codingRule } = useCodingRule('HR_ORG_UNITS');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const [selectedId, setSelectedId] = useState<number | undefined>();
  const [creatingParentId, setCreatingParentId] = useState<number | null | undefined>(undefined);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const [form, setForm] = useState<FormState>(emptyForm());
  const [search, setSearch] = useState('');

  const selected = units?.find((u) => u.id === selectedId);
  const isCreating = creatingParentId !== undefined;

  const save = useSaveOrgUnit(selectedId);
  const remove = useDeleteOrgUnit();

  const branchOptions = useMemo(
    () => [{ value: '', label: t('hr.orgUnits.noBranch') }, ...(branches ?? []).map((b) => ({ value: b.id, label: b.nameAr }))],
    [branches, t]
  );

  const visibleUnits = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!units || !query) return units;

    const byId = new Map(units.map((u) => [u.id, u]));
    const visibleIds = new Set<number>();

    for (const unit of units) {
      const isMatch = `${unit.code} ${unit.nameAr} ${unit.nameEn}`.toLowerCase().includes(query);
      if (!isMatch) continue;

      let current: OrgUnit | undefined = unit;
      while (current && !visibleIds.has(current.id)) {
        visibleIds.add(current.id);
        current = current.parentId ? byId.get(current.parentId) : undefined;
      }
    }

    return units.filter((u) => visibleIds.has(u.id));
  }, [units, search]);

  const byParent = useMemo(() => {
    const map = new Map<number | 'root', OrgUnit[]>();
    for (const unit of visibleUnits ?? []) {
      const key = unit.parentId ?? 'root';
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(unit);
    }
    return map;
  }, [visibleUnits]);

  const effectiveExpanded = search.trim() ? new Set(visibleUnits?.map((u) => u.id)) : expanded;

  const startCreate = (parentId: number | null) => {
    setSelectedId(undefined);
    setCreatingParentId(parentId);
    setForm(emptyForm());
  };

  const selectUnit = (unit: OrgUnit) => {
    setCreatingParentId(undefined);
    setSelectedId(unit.id);
  };

  useEffect(() => {
    if (selected) {
      setForm({ code: selected.code, nameAr: selected.nameAr, nameEn: selected.nameEn, branchId: selected.branchId ?? '', isActive: selected.isActive });
    }
  }, [selected]);

  const toggleExpand = (id: number) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const cancel = () => {
    setSelectedId(undefined);
    setCreatingParentId(undefined);
    setForm(emptyForm());
  };

  const handleSave = async () => {
    try {
      if (isCreating) {
        const result = await save.mutateAsync({
          code: codeIsAutomatic ? undefined : form.code,
          data: {
            nameAr: form.nameAr, nameEn: form.nameEn, parentId: creatingParentId ?? null,
            branchId: form.branchId === '' ? null : Number(form.branchId), managerEmployeeId: null,
            costCenterDimensionValueId: null, isActive: true
          }
        });
        showToast(t('hr.orgUnits.createSuccess'), 'success');
        setCreatingParentId(undefined);
        setSelectedId(result.id);
      } else if (selected) {
        await save.mutateAsync({
          data: {
            nameAr: form.nameAr, nameEn: form.nameEn, parentId: selected.parentId,
            branchId: form.branchId === '' ? null : Number(form.branchId), managerEmployeeId: selected.managerEmployeeId,
            costCenterDimensionValueId: selected.costCenterDimensionValueId, isActive: form.isActive
          }
        });
        showToast(t('hr.orgUnits.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!selectedId) return;
    const idToDelete = selectedId;
    setSelectedId(undefined);
    setForm(emptyForm());
    try {
      await remove.mutateAsync(idToDelete);
      showToast(t('hr.orgUnits.deleteSuccess'), 'success');
    } catch (error) {
      setSelectedId(idToDelete);
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('hr.orgUnits.title')}</h2>

      <ActionBar
        primary={isCreating || selected ? { key: 'save', label: t('common.save'), onClick: handleSave } : undefined}
        secondary={[
          { key: 'newRoot', label: t('hr.orgUnits.newRoot'), onClick: () => startCreate(null) },
          ...(isCreating || selected ? [{ key: 'cancel', label: t('common.cancel'), onClick: cancel }] : [])
        ]}
        destructive={!isCreating && selected ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('hr.orgUnits.deleteConfirm') }] : []}
      />

      <div style={{ display: 'flex', gap: 24 }}>
        <Card style={{ flex: 1 }}>
          <CardBody style={{ maxHeight: 640, overflowY: 'auto' }}>
            <Input placeholder={t('common.search')} value={search} onChange={(e) => setSearch(e.target.value)} style={{ width: '100%', marginBottom: 12 }} />
            {search.trim() && (byParent.get('root') ?? []).length === 0 && (
              <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>
            )}
            {(byParent.get('root') ?? []).map((unit) => (
              <OrgUnitTreeNode
                key={unit.id}
                unit={unit}
                level={0}
                byParent={byParent}
                expanded={effectiveExpanded}
                onToggle={toggleExpand}
                onSelect={selectUnit}
                onAddChild={startCreate}
                selectedId={selectedId}
              />
            ))}
          </CardBody>
        </Card>

        <Card style={{ flex: 1 }}>
          {!isCreating && !selected ? (
            <CardBody>
              <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('hr.orgUnits.selectToEdit')}</p>
            </CardBody>
          ) : (
            <>
              <CardHeader>{isCreating ? t('hr.orgUnits.newRoot') : `${t('hr.orgUnits.detailTitle')} — ${selected?.code}`}</CardHeader>
              <CardBody style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <FieldWrapper label={t('hr.code')}>
                  {isCreating && codeIsAutomatic ? (
                    <Input value={t('codingRules.autoGeneratedPlaceholder')} disabled />
                  ) : (
                    <Input value={form.code} disabled={!isCreating} onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))} />
                  )}
                </FieldWrapper>
                <FieldWrapper label={t('hr.nameAr')}>
                  <Input value={form.nameAr} onChange={(e) => setForm((f) => ({ ...f, nameAr: e.target.value }))} />
                </FieldWrapper>
                <FieldWrapper label={t('hr.nameEn')}>
                  <Input value={form.nameEn} onChange={(e) => setForm((f) => ({ ...f, nameEn: e.target.value }))} />
                </FieldWrapper>
                <FieldWrapper label={t('hr.orgUnits.branch')}>
                  <SearchableSelect
                    value={form.branchId}
                    onChange={(v) => setForm((f) => ({ ...f, branchId: v === '' ? '' : Number(v) }))}
                    options={branchOptions}
                  />
                </FieldWrapper>
                {!isCreating && (
                  <label style={{ fontSize: 13, display: 'flex', alignItems: 'center', gap: 6 }}>
                    <input type="checkbox" checked={form.isActive} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))} />
                    {t('hr.isActive')}
                  </label>
                )}
              </CardBody>
            </>
          )}
        </Card>
      </div>
    </div>
  );
}

function OrgUnitTreeNode({
  unit, level, byParent, expanded, onToggle, onSelect, onAddChild, selectedId
}: {
  unit: OrgUnit;
  level: number;
  byParent: Map<number | 'root', OrgUnit[]>;
  expanded: Set<number>;
  onToggle: (id: number) => void;
  onSelect: (unit: OrgUnit) => void;
  onAddChild: (parentId: number) => void;
  selectedId: number | undefined;
}) {
  const { t } = useTranslation();
  const children = byParent.get(unit.id) ?? [];
  const isExpanded = expanded.has(unit.id);

  return (
    <div>
      <div
        style={{
          display: 'flex', alignItems: 'center', gap: 6, padding: '5px 8px', cursor: 'pointer', fontSize: 13,
          borderInlineStart: selectedId === unit.id ? '3px solid var(--color-gold-500)' : '3px solid transparent',
          borderRadius: '0 6px 6px 0',
          background: selectedId === unit.id ? 'var(--color-surface-2)' : 'transparent',
          color: selectedId === unit.id ? 'var(--color-navy-700)' : unit.isActive ? 'inherit' : 'var(--color-text-muted)',
          fontWeight: selectedId === unit.id ? 700 : 400,
          marginInlineStart: level * 16
        }}
      >
        {children.length > 0 ? (
          <span onClick={() => onToggle(unit.id)} style={{ width: 14 }}>{isExpanded ? '▾' : '▸'}</span>
        ) : (
          <span style={{ width: 14 }} />
        )}
        <span onClick={() => onSelect(unit)} style={{ flex: 1 }}>
          {unit.code} — {unit.nameAr}
        </span>
        <span onClick={() => onAddChild(unit.id)} title={t('hr.orgUnits.newChild')} style={{ opacity: 0.7, padding: '0 4px' }}>
          +
        </span>
      </div>
      {isExpanded && children.map((child) => (
        <OrgUnitTreeNode
          key={child.id}
          unit={child}
          level={level + 1}
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
