import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useEmployeesLookup } from '../employees/api';
import { useOrgUnits, type OrgUnit } from '../orgUnits/api';
import { useWorkShiftDefinitions } from '../workShifts/api';
import { usePreviewBulkShiftSchedules, useGenerateBulkShiftSchedules, type BulkScheduleException, type BulkSchedulePreviewLine } from './api';

const WEEKDAYS: { day: number; key: string }[] = [
  { day: 0, key: 'Sunday' }, { day: 1, key: 'Monday' }, { day: 2, key: 'Tuesday' }, { day: 3, key: 'Wednesday' },
  { day: 4, key: 'Thursday' }, { day: 5, key: 'Friday' }, { day: 6, key: 'Saturday' }
];

/** بناء خيارات OrgUnit مسطّحة بمسافات بادئة حسب العمق (بدل شجرة كاملة — تبسيط متعمَّد). */
function useIndentedOrgUnitOptions(units: OrgUnit[] | undefined) {
  return useMemo(() => {
    if (!units) return [];
    const byParent = new Map<number | null, OrgUnit[]>();
    for (const u of units) {
      const list = byParent.get(u.parentId) ?? [];
      list.push(u);
      byParent.set(u.parentId, list);
    }
    const options: { value: number; label: string }[] = [];
    const walk = (parentId: number | null, depth: number) => {
      for (const u of byParent.get(parentId) ?? []) {
        options.push({ value: u.id, label: `${'— '.repeat(depth)}${u.nameAr}` });
        walk(u.id, depth + 1);
      }
    };
    walk(null, 0);
    return options;
  }, [units]);
}

function maskToDays(mask: number): number[] {
  return WEEKDAYS.filter((d) => (mask & (1 << d.day)) !== 0).map((d) => d.day);
}

function daysToMask(days: number[]): number {
  return days.reduce((acc, d) => acc | (1 << d), 0);
}

function WeekdayCheckboxes({ mask, onChange }: { mask: number; onChange: (mask: number) => void }) {
  const { t } = useTranslation();
  const selected = maskToDays(mask);
  return (
    <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
      {WEEKDAYS.map((d) => (
        <label key={d.day} style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 13 }}>
          <input
            type="checkbox"
            checked={selected.includes(d.day)}
            onChange={(e) => {
              const next = e.target.checked ? [...selected, d.day] : selected.filter((x) => x !== d.day);
              onChange(daysToMask(next));
            }}
          />
          {t(`hr.attendance.shiftScheduleGenerator.weekdays.${d.key}`)}
        </label>
      ))}
    </div>
  );
}

interface ExceptionRow {
  employeeId: number | '';
  workShiftDefinitionId: number | '';
  weeklyRestDaysMask: number;
}

