import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api } from '../../../app/api';

export type CodeFormat = 'NumbersOnly' | 'LettersOnly' | 'LettersAndNumbers';

export interface CodingRule {
  screenCode: string;
  screenLabel: string;
  isAutomatic: boolean;
  format: CodeFormat;
  prefix: string | null;
  sequenceLength: number;
  isAttachmentMandatory: boolean;
  isDescriptionMandatory: boolean;
}

export interface UpsertCodingRulePayload {
  isAutomatic: boolean;
  format: CodeFormat;
  prefix: string | null;
  sequenceLength: number;
  isAttachmentMandatory: boolean;
  isDescriptionMandatory: boolean;
}

/** GET /api/v1/settings/coding-rules — every code-bearing screen (00-System-Wide-Corrections-01.md-adjacent request). */
export function useCodingRules() {
  const { i18n } = useTranslation();
  return useQuery({
    queryKey: ['coding-rules', i18n.language],
    queryFn: async () => (await api.get<CodingRule[]>('/settings/coding-rules', { params: { lang: i18n.language } })).data
  });
}

/** GET /api/v1/settings/coding-rules/{screenCode} — used by a single create-form to decide
 * whether to show a manual Code input. */
export function useCodingRule(screenCode: string) {
  const { i18n } = useTranslation();
  return useQuery({
    queryKey: ['coding-rule', screenCode, i18n.language],
    queryFn: async () => (await api.get<CodingRule>(`/settings/coding-rules/${screenCode}`, { params: { lang: i18n.language } })).data,
    staleTime: 60 * 1000
  });
}

export function useUpsertCodingRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ screenCode, ...payload }: UpsertCodingRulePayload & { screenCode: string }) =>
      api.put(`/settings/coding-rules/${screenCode}`, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['coding-rules'] });
      queryClient.invalidateQueries({ queryKey: ['coding-rule'] });
    }
  });
}
