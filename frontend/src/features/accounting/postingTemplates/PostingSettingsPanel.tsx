import { useEffect, useState, type CSSProperties } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody, CardHeader } from '../../../ui-kit/Card';
import { Button } from '../../../ui-kit/Button';
import { Badge } from '../../../ui-kit/Badge';
import { Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { usePermission } from '../../../ui-kit/usePermission';
import { getFieldErrorMessage } from '../../../app/api';
import { useAccountsList } from '../accounts/api';
import { useAccountDimensionLinks, useDimensionsList, useDimensionValues, type Dimension } from '../dimensions/api';
import {
  useCreateDefaultTemplates, useCreateTemplate, usePostingEngineCatalog, usePostingTemplate, usePreviewScreen, usePreviewTemplate,
  useSetScreenActive, useSetTemplateActive, useUpdateTemplate,
  type AccountSource, type AmountFormula, type Condition, type CostCenterInput, type CostCenterSource, type PostingScreen,
  type PostingScreenTemplate, type Preview, type PreviewSample, type ScreenPreviewItem, type TemplateDefinition, type TemplateLineInput,
  type TriggerType
} from './api';

const labelStyle: CSSProperties = { fontSize: 12, color: 'var(--color-text-muted)', fontWeight: 600 };
const fieldCol: CSSProperties = { display: 'flex', flexDirection: 'column', gap: 4, minWidth: 0 };
const muted: CSSProperties = { margin: 0, fontSize: 12.5, color: 'var(--color-text-muted)', lineHeight: 1.8 };

function useRun() {
  const showToast = useToastStore((s) => s.show);
  return async (action: () => Promise<unknown>, success?: string) => {
    try {
      await action();
      if (success) showToast(success, 'success');
      return true;
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
      return false;
    }
  };
}

/**
 * Shown for a screen that has no posting hook in the system (design notes, note 4). The switch is
 * visible — every screen shows its posting settings — but it cannot be turned on: nothing in that
 * screen's code calls the posting engine, so a template there would never run. It says so instead.
 */
export function NotWiredPostingPanel() {
  const { t } = useTranslation();
  return (
    <Card>
      <CardHeader>{t('postingSettings.title')}</CardHeader>
      <CardBody>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13.5, fontWeight: 600, opacity: 0.55 }}>
          <input type="checkbox" checked={false} disabled readOnly />
          {t('postingSettings.screenPostsQuestion')}
        </label>
        <p style={{ ...muted, marginTop: 8 }}>{t('postingSettings.notWired')}</p>
      </CardBody>
    </Card>
  );
}

/**
 * The "إعدادات القيد" part of screen settings (00-Posting-Engine-Architecture.md section 9, design
 * notes 2026-09-18): whether the screen posts, and the templates it posts with — one entry each, in
 * execution order, each with its own trigger. Every choice is a closed list (spec section 4); the
 * server re-checks all of it when a template is saved.
 */
export function PostingSettingsPanel({ screen }: { screen: PostingScreen }) {
  const { t } = useTranslation();
  const run = useRun();
  const canAdd = usePermission('add');
  const canEdit = usePermission('edit');
  const setScreenActive = useSetScreenActive();
  const createDefaults = useCreateDefaultTemplates();
  const [editing, setEditing] = useState<{ templateId: number | null } | null>(null);
  const [showPreview, setShowPreview] = useState(false);

  const hasTemplates = screen.templates.length > 0;

  return (
    <Card>
      <CardHeader
        end={
          <label title={hasTemplates ? undefined : t('postingSettings.switchNeedsTemplate')}
            style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13.5, fontWeight: 700, opacity: hasTemplates ? 1 : 0.55 }}>
            <input
              type="checkbox"
              checked={screen.isPosting}
              disabled={!canEdit || !hasTemplates || setScreenActive.isPending}
              onChange={(e) => run(
                () => setScreenActive.mutateAsync({ screenCode: screen.screenCode, isActive: e.target.checked }),
                e.target.checked ? t('postingSettings.activated') : t('postingSettings.deactivated'))}
            />
            {t('postingSettings.screenPostsQuestion')}
          </label>
        }
      >
        {t('postingSettings.title')}
      </CardHeader>
      <CardBody>
        <p style={{ ...muted, marginBottom: 14 }}>{screen.whenAr}</p>

        <div style={{ fontWeight: 700, fontSize: 13.5, marginBottom: 8 }}>{t('postingSettings.linkedTemplates')}</div>
        {hasTemplates ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {screen.templates.map((template) => (
              <TemplateRow
                key={template.id}
                screen={screen}
                template={template}
                isEditing={editing?.templateId === template.id}
                onEdit={() => setEditing({ templateId: template.id })}
              />
            ))}
          </div>
        ) : (
          <p style={muted}>{t('postingSettings.noTemplates')}</p>
        )}

        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 12 }}>
          {canAdd && !hasTemplates && screen.defaultTemplateCount > 0 && (
            <Button size="sm" variant="primary" disabled={createDefaults.isPending}
              onClick={() => run(() => createDefaults.mutateAsync(screen.screenCode), t('postingSettings.defaultCreated'))}>
              {t('postingSettings.startFromDefault', { count: screen.defaultTemplateCount })}
            </Button>
          )}
          {canAdd && <Button size="sm" variant="secondary" onClick={() => setEditing({ templateId: null })}>{t('postingSettings.addTemplate')}</Button>}
          {hasTemplates && (
            <Button size="sm" variant="ghost" onClick={() => setShowPreview((v) => !v)}>{t('postingSettings.previewAll')}</Button>
          )}
        </div>

        {showPreview && hasTemplates && <ScreenPreview screen={screen} />}

        {editing && (
          <TemplateEditor
            key={editing.templateId ?? 'new'}
            screen={screen}
            templateId={editing.templateId}
            onClose={() => setEditing(null)}
          />
        )}
      </CardBody>
    </Card>
  );
}

