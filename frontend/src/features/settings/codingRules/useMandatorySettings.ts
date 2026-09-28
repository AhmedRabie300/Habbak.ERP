import { useTranslation } from 'react-i18next';
import { useCodingRule } from './api';
import { useAttachmentsList } from '../../common/attachments/api';
import { useToastStore } from '../../../store/toastStore';

/** My Remarks/Remarks2.md, remark 4.1 — reads the per-screen "Attachments mandatory"/"Description
 * mandatory" toggles set from /settings/coding-rules and turns them into the two checks every
 * wired Edit page needs: block the state-transition action (Submit/Post/Confirm/...) when
 * mandatory attachments are missing, and block Save when the mandatory description is empty.
 * Centralized here since it's the exact same two checks on every screen that wires it. */
export function useMandatorySettings(screenCode: string, entityType: string, entityId: number | undefined) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: settings } = useCodingRule(screenCode);
  const { data: attachments } = useAttachmentsList(entityType, entityId);

  const checkDescriptionMandatory = (description: string | undefined | null): boolean => {
    if (settings?.isDescriptionMandatory && !description?.trim()) {
      showToast(t('codingRules.descriptionRequiredError'), 'error');
      return false;
    }
    return true;
  };

  const checkAttachmentMandatory = (): boolean => {
    if (settings?.isAttachmentMandatory && (attachments?.length ?? 0) === 0) {
      showToast(t('codingRules.attachmentRequiredError'), 'error');
      return false;
    }
    return true;
  };

  return { checkDescriptionMandatory, checkAttachmentMandatory };
}
