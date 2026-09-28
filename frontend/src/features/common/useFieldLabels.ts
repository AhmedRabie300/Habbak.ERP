import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api } from '../../app/api';

/**
 * GET /api/v1/field-labels?screenCode=... — DataGrid column headers and form field labels driven
 * from the database (00-System-Wide-Corrections-01.md, section 4). Returns a `label(fieldCode,
 * fallback)` helper so a screen keeps working (via its i18n fallback) while the request is in
 * flight or a given field has no override row yet.
 */
export function useFieldLabels(screenCode: string) {
  const { i18n } = useTranslation();
  const { data } = useQuery({
    queryKey: ['field-labels', screenCode, i18n.language],
    queryFn: async () =>
      (await api.get<Record<string, string>>('/field-labels', { params: { screenCode, lang: i18n.language } })).data,
    staleTime: 5 * 60 * 1000
  });

  const label = (fieldCode: string, fallback: string) => data?.[fieldCode] ?? fallback;

  return { labels: data, label };
}