/** /hr/shift-schedule-generator — screen HR_SHIFT_SCHEDULE_GENERATOR (Remarks8 Item 7). */
export function BulkShiftScheduleGeneratorPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: orgUnits } = useOrgUnits();
  const orgUnitOptions = useIndentedOrgUnitOptions(orgUnits);
  const { data: employees } = useEmployeesLookup();
  const { data: workShifts } = useWorkShiftDefinitions();

  const [orgUnitId, setOrgUnitId] = useState<number | ''>('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [workShiftDefinitionId, setWorkShiftDefinitionId] = useState<number | ''>('');
  const [weeklyRestDaysMask, setWeeklyRestDaysMask] = useState(1 << 5); // الجمعة افتراضيًا
  const [exceptions, setExceptions] = useState<ExceptionRow[]>([]);
  const [preview, setPreview] = useState<BulkSchedulePreviewLine[] | null>(null);

  const previewMutation = usePreviewBulkShiftSchedules();
  const generateMutation = useGenerateBulkShiftSchedules();

  const buildExceptions = (): BulkScheduleException[] | null => {
    const valid = exceptions.filter((e) => e.employeeId !== '');
    if (valid.length === 0) return null;
    return valid.map((e) => ({
      employeeId: Number(e.employeeId),
      workShiftDefinitionId: e.workShiftDefinitionId === '' ? null : Number(e.workShiftDefinitionId),
      weeklyRestDaysMask: e.weeklyRestDaysMask
    }));
  };

  const canRun = orgUnitId !== '' && !!fromDate && !!toDate;

  const handlePreview = async () => {
    if (!canRun) return;
    try {
      const result = await previewMutation.mutateAsync({
        orgUnitId: Number(orgUnitId), fromDate, toDate,
        workShiftDefinitionId: workShiftDefinitionId === '' ? null : Number(workShiftDefinitionId),
        weeklyRestDaysMask, exceptions: buildExceptions()
      });
      setPreview(result);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleGenerate = async () => {
    if (!canRun) return;
    try {
      const result = await generateMutation.mutateAsync({
        orgUnitId: Number(orgUnitId), fromDate, toDate,
        workShiftDefinitionId: workShiftDefinitionId === '' ? null : Number(workShiftDefinitionId),
        weeklyRestDaysMask, exceptions: buildExceptions()
      });
      showToast(t('hr.attendance.shiftScheduleGenerator.generateSuccess', { employees: result.employeesProcessed, days: result.daysCreated }), 'success');
      setPreview(null);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const employeeOptions = useMemo(() => (employees ?? []).map((e) => ({ value: e.id, label: `${e.code} — ${e.nameAr}` })), [employees]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 900 }}>
      <h2 style={{ margin: 0 }}>{t('hr.attendance.shiftScheduleGenerator.title')}</h2>

      <ActionBar
        primary={{ key: 'generate', label: t('hr.attendance.shiftScheduleGenerator.generate'), onClick: handleGenerate }}
        secondary={[
          { key: 'preview', label: t('hr.attendance.shiftScheduleGenerator.preview'), onClick: handlePreview },
          { key: 'back', label: t('common.back'), onClick: () => navigate('/hr/shift-schedules') }
        ]}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.attendance.shiftScheduleGenerator.orgUnit')}>
              <SearchableSelect value={orgUnitId} onChange={(v) => setOrgUnitId(v === '' ? '' : Number(v))} options={orgUnitOptions} style={{ minWidth: 260 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.shiftScheduleGenerator.fromDate')}>
              <Input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.shiftScheduleGenerator.toDate')}>
              <Input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} min={fromDate || undefined} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.shiftScheduleGenerator.workShift')}>
              <SearchableSelect
                value={workShiftDefinitionId}
                onChange={(v) => setWorkShiftDefinitionId(v === '' ? '' : Number(v))}
                options={[{ value: '', label: t('hr.attendance.shiftSchedules.noWorkShift') }, ...(workShifts ?? []).map((w) => ({ value: w.id, label: w.nameAr }))]}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
          </div>

          <div style={{ marginTop: 16 }}>
            <FieldWrapper label={t('hr.attendance.shiftScheduleGenerator.weeklyRestDays')}>
              <WeekdayCheckboxes mask={weeklyRestDaysMask} onChange={setWeeklyRestDaysMask} />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
            <strong>{t('hr.attendance.shiftScheduleGenerator.exceptions')}</strong>
            <Button variant="secondary" onClick={() => setExceptions((prev) => [...prev, { employeeId: '', workShiftDefinitionId: '', weeklyRestDaysMask }])}>
              {t('hr.attendance.shiftScheduleGenerator.addException')}
            </Button>
          </div>

          {exceptions.length === 0 && <div style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('hr.attendance.shiftScheduleGenerator.noExceptions')}</div>}

          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {exceptions.map((row, index) => (
              <div key={index} style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'end', borderTop: '1px solid var(--color-border)', paddingTop: 10 }}>
                <FieldWrapper label={t('hr.attendance.shiftScheduleGenerator.exceptionEmployee')}>
                  <SearchableSelect
                    value={row.employeeId}
                    onChange={(v) => setExceptions((prev) => prev.map((r, i) => (i === index ? { ...r, employeeId: v === '' ? '' : Number(v) } : r)))}
                    options={employeeOptions}
                    style={{ minWidth: 220 }}
                  />
                </FieldWrapper>
                <FieldWrapper label={t('hr.attendance.shiftScheduleGenerator.workShift')}>
                  <SearchableSelect
                    value={row.workShiftDefinitionId}
                    onChange={(v) => setExceptions((prev) => prev.map((r, i) => (i === index ? { ...r, workShiftDefinitionId: v === '' ? '' : Number(v) } : r)))}
                    options={[{ value: '', label: t('hr.attendance.shiftSchedules.noWorkShift') }, ...(workShifts ?? []).map((w) => ({ value: w.id, label: w.nameAr }))]}
                    style={{ minWidth: 180 }}
                  />
                </FieldWrapper>
                <WeekdayCheckboxes
                  mask={row.weeklyRestDaysMask}
                  onChange={(mask) => setExceptions((prev) => prev.map((r, i) => (i === index ? { ...r, weeklyRestDaysMask: mask } : r)))}
                />
                <Button variant="secondary" onClick={() => setExceptions((prev) => prev.filter((_, i) => i !== index))}>
                  {t('common.remove')}
                </Button>
              </div>
            ))}
          </div>
        </CardBody>
      </Card>

      {preview && (
        <Card>
          <CardBody>
            <h3 style={{ marginTop: 0 }}>{t('hr.attendance.shiftScheduleGenerator.previewTitle')}</h3>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
              <thead>
                <tr>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.shiftScheduleGenerator.exceptionEmployee')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.shiftScheduleGenerator.workingDays')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.shiftScheduleGenerator.restDays')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.shiftScheduleGenerator.alreadyScheduled')}</th>
                </tr>
              </thead>
              <tbody>
                {preview.map((row) => (
                  <tr key={row.employeeId} style={{ borderTop: '1px solid var(--color-border)' }}>
                    <td style={{ padding: 6 }}>{row.employeeCode} — {row.employeeNameAr}</td>
                    <td style={{ padding: 6 }}>{row.workingDays}</td>
                    <td style={{ padding: 6 }}>{row.restDays}</td>
                    <td style={{ padding: 6 }}>{row.alreadyScheduledDays}</td>
                  </tr>
                ))}
                {preview.length === 0 && (
                  <tr><td colSpan={4} style={{ padding: 6, color: 'var(--color-text-muted)' }}>{t('hr.attendance.shiftScheduleGenerator.noEmployees')}</td></tr>
                )}
              </tbody>
            </table>
          </CardBody>
        </Card>
      )}
    </div>
  );
}