function TemplateRow({ screen, template, isEditing, onEdit }: {
  screen: PostingScreen; template: PostingScreenTemplate; isEditing: boolean; onEdit: () => void;
}) {
  const { t } = useTranslation();
  const run = useRun();
  const canEdit = usePermission('edit');
  const setActive = useSetTemplateActive();

  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap', padding: '10px 12px',
      border: `1px solid ${isEditing ? 'var(--color-primary)' : 'var(--color-border)'}`, borderRadius: 8
    }}>
      <span style={{
        fontVariantNumeric: 'tabular-nums', fontWeight: 700, fontSize: 12, width: 26, height: 26, borderRadius: '50%',
        display: 'grid', placeItems: 'center', background: 'var(--color-surface-2)'
      }}>{template.executionOrder}</span>
      <div style={{ flex: '1 1 240px', minWidth: 0 }}>
        <div style={{ fontWeight: 600, fontSize: 13.5 }}>{template.nameAr}</div>
        <div style={{ fontSize: 12, color: 'var(--color-text-muted)', marginTop: 2 }}>
          {describeTrigger(t, screen, template.triggerType, template.triggerFieldName, template.triggerFieldValue)}
          {' · '}{t('postingSettings.version', { version: template.versionNumber })}
          {' · '}{t('postingSettings.lineCount', { count: template.lineCount })}
        </div>
      </div>
      <Badge label={template.isActive ? t('postingSettings.active') : t('postingSettings.inactive')} tone={template.isActive ? 'success' : 'neutral'} />
      <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12.5 }}>
        <input type="checkbox" checked={template.isActive} disabled={!canEdit || setActive.isPending}
          onChange={(e) => run(() => setActive.mutateAsync({ id: template.id, isActive: e.target.checked }))} />
        {t('postingSettings.templateOn')}
      </label>
      <Button size="sm" variant="secondary" onClick={onEdit}>{t('postingSettings.edit')}</Button>
    </div>
  );
}

function describeTrigger(
  t: (key: string, options?: Record<string, unknown>) => string, screen: PostingScreen,
  type: TriggerType, field: string | null, value: string | null
) {
  if (type === 'HasStockMovement') return t('postingSettings.trigger.HasStockMovement');
  if (type === 'FieldCondition') {
    const label = screen.fields.find((f) => f.name === field)?.labelAr ?? field;
    return t('postingSettings.trigger.FieldConditionIs', { field: label, value: t(`postingSettings.choice.${value}`, { defaultValue: value }) });
  }
  return t('postingSettings.trigger.Always');
}

// ---------------------------------------------------------------------------- sample document

function defaultSample(screen: PostingScreen): PreviewSample {
  return {
    values: Object.fromEntries([
      ...screen.fields.filter((f) => f.kind === 'Amount').map((f) => [f.name, f.sample ?? 0]),
      ...screen.groups.map((g) => [g.name, g.sample])
    ]),
    texts: Object.fromEntries(screen.fields.filter((f) => f.choices?.length).map((f) => [f.name, f.choices![0]])),
    hasStockMovement: screen.canMoveStock
  };
}

function SampleEditor({ screen, sample, onChange }: { screen: PostingScreen; sample: PreviewSample; onChange: (s: PreviewSample) => void }) {
  const { t } = useTranslation();
  return (
    <details style={{ fontSize: 13 }}>
      <summary style={{ cursor: 'pointer', fontWeight: 600 }}>{t('postingSettings.sampleValues')}</summary>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: 10, marginTop: 10 }}>
        {screen.fields.filter((f) => f.choices?.length).map((f) => (
          <div key={f.name} style={fieldCol}>
            <span style={labelStyle}>{f.labelAr}</span>
            <SearchableSelect value={sample.texts[f.name] ?? ''}
              onChange={(v) => onChange({ ...sample, texts: { ...sample.texts, [f.name]: v } })}
              options={f.choices!.map((c) => ({ value: c, label: t(`postingSettings.choice.${c}`, { defaultValue: c }) }))} />
          </div>
        ))}
        {[...screen.fields.filter((f) => f.kind === 'Amount').map((f) => ({ name: f.name, label: f.labelAr })),
          ...screen.groups.map((g) => ({ name: g.name, label: g.labelAr }))].map((f) => (
          <div key={f.name} style={fieldCol}>
            <span style={labelStyle}>{f.label}</span>
            <Input type="number" step="0.01" value={sample.values[f.name] ?? 0}
              onChange={(e) => onChange({ ...sample, values: { ...sample.values, [f.name]: Number(e.target.value) } })} />
          </div>
        ))}
        {screen.canMoveStock && (
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, alignSelf: 'end', paddingBottom: 8 }}>
            <input type="checkbox" checked={sample.hasStockMovement} onChange={(e) => onChange({ ...sample, hasStockMovement: e.target.checked })} />
            {t('postingSettings.sampleMovedStock')}
          </label>
        )}
      </div>
    </details>
  );
}

