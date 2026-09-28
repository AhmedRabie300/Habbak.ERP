import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardHeader, CardBody } from '../../../ui-kit/Card';
import { Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useCodingRules, useUpsertCodingRule, type CodeFormat, type CodingRule } from './api';
import { usePostingScreens } from '../../accounting/postingTemplates/api';
import { NotWiredPostingPanel, PostingSettingsPanel } from '../../accounting/postingTemplates/PostingSettingsPanel';

/** These screens' number generators always call ResolveCodeAsync with manualCode: null
 * (JournalEntryNumberGenerator/VoucherNumberGenerator) — there is no manual-code input anywhere
 * for them, so switching them to manual here would make creation fail outright. */
const ALWAYS_AUTOMATIC_SCREENS = new Set([
  'ACCOUNTING_JOURNAL_ENTRIES',
  'ACCOUNTING_RECEIPT_VOUCHERS',
  'ACCOUNTING_PAYMENT_VOUCHERS'
]);

/** /settings/coding-rules — My Remarks/Remarks2.md, remark 4.1: renamed from a single-purpose
 * "قواعد الترقيم" screen into a central "إعدادات الشاشات" screen — pick one screen from the
 * dropdown, then configure everything for it (coding rule + the mandatory-field toggles below) in
 * one place. A screen that posts journal entries also gets its posting settings here (the posting
 * engine's screen codes match these); the few that post but have no numbering of their own (shift
 * variance, drawer expenses, waste) are listed for their posting settings alone. */
export function CodingRulesSettingsPage() {
  const { t } = useTranslation();
  const { data: rules, isLoading } = useCodingRules();
  const { data: postingScreens } = usePostingScreens();
  const [screenCode, setScreenCode] = useState('');

  const selectedRule = rules?.find((r) => r.screenCode === screenCode);
  const selectedPostingScreen = postingScreens?.find((s) => s.screenCode === screenCode);

  const options = useMemo(() => {
    const codes = new Set(rules?.map((r) => r.screenCode) ?? []);
    const postingCodes = new Set(postingScreens?.filter((s) => s.isPosting).map((s) => s.screenCode) ?? []);
    return [
      ...(rules?.map((r) => ({ value: r.screenCode, label: postingCodes.has(r.screenCode) ? `${r.screenLabel} · ${t('postingSettings.postsTag')}` : r.screenLabel })) ?? []),
      ...(postingScreens?.filter((s) => !codes.has(s.screenCode)).map((s) => ({ value: s.screenCode, label: s.isPosting ? `${s.nameAr} · ${t('postingSettings.postsTag')}` : s.nameAr })) ?? [])
    ];
  }, [rules, postingScreens, t]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('codingRules.title')}</h2>
      <p style={{ fontSize: 12, color: 'var(--color-text-muted)', margin: 0 }}>{t('codingRules.hint')}</p>

      <SearchableSelect
        value={screenCode}
        onChange={setScreenCode}
        options={[
          { value: '', label: t('codingRules.selectScreen') },
          ...options
        ]}
        style={{ maxWidth: 320 }}
      />

      {isLoading && <p>{t('common.loading')}</p>}

      {!isLoading && screenCode && !selectedRule && !selectedPostingScreen && (
        <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>
      )}

      {selectedRule && <ScreenSettingsPanel key={`coding-${selectedRule.screenCode}`} rule={selectedRule} />}
      {selectedPostingScreen && <PostingSettingsPanel key={`posting-${selectedPostingScreen.screenCode}`} screen={selectedPostingScreen} />}
      {selectedRule && postingScreens && !selectedPostingScreen && <NotWiredPostingPanel />}
    </div>
  );
}

