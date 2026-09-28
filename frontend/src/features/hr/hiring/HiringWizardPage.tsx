import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { EmployeeStep } from './steps/EmployeeStep';
import { PersonalDataStep } from './steps/PersonalDataStep';
import { ContractStep } from './steps/ContractStep';
import { DocumentsStep } from './steps/DocumentsStep';
import { UserStep } from './steps/UserStep';

const STEP_IDS = ['employee', 'personalData', 'contract', 'documents', 'user'] as const;
type StepId = (typeof STEP_IDS)[number];

/**
 * /hr/hiring/new — معالج التعيين (Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch
 * 1.5.5). أول Wizard في المشروع (قرار 1.5.1: مفيش Component عام له — الـ Consumer واحد بس).
 *
 * **تصميم متعمّد: للأمام بس، مفيش رجوع** — كل خطوة "التالي" فيها بتعمل نداء API حقيقي (إنشاء
 * فعلي، مش مسودة محلية)، فـ"رجوع" هيحتاج Undo/Delete مش مجرد تغيير شاشة. البديل الأبسط والأصدق:
 * بعد أول خطوة (إنشاء الموظف)، رابط "فتح شاشة الموظف الكاملة" بيظهر دايمًا — أي تصحيح بعد كده
 * يحصل من هناك (شاشة الموظف الكاملة، 1.5.3، بكل تبويباتها)، مش من الـ Wizard نفسه.
 */
export function HiringWizardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [stepIndex, setStepIndex] = useState(0);
  const [employeeId, setEmployeeId] = useState<number | undefined>();
  const currentStep: StepId = STEP_IDS[stepIndex];

  const advance = () => setStepIndex((i) => Math.min(i + 1, STEP_IDS.length - 1));

  const stepLabels: Record<StepId, string> = {
    employee: t('hr.hiring.stepEmployee'),
    personalData: t('hr.hiring.stepPersonalData'),
    contract: t('hr.hiring.stepContract'),
    documents: t('hr.hiring.stepDocuments'),
    user: t('hr.hiring.stepUser')
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 900 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <h2 style={{ margin: 0 }}>{t('hr.hiring.title')}</h2>
        {employeeId && (
          <a
            href={`/hr/employees/${employeeId}`}
            onClick={(e) => { e.preventDefault(); navigate(`/hr/employees/${employeeId}`); }}
            style={{ fontSize: 13, color: 'var(--color-gold-600, var(--color-gold-500))' }}
          >
            {t('hr.hiring.openFullScreen')}
          </a>
        )}
      </div>

      <div style={{ display: 'flex', gap: 4 }}>
        {STEP_IDS.map((id, i) => (
          <div
            key={id}
            style={{
              flex: 1,
              textAlign: 'center',
              padding: '8px 4px',
              fontSize: 12,
              fontWeight: i === stepIndex ? 700 : 500,
              color: i <= stepIndex ? 'var(--color-text)' : 'var(--color-text-muted)',
              borderBottom: i <= stepIndex ? '2px solid var(--color-gold-500)' : '2px solid var(--color-border)'
            }}
          >
            {i + 1}. {stepLabels[id]}
          </div>
        ))}
      </div>

      {currentStep === 'employee' && (
        <EmployeeStep onComplete={(id) => { setEmployeeId(id); advance(); }} />
      )}
      {currentStep === 'personalData' && employeeId && (
        <PersonalDataStep employeeId={employeeId} onComplete={advance} />
      )}
      {currentStep === 'contract' && employeeId && (
        <ContractStep employeeId={employeeId} onComplete={advance} />
      )}
      {currentStep === 'documents' && employeeId && (
        <DocumentsStep employeeId={employeeId} onComplete={advance} />
      )}
      {currentStep === 'user' && employeeId && (
        <UserStep employeeId={employeeId} onFinish={() => navigate(`/hr/employees/${employeeId}`)} />
      )}
    </div>
  );
}