function ScreenPreview({ screen }: { screen: PostingScreen }) {
  const { t } = useTranslation();
  const run = useRun();
  const previewScreen = usePreviewScreen();
  const [sample, setSample] = useState<PreviewSample>(() => defaultSample(screen));
  const [items, setItems] = useState<ScreenPreviewItem[] | null>(null);

  const refresh = () => run(async () => setItems(await previewScreen.mutateAsync({ screenCode: screen.screenCode, sample })));

  return (
    <div style={{ marginTop: 14, padding: 12, border: '1px dashed var(--color-border)', borderRadius: 8, display: 'flex', flexDirection: 'column', gap: 10 }}>
      <SampleEditor screen={screen} sample={sample} onChange={(s) => { setSample(s); setItems(null); }} />
      <div><Button size="sm" variant="secondary" onClick={refresh} disabled={previewScreen.isPending}>{t('postingSettings.runPreview')}</Button></div>
      {items?.map((item) => (
        <div key={item.templateId} style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
            <strong style={{ fontSize: 13.5 }}>{item.executionOrder}. {item.nameAr}</strong>
            {!item.isActive && <Badge label={t('postingSettings.inactive')} tone="neutral" />}
            {item.preview.triggerMatched
              ? <Badge label={t('postingSettings.runsForSample')} tone="success" />
              : <Badge label={t('postingSettings.skippedForSample')} tone="neutral" />}
            <span style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>{item.preview.triggerDescription}</span>
          </div>
          {item.preview.triggerMatched && <PreviewTable preview={item.preview} />}
        </div>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------- editor

const emptyLine = (lineNumber: number, direction: 'Debit' | 'Credit'): TemplateLineInput => ({
  lineNumber, direction, accountSourceType: 'FromCompany', fixedAccountId: null, accountFieldName: null, accountResolverKey: null,
  amountFormulaType: 'DirectField', amountFieldName: null, amountFieldNames: null, amountMultiplier: null, amountPercentage: null,
  conditionType: 'None', conditionFieldName: null, conditionFieldValue: null, lineDescription: null, costCenters: []
});

function TemplateEditor({ screen, templateId, onClose }: { screen: PostingScreen; templateId: number | null; onClose: () => void }) {
  const { t } = useTranslation();
  const run = useRun();
  const canSave = usePermission(templateId == null ? 'add' : 'edit');
  const showToast = useToastStore((s) => s.show);
  const { data: existing, isLoading } = usePostingTemplate(templateId);
  const createTemplate = useCreateTemplate();
  const updateTemplate = useUpdateTemplate();
  const previewMutation = usePreviewTemplate();

  const [definition, setDefinition] = useState<TemplateDefinition>({
    nameAr: '', nameEn: '', description: null, triggerType: 'Always', triggerFieldName: null, triggerFieldValue: null,
    executionOrder: Math.max(0, ...screen.templates.map((x) => x.executionOrder)) + 1,
    lines: [emptyLine(1, 'Debit'), emptyLine(2, 'Credit')]
  });
  const [sample, setSample] = useState<PreviewSample>(() => defaultSample(screen));
  const [preview, setPreview] = useState<Preview | null>(null);

  useEffect(() => {
    if (existing) {
      setDefinition({
        nameAr: existing.nameAr, nameEn: existing.nameEn, description: existing.description, triggerType: existing.triggerType,
        triggerFieldName: existing.triggerFieldName, triggerFieldValue: existing.triggerFieldValue,
        executionOrder: existing.executionOrder, lines: existing.lines
      });
    }
  }, [existing]);

  const patch = (p: Partial<TemplateDefinition>) => { setDefinition((d) => ({ ...d, ...p })); setPreview(null); };
  const updateLine = (index: number, p: Partial<TemplateLineInput>) =>
    patch({ lines: definition.lines.map((l, i) => (i === index ? { ...l, ...p } : l)) });

  const runPreview = () => run(async () =>
    setPreview(await previewMutation.mutateAsync({ screenCode: screen.screenCode, definition, sample })));

  const save = async () => {
    const ok = await run(async () => {
      if (templateId != null) {
        const result = await updateTemplate.mutateAsync({ id: templateId, definition });
        showToast(result.isNewVersion ? t('postingSettings.savedNewVersion', { version: result.versionNumber }) : t('postingSettings.saved'), 'success');
      } else {
        await createTemplate.mutateAsync({ screenCode: screen.screenCode, definition, isActive: false });
        showToast(t('postingSettings.savedInactive'), 'success');
      }
    });
    if (ok) onClose();
  };

  if (isLoading) return <p style={{ marginTop: 16 }}>{t('common.loading')}</p>;

  const triggerFields = screen.fields.filter((f) => f.kind === 'Text' || f.kind === 'Id');
  const triggerField = screen.fields.find((f) => f.name === definition.triggerFieldName);

  return (
    <div style={{ marginTop: 18, paddingTop: 16, borderTop: '1px solid var(--color-border)', display: 'flex', flexDirection: 'column', gap: 14 }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 12, alignItems: 'end' }}>
        <div style={fieldCol}>
          <span style={labelStyle}>{t('postingSettings.templateName')}</span>
          <Input value={definition.nameAr} placeholder={t('postingSettings.templateNameHint')} onChange={(e) => patch({ nameAr: e.target.value })} />
        </div>
        <div style={fieldCol}>
          <span style={labelStyle}>{t('postingSettings.templateNameEn')}</span>
          <Input value={definition.nameEn} dir="ltr" onChange={(e) => patch({ nameEn: e.target.value })} />
        </div>
        <div style={fieldCol}>
          <span style={labelStyle}>{t('postingSettings.executionOrder')}</span>
          <Input type="number" min={1} max={99} value={definition.executionOrder} onChange={(e) => patch({ executionOrder: Number(e.target.value) })} />
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 12, alignItems: 'end' }}>
        <div style={fieldCol}>
          <span style={labelStyle}>{t('postingSettings.runsWhen')}</span>
          <SearchableSelect value={definition.triggerType}
            onChange={(v) => patch({ triggerType: v as TriggerType, triggerFieldName: null, triggerFieldValue: null })}
            options={[
              { value: 'Always', label: t('postingSettings.trigger.Always') },
              ...(screen.canMoveStock ? [{ value: 'HasStockMovement', label: t('postingSettings.trigger.HasStockMovement') }] : []),
              { value: 'FieldCondition', label: t('postingSettings.trigger.FieldCondition') }
            ]} />
        </div>
        {definition.triggerType === 'FieldCondition' && (
          <>
            <div style={fieldCol}>
              <span style={labelStyle}>{t('postingSettings.conditionField')}</span>
              <SearchableSelect value={definition.triggerFieldName ?? ''} placeholder={t('postingSettings.pickField')}
                onChange={(v) => patch({ triggerFieldName: v || null, triggerFieldValue: null })}
                options={triggerFields.map((f) => ({ value: f.name, label: f.labelAr }))} />
            </div>
            <div style={fieldCol}>
              <span style={labelStyle}>{t('postingSettings.conditionValue')}</span>
              {triggerField?.choices?.length ? (
                <SearchableSelect value={definition.triggerFieldValue ?? ''} placeholder={t('postingSettings.pickValue')}
                  onChange={(v) => patch({ triggerFieldValue: v || null })}
                  options={triggerField.choices.map((c) => ({ value: c, label: t(`postingSettings.choice.${c}`, { defaultValue: c }) }))} />
              ) : (
                <Input value={definition.triggerFieldValue ?? ''} onChange={(e) => patch({ triggerFieldValue: e.target.value || null })} />
              )}
            </div>
          </>
        )}
      </div>

      {templateId != null && <p style={muted}>{t('postingSettings.versioningNote')}</p>}

      {(['Debit', 'Credit'] as const).map((direction) => (
        <section key={direction} style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <h4 style={{ margin: 0, fontSize: 14, color: direction === 'Debit' ? 'var(--color-primary)' : 'var(--color-text)' }}>
              {t(`postingSettings.${direction === 'Debit' ? 'debitSide' : 'creditSide'}`)}
            </h4>
            <Button size="sm" variant="ghost"
              onClick={() => patch({ lines: [...definition.lines, emptyLine(Math.max(0, ...definition.lines.map((l) => l.lineNumber)) + 1, direction)] })}>
              {t('postingSettings.addLine')}
            </Button>
          </div>
          {definition.lines.map((line, index) => line.direction !== direction ? null : (
            <LineEditor
              key={line.lineNumber}
              screen={screen}
              line={line}
              onChange={(p) => updateLine(index, p)}
              onRemove={() => patch({ lines: definition.lines.filter((_, i) => i !== index) })}
            />
          ))}
        </section>
      ))}

      <SampleEditor screen={screen} sample={sample} onChange={(s) => { setSample(s); setPreview(null); }} />

      {preview && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <span style={{ fontSize: 12.5, color: 'var(--color-text-muted)' }}>
            {preview.triggerDescription} — {preview.triggerMatched ? t('postingSettings.runsForSample') : t('postingSettings.skippedForSample')}
          </span>
          <PreviewTable preview={preview} />
        </div>
      )}

      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
        <Button variant="secondary" onClick={runPreview} disabled={previewMutation.isPending}>{t('postingSettings.preview')}</Button>
        {canSave && (
          <Button variant="primary" onClick={save} disabled={createTemplate.isPending || updateTemplate.isPending}>{t('common.save')}</Button>
        )}
        <Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
      </div>
    </div>
  );
}

const MULTI_FIELD_FORMULAS: AmountFormula[] = ['AddFields', 'SubtractFields', 'DivideFields'];

function LineEditor({ screen, line, onChange, onRemove }: {
  screen: PostingScreen; line: TemplateLineInput; onChange: (patch: Partial<TemplateLineInput>) => void; onRemove: () => void;
}) {
  const { t } = useTranslation();
  const { data: accounts } = useAccountsList(true);
  const { data: catalog } = usePostingEngineCatalog();
  const { data: dimensions } = useDimensionsList();
  const { data: links } = useAccountDimensionLinks(line.accountSourceType === 'Fixed' && line.fixedAccountId ? line.fixedAccountId : undefined);

  const amountFields = screen.fields.filter((f) => f.kind === 'Amount');
  const accountFields = screen.fields.filter((f) => f.kind === 'Account');
  const accountResolvers = screen.resolvers.filter((r) => r.kind === 'Account');
  const amountOptions = amountFields.map((f) => ({ value: f.name, label: f.labelAr }));

  const sourceOptions: { value: AccountSource; label: string }[] = [
    { value: 'FromCompany', label: t('postingSettings.source.FromCompany') },
    { value: 'Fixed', label: t('postingSettings.source.Fixed') },
    ...(accountResolvers.length ? [{ value: 'Resolver' as const, label: t('postingSettings.source.Resolver') }] : []),
    ...(accountFields.length ? [{ value: 'FromDocument' as const, label: t('postingSettings.source.FromDocument') }] : []),
    ...(screen.groups.length ? [{ value: 'FromGroup' as const, label: t('postingSettings.source.FromGroup') }] : [])
  ];

  const setSource = (source: AccountSource) => onChange({
    accountSourceType: source, fixedAccountId: null, accountFieldName: null, accountResolverKey: null,
    ...(source === 'FromGroup'
      ? { amountFormulaType: 'GroupItemAmount', amountFieldName: screen.groups[0]?.name ?? null }
      : line.amountFormulaType === 'GroupItemAmount' ? { amountFormulaType: 'DirectField', amountFieldName: null } : {}),
    // A cost center on one fixed account may not be linked to the next one.
    costCenters: []
  });

  const accountValue = () => {
    switch (line.accountSourceType) {
      case 'Fixed':
        return (
          <SearchableSelect value={line.fixedAccountId ?? ''} placeholder={t('postingSettings.pickAccount')}
            onChange={(v) => onChange({ fixedAccountId: v === '' ? null : Number(v), costCenters: [] })}
            options={(accounts ?? []).map((a) => ({ value: a.id, label: `${a.code} — ${a.nameAr}` }))} />
        );
      case 'FromCompany':
        return (
          <SearchableSelect value={line.accountResolverKey ?? ''} placeholder={t('postingSettings.pickRole')}
            onChange={(v) => onChange({ accountResolverKey: v || null })}
            options={(catalog?.companyAccountRoles ?? []).map((r) => ({ value: r, label: t(`accountMappings.role.${r}`, { defaultValue: r }) }))} />
        );
      case 'Resolver':
        return (
          <SearchableSelect value={line.accountResolverKey ?? ''} placeholder={t('postingSettings.pickResolver')}
            onChange={(v) => onChange({ accountResolverKey: v || null })}
            options={accountResolvers.map((r) => ({ value: r.key, label: t(`postingSettings.resolver.${r.key}`, { defaultValue: r.key }) }))} />
        );
      case 'FromDocument':
        return (
          <SearchableSelect value={line.accountFieldName ?? ''} placeholder={t('postingSettings.pickField')}
            onChange={(v) => onChange({ accountFieldName: v || null })}
            options={accountFields.map((f) => ({ value: f.name, label: f.labelAr }))} />
        );
      case 'FromGroup':
        return (
          <SearchableSelect value={line.amountFieldName ?? ''}
            onChange={(v) => onChange({ amountFieldName: v || null })}
            options={screen.groups.map((g) => ({ value: g.name, label: g.labelAr }))} />
        );
    }
  };

  const formulaOptions: { value: AmountFormula; label: string }[] =
    (['DirectField', 'PercentageOf', 'Multiply', 'AddFields', 'SubtractFields', 'DivideFields'] as const)
      .map((f) => ({ value: f, label: t(`postingSettings.formula.${f}`) }));

  const setFormula = (formula: AmountFormula) => onChange({
    amountFormulaType: formula,
    amountFieldName: MULTI_FIELD_FORMULAS.includes(formula) ? null : line.amountFieldName,
    amountFieldNames: MULTI_FIELD_FORMULAS.includes(formula) ? (line.amountFieldNames?.length ? line.amountFieldNames : [amountFields[0]?.name ?? '', amountFields[1]?.name ?? '']) : null,
    amountMultiplier: formula === 'Multiply' ? line.amountMultiplier ?? 1 : null,
    amountPercentage: formula === 'PercentageOf' ? line.amountPercentage ?? 14 : null
  });

  const setFieldAt = (i: number, value: string) => {
    const next = [...(line.amountFieldNames ?? [])];
    next[i] = value;
    onChange({ amountFieldNames: next });
  };

  const amountEditor = () => {
    if (MULTI_FIELD_FORMULAS.includes(line.amountFormulaType)) {
      const names = line.amountFieldNames ?? [];
      const operator = line.amountFormulaType === 'AddFields' ? '+' : line.amountFormulaType === 'SubtractFields' ? '−' : '÷';
      return (
        <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
          {names.map((name, i) => (
            <span key={i} style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
              {i > 0 && <strong>{operator}</strong>}
              <SearchableSelect value={name} onChange={(v) => setFieldAt(i, v)} options={amountOptions} style={{ minWidth: 150 }} />
            </span>
          ))}
          {line.amountFormulaType === 'AddFields' && names.length < 5 && (
            <Button size="sm" variant="ghost" onClick={() => onChange({ amountFieldNames: [...names, amountFields[0]?.name ?? ''] })}>+</Button>
          )}
          {line.amountFormulaType === 'AddFields' && names.length > 2 && (
            <Button size="sm" variant="ghost" onClick={() => onChange({ amountFieldNames: names.slice(0, -1) })}>−</Button>
          )}
        </div>
      );
    }
    return (
      <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
        <SearchableSelect value={line.amountFieldName ?? ''} placeholder={t('postingSettings.pickField')} style={{ flex: 1 }}
          onChange={(v) => onChange({ amountFieldName: v || null })} options={amountOptions} />
        {line.amountFormulaType === 'Multiply' && (
          <>
            <strong>×</strong>
            <Input type="number" step="0.0001" value={line.amountMultiplier ?? ''} style={{ width: 90 }}
              onChange={(e) => onChange({ amountMultiplier: e.target.value === '' ? null : Number(e.target.value) })} />
          </>
        )}
        {line.amountFormulaType === 'PercentageOf' && (
          <>
            <strong>×</strong>
            <Input type="number" step="0.01" value={line.amountPercentage ?? ''} style={{ width: 80 }}
              onChange={(e) => onChange({ amountPercentage: e.target.value === '' ? null : Number(e.target.value) })} />
            <strong>%</strong>
          </>
        )}
      </div>
    );
  };

  const conditionOptions: { value: Condition; label: string }[] = [
    { value: 'None', label: t('postingSettings.condition.None') },
    { value: 'FieldGreaterThanZero', label: t('postingSettings.condition.FieldGreaterThanZero') },
    { value: 'FieldNotNull', label: t('postingSettings.condition.FieldNotNull') },
    { value: 'FieldEquals', label: t('postingSettings.condition.FieldEquals') }
  ];

  // A fixed account only takes the dimensions linked to it; anything else is only known per
  // document, so any active dimension can be offered and IPostingService checks at posting time.
  const dimensionChoices: Dimension[] = (dimensions ?? []).filter((d) => d.isActive
    && (line.accountSourceType !== 'Fixed' || (links ?? []).some((l) => l.costCenterDimensionId === d.id)));

  const updateCostCenter = (index: number, p: Partial<CostCenterInput>) =>
    onChange({ costCenters: line.costCenters.map((c, i) => (i === index ? { ...c, ...p } : c)) });

  return (
    <div style={{ border: '1px solid var(--color-border)', borderRadius: 8, padding: 12, display: 'flex', flexDirection: 'column', gap: 10, background: 'var(--color-surface)' }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))', gap: 10, alignItems: 'end' }}>
        <div style={fieldCol}>
          <span style={labelStyle}>{t('postingSettings.accountFrom')}</span>
          <SearchableSelect value={line.accountSourceType} onChange={(v) => setSource(v as AccountSource)} options={sourceOptions} />
        </div>
        <div style={fieldCol}>
          <span style={labelStyle}>{t('postingSettings.account')}</span>
          {accountValue()}
        </div>
        {line.accountSourceType !== 'FromGroup' && (
          <div style={fieldCol}>
            <span style={labelStyle}>{t('postingSettings.amountFrom')}</span>
            <SearchableSelect value={line.amountFormulaType} onChange={(v) => setFormula(v as AmountFormula)} options={formulaOptions} />
          </div>
        )}
        <div style={fieldCol}>
          <span style={labelStyle}>{t('postingSettings.when')}</span>
          <SearchableSelect value={line.conditionType}
            onChange={(v) => onChange({
              conditionType: v as Condition,
              conditionFieldName: v === 'None' ? null : line.conditionFieldName ?? line.amountFieldName,
              conditionFieldValue: v === 'FieldEquals' ? line.conditionFieldValue : null
            })}
            options={conditionOptions} />
        </div>
        {line.conditionType !== 'None' && (
          <div style={fieldCol}>
            <span style={labelStyle}>{t('postingSettings.conditionField')}</span>
            <SearchableSelect value={line.conditionFieldName ?? ''} onChange={(v) => onChange({ conditionFieldName: v || null })}
              options={screen.fields.map((f) => ({ value: f.name, label: f.labelAr }))} />
          </div>
        )}
        {line.conditionType === 'FieldEquals' && (
          <div style={fieldCol}>
            <span style={labelStyle}>{t('postingSettings.conditionValue')}</span>
            <Input value={line.conditionFieldValue ?? ''} onChange={(e) => onChange({ conditionFieldValue: e.target.value || null })} />
          </div>
        )}
      </div>

      {line.accountSourceType !== 'FromGroup' && (
        <div style={fieldCol}>
          <span style={labelStyle}>{t('postingSettings.amount')}</span>
          {amountEditor()}
        </div>
      )}

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {line.costCenters.map((cc, index) => (
          <CostCenterRow
            key={index}
            screen={screen}
            costCenter={cc}
            dimensions={dimensionChoices}
            allDimensions={dimensions ?? []}
            onChange={(p) => updateCostCenter(index, p)}
            onRemove={() => onChange({ costCenters: line.costCenters.filter((_, i) => i !== index) })}
          />
        ))}
      </div>

      <div style={{ display: 'flex', gap: 8, justifyContent: 'space-between', flexWrap: 'wrap' }}>
        <Button size="sm" variant="ghost" disabled={line.costCenters.length >= 5 || dimensionChoices.length === 0}
          title={dimensionChoices.length === 0 ? t('postingSettings.noDimensionsHint') : undefined}
          onClick={() => onChange({
            costCenters: [...line.costCenters, {
              costCenterDimensionId: dimensionChoices.find((d) => !line.costCenters.some((c) => c.costCenterDimensionId === d.id))?.id ?? dimensionChoices[0].id,
              sourceType: 'Fixed', fixedValueId: null, valueFieldName: null, valueResolverKey: null,
              relatedEntityType: null, relatedEntityField: null, contextKey: null,
              displayOrder: line.costCenters.length + 1
            }]
          })}>
          {t('postingSettings.addCostCenter')}
        </Button>
        <Button size="sm" variant="danger" onClick={onRemove}>{t('postingSettings.removeLine')}</Button>
      </div>
    </div>
  );
}