function ScreenSettingsPanel({ rule }: { rule: CodingRule }) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const upsert = useUpsertCodingRule();

  const forcedAutomatic = ALWAYS_AUTOMATIC_SCREENS.has(rule.screenCode);
  const [isAutomatic, setIsAutomatic] = useState(rule.isAutomatic);
  const [format, setFormat] = useState<CodeFormat>(rule.format);
  const [prefix, setPrefix] = useState(rule.prefix ?? '');
  const [sequenceLength, setSequenceLength] = useState(rule.sequenceLength);
  const [isAttachmentMandatory, setIsAttachmentMandatory] = useState(rule.isAttachmentMandatory);
  const [isDescriptionMandatory, setIsDescriptionMandatory] = useState(rule.isDescriptionMandatory);

  useEffect(() => {
    setIsAutomatic(rule.isAutomatic);
    setFormat(rule.format);
    setPrefix(rule.prefix ?? '');
    setSequenceLength(rule.sequenceLength);
    setIsAttachmentMandatory(rule.isAttachmentMandatory);
    setIsDescriptionMandatory(rule.isDescriptionMandatory);
  }, [rule]);

  const handleSave = async () => {
    try {
      await upsert.mutateAsync({
        screenCode: rule.screenCode,
        isAutomatic,
        format,
        prefix: format === 'NumbersOnly' ? null : prefix || null,
        sequenceLength,
        isAttachmentMandatory,
        isDescriptionMandatory
      });
      showToast(t('codingRules.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const previewCode = () => {
    const padded = '1'.padStart(sequenceLength, '0');
    if (format === 'NumbersOnly') return padded;
    if (format === 'LettersOnly') return `${prefix || '—'}A`;
    return prefix ? `${prefix}-${padded}` : padded;
  };

  return (
    <Card>
      <CardHeader>{rule.screenLabel}</CardHeader>
      <CardBody>
        <h4 style={{ margin: '0 0 10px', fontSize: 13.5 }}>{t('codingRules.title')}</h4>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, fontWeight: 600, marginBottom: 12 }} title={forcedAutomatic ? t('codingRules.forcedAutomaticHint') : undefined}>
          <input type="checkbox" checked={isAutomatic} disabled={forcedAutomatic} onChange={(e) => setIsAutomatic(e.target.checked)} />
          {t('codingRules.automatic')}
        </label>

        {isAutomatic ? (
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 5, fontSize: 12.5 }}>
              <label style={{ color: 'var(--color-text-muted)', fontWeight: 600 }}>{t('codingRules.format')}</label>
              <SearchableSelect
                value={format}
                onChange={(v) => setFormat(v as CodeFormat)}
                options={[
                  { value: 'LettersAndNumbers', label: t('codingRules.formatLettersAndNumbers') },
                  { value: 'NumbersOnly', label: t('codingRules.formatNumbersOnly') },
                  { value: 'LettersOnly', label: t('codingRules.formatLettersOnly') }
                ]}
                style={{ minWidth: 200 }}
              />
            </div>

            {format !== 'NumbersOnly' && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 5, fontSize: 12.5 }}>
                <label style={{ color: 'var(--color-text-muted)', fontWeight: 600 }}>{t('codingRules.prefix')}</label>
                <Input value={prefix} onChange={(e) => setPrefix(e.target.value.toUpperCase())} style={{ width: 120 }} maxLength={20} />
              </div>
            )}

            {format !== 'LettersOnly' && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 5, fontSize: 12.5 }}>
                <label style={{ color: 'var(--color-text-muted)', fontWeight: 600 }}>{t('codingRules.sequenceLength')}</label>
                <Input
                  type="number"
                  min={1}
                  max={10}
                  value={sequenceLength}
                  onChange={(e) => setSequenceLength(Number(e.target.value))}
                  style={{ width: 90 }}
                />
              </div>
            )}

            <div style={{ fontSize: 12.5, color: 'var(--color-text-muted)' }}>
              {t('codingRules.preview')}: <strong style={{ color: 'var(--color-text)' }}>{previewCode()}</strong>
            </div>
          </div>
        ) : (
          <p style={{ fontSize: 12.5, color: 'var(--color-text-muted)', margin: 0 }}>{t('codingRules.manualHint')}</p>
        )}

        <div style={{ height: 1, background: 'var(--color-border)', margin: '16px 0' }} />

        <h4 style={{ margin: '0 0 10px', fontSize: 13.5 }}>{t('codingRules.otherSettings')}</h4>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13 }}>
            <input type="checkbox" checked={isAttachmentMandatory} onChange={(e) => setIsAttachmentMandatory(e.target.checked)} />
            {t('codingRules.attachmentMandatory')}
          </label>
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13 }}>
            <input type="checkbox" checked={isDescriptionMandatory} onChange={(e) => setIsDescriptionMandatory(e.target.checked)} />
            {t('codingRules.descriptionMandatory')}
          </label>
        </div>

        <div style={{ marginTop: 16 }}>
          <ActionBar primary={{ key: 'save', label: t('common.save'), onClick: handleSave }} />
        </div>
      </CardBody>
    </Card>
  );
}