/**
 * One cost center on a line. The entity-based sources only offer what fits the dimension: a
 * terminal-linked dimension takes a terminal — from the document, from the module, or off the
 * shift — never a branch. The server enforces the same when the template is saved.
 */
function CostCenterRow({ screen, costCenter, dimensions, allDimensions, onChange, onRemove }: {
  screen: PostingScreen;
  costCenter: CostCenterInput;
  dimensions: Dimension[];
  allDimensions: Dimension[];
  onChange: (patch: Partial<CostCenterInput>) => void;
  onRemove: () => void;
}) {
  const { t } = useTranslation();
  const { data: values } = useDimensionValues(costCenter.costCenterDimensionId);
  const dimension = allDimensions.find((d) => d.id === costCenter.costCenterDimensionId);
  const linked = dimension?.linkedEntityType ?? 'None';

  const documentFields = screen.fields.filter((f) => f.kind === 'Id' && !f.isContext && f.entityType === linked);
  const contextFields = screen.fields.filter((f) => f.kind === 'Id' && f.isContext && f.entityType === linked);
  const related = screen.relatedEntities
    .map((r) => ({ ...r, fields: r.fields.filter((f) => f.entityType === linked) }))
    .filter((r) => r.fields.length > 0);
  const branchResolver = linked === 'Branch' && screen.resolvers.some((r) => r.key === 'Branch.ToCostCenterValue');

  const sources: { value: CostCenterSource; label: string }[] = [
    { value: 'Fixed', label: t('postingSettings.ccSource.Fixed') },
    ...(documentFields.length ? [{ value: 'FromDocument' as const, label: t('postingSettings.ccSource.FromDocument') }] : []),
    ...(contextFields.length ? [{ value: 'FromContext' as const, label: t('postingSettings.ccSource.FromContext') }] : []),
    ...(related.length ? [{ value: 'FromRelatedEntity' as const, label: t('postingSettings.ccSource.FromRelatedEntity') }] : []),
    ...(branchResolver ? [{ value: 'Dynamic' as const, label: t('postingSettings.ccSource.Branch') }] : [])
  ];

  const reset = { fixedValueId: null, valueFieldName: null, valueResolverKey: null, relatedEntityType: null, relatedEntityField: null, contextKey: null };

  const setSource = (source: CostCenterSource) => onChange({
    ...reset,
    sourceType: source,
    valueFieldName: source === 'FromDocument' ? documentFields[0]?.name ?? null : null,
    contextKey: source === 'FromContext' ? contextFields[0]?.name ?? null : null,
    relatedEntityType: source === 'FromRelatedEntity' ? related[0]?.name ?? null : null,
    relatedEntityField: source === 'FromRelatedEntity' ? related[0]?.fields[0]?.name ?? null : null,
    valueResolverKey: source === 'Dynamic' ? 'Branch.ToCostCenterValue' : null
  });

  const relatedEntity = related.find((r) => r.name === costCenter.relatedEntityType);

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr)) auto', gap: 8, alignItems: 'end', paddingInlineStart: 10, borderInlineStart: '3px solid var(--color-border)' }}>
      <div style={fieldCol}>
        <span style={labelStyle}>{t('postingSettings.dimension')}</span>
        <SearchableSelect value={costCenter.costCenterDimensionId}
          onChange={(v) => onChange({ ...reset, costCenterDimensionId: Number(v), sourceType: 'Fixed' })}
          options={(dimensions.some((d) => d.id === costCenter.costCenterDimensionId) ? dimensions : [...dimensions, ...(dimension ? [dimension] : [])])
            .map((d) => ({ value: d.id, label: d.nameAr }))} />
      </div>
      <div style={fieldCol}>
        <span style={labelStyle}>{t('postingSettings.ccValueFrom')}</span>
        <SearchableSelect value={costCenter.sourceType} onChange={(v) => setSource(v as CostCenterSource)} options={sources} />
      </div>
      <div style={fieldCol}>
        <span style={labelStyle}>{t('postingSettings.ccValue')}</span>
        {costCenter.sourceType === 'Fixed' && (
          <SearchableSelect value={costCenter.fixedValueId ?? ''} placeholder={t('postingSettings.pickValue')}
            onChange={(v) => onChange({ fixedValueId: v === '' ? null : Number(v) })}
            options={(values ?? []).filter((x) => x.isActive).map((x) => ({ value: x.id, label: `${x.code} — ${x.nameAr}` }))} />
        )}
        {costCenter.sourceType === 'FromDocument' && (
          <SearchableSelect value={costCenter.valueFieldName ?? ''} onChange={(v) => onChange({ valueFieldName: v || null })}
            options={documentFields.map((f) => ({ value: f.name, label: f.labelAr }))} />
        )}
        {costCenter.sourceType === 'FromContext' && (
          <SearchableSelect value={costCenter.contextKey ?? ''} onChange={(v) => onChange({ contextKey: v || null })}
            options={contextFields.map((f) => ({ value: f.name, label: f.labelAr }))} />
        )}
        {costCenter.sourceType === 'FromRelatedEntity' && (
          <div style={{ display: 'flex', gap: 6 }}>
            <SearchableSelect value={costCenter.relatedEntityType ?? ''} style={{ flex: 1 }}
              onChange={(v) => {
                const entity = related.find((r) => r.name === v);
                onChange({ relatedEntityType: v || null, relatedEntityField: entity?.fields[0]?.name ?? null });
              }}
              options={related.map((r) => ({ value: r.name, label: r.labelAr }))} />
            <SearchableSelect value={costCenter.relatedEntityField ?? ''} style={{ flex: 1 }}
              onChange={(v) => onChange({ relatedEntityField: v || null })}
              options={(relatedEntity?.fields ?? []).map((f) => ({ value: f.name, label: f.labelAr }))} />
          </div>
        )}
        {costCenter.sourceType === 'Dynamic' && (
          <div style={{ fontSize: 13, padding: '9px 0', color: 'var(--color-text-muted)' }}>{t('postingSettings.ccBranchAuto')}</div>
        )}
      </div>
      <Button size="sm" variant="ghost" onClick={onRemove} aria-label={t('postingSettings.removeCostCenter')}>✕</Button>
    </div>
  );
}

function PreviewTable({ preview }: { preview: Preview }) {
  const { t } = useTranslation();
  const cell: CSSProperties = { padding: '7px 10px', borderBottom: '1px solid var(--color-border)', fontSize: 13, verticalAlign: 'top' };
  const num: CSSProperties = { ...cell, textAlign: 'end', fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div style={{ overflowX: 'auto', border: '1px solid var(--color-border)', borderRadius: 8 }}>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ background: 'var(--color-surface-2)' }}>
              <th style={{ ...cell, textAlign: 'start' }}>{t('postingSettings.account')}</th>
              <th style={{ ...cell, textAlign: 'start' }}>{t('postingSettings.costCenters')}</th>
              <th style={num}>{t('postingSettings.debit')}</th>
              <th style={num}>{t('postingSettings.credit')}</th>
            </tr>
          </thead>
          <tbody>
            {preview.lines.map((l) => (
              <tr key={l.lineNumber} style={{ opacity: l.skipped ? 0.5 : 1 }}>
                <td style={cell}>
                  {l.skipped ? l.note : l.accountDisplay}
                  {!l.accountKnownNow && !l.skipped && (
                    <div style={{ fontSize: 11.5, color: 'var(--color-text-muted)' }}>{t('postingSettings.decidedPerDocument')}</div>
                  )}
                  {l.amountFormula && <div style={{ fontSize: 11.5, color: 'var(--color-text-muted)' }} dir="auto">{l.amountFormula}</div>}
                </td>
                <td style={{ ...cell, fontSize: 12 }}>{l.costCenters.join(' · ') || '—'}</td>
                <td style={num}>{!l.skipped && l.direction === 'Debit' ? l.amount.toFixed(2) : ''}</td>
                <td style={num}>{!l.skipped && l.direction === 'Credit' ? l.amount.toFixed(2) : ''}</td>
              </tr>
            ))}
            <tr style={{ fontWeight: 700 }}>
              <td style={cell} colSpan={2}>
                {preview.isBalanced ? <Badge label={t('postingSettings.balanced')} tone="success" /> : <Badge label={t('postingSettings.unbalanced')} tone="error" />}
              </td>
              <td style={num}>{preview.totalDebit.toFixed(2)}</td>
              <td style={num}>{preview.totalCredit.toFixed(2)}</td>
            </tr>
          </tbody>
        </table>
      </div>
      {preview.problems.length > 0 && (
        <ul style={{ margin: 0, paddingInlineStart: 18, fontSize: 13, color: 'var(--color-danger, #B3261E)', lineHeight: 1.8 }}>
          {preview.problems.map((p) => <li key={p}>{p}</li>)}
        </ul>
      )}
    </div>
  );
}
