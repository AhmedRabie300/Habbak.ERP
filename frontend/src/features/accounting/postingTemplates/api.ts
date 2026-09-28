import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

const BASE = '/accounting/posting-templates';

export type Direction = 'Debit' | 'Credit';
export type AccountSource = 'Fixed' | 'FromDocument' | 'FromCompany' | 'Resolver' | 'FromGroup';
export type AmountFormula =
  | 'DirectField'
  | 'SumLineQuantityTimesUnitPrice'
  | 'SumLineQuantityTimesUnitCost'
  | 'SubtotalMinusDiscount'
  | 'SumShiftVarianceLiability'
  | 'GroupItemAmount'
  | 'Multiply'
  | 'AddFields'
  | 'SubtractFields'
  | 'DivideFields'
  | 'PercentageOf';
export type Condition = 'None' | 'FieldEquals' | 'FieldGreaterThanZero' | 'FieldNotNull';
export type CostCenterSource = 'Fixed' | 'FromDocument' | 'Dynamic' | 'FromRelatedEntity' | 'FromContext';
export type TriggerType = 'Always' | 'HasStockMovement' | 'FieldCondition';

export interface PostingScreenField {
  name: string;
  labelAr: string;
  labelEn: string;
  kind: 'Amount' | 'Id' | 'Account' | 'Text';
  sample: number | null;
  entityType: string;
  isContext: boolean;
  choices: string[] | null;
}
export interface PostingScreenGroup { name: string; labelAr: string; labelEn: string; sample: number }
export interface PostingScreenResolver { key: string; kind: 'Account' | 'CostCenter'; requiredField: string }
export interface PostingRelatedEntity {
  name: string;
  labelAr: string;
  labelEn: string;
  fields: { name: string; labelAr: string; labelEn: string; entityType: string }[];
}
export interface PostingScreenTemplate {
  id: number;
  nameAr: string;
  triggerType: TriggerType;
  triggerFieldName: string | null;
  triggerFieldValue: string | null;
  executionOrder: number;
  versionNumber: number;
  isActive: boolean;
  lineCount: number;
}

export interface PostingScreen {
  screenCode: string;
  nameAr: string;
  nameEn: string;
  sourceModule: string;
  whenAr: string;
  canMoveStock: boolean;
  isPosting: boolean;
  defaultTemplateCount: number;
  fields: PostingScreenField[];
  groups: PostingScreenGroup[];
  resolvers: PostingScreenResolver[];
  relatedEntities: PostingRelatedEntity[];
  templates: PostingScreenTemplate[];
}

export interface CostCenterInput {
  costCenterDimensionId: number;
  sourceType: CostCenterSource;
  fixedValueId: number | null;
  valueFieldName: string | null;
  valueResolverKey: string | null;
  relatedEntityType: string | null;
  relatedEntityField: string | null;
  contextKey: string | null;
  displayOrder: number;
}

export interface TemplateLineInput {
  lineNumber: number;
  direction: Direction;
  accountSourceType: AccountSource;
  fixedAccountId: number | null;
  accountFieldName: string | null;
  accountResolverKey: string | null;
  amountFormulaType: AmountFormula;
  amountFieldName: string | null;
  amountFieldNames: string[] | null;
  amountMultiplier: number | null;
  amountPercentage: number | null;
  conditionType: Condition;
  conditionFieldName: string | null;
  conditionFieldValue: string | null;
  lineDescription: string | null;
  costCenters: CostCenterInput[];
}

export interface TemplateDefinition {
  nameAr: string;
  nameEn: string;
  description: string | null;
  triggerType: TriggerType;
  triggerFieldName: string | null;
  triggerFieldValue: string | null;
  executionOrder: number;
  lines: TemplateLineInput[];
}

export interface TemplateDetail extends TemplateDefinition {
  id: number;
  screenCode: string;
  versionNumber: number;
  isCurrentVersion: boolean;
  isActive: boolean;
  isSystemTemplate: boolean;
}

export interface PreviewLine {
  lineNumber: number;
  direction: Direction;
  accountDisplay: string;
  accountKnownNow: boolean;
  amount: number;
  amountFormula: string | null;
  skipped: boolean;
  note: string | null;
  costCenters: string[];
}

export interface Preview {
  triggerMatched: boolean;
  triggerDescription: string;
  lines: PreviewLine[];
  totalDebit: number;
  totalCredit: number;
  isBalanced: boolean;
  problems: string[];
}

export interface ScreenPreviewItem { templateId: number; nameAr: string; executionOrder: number; isActive: boolean; preview: Preview }

/** A sample document for previews: amounts, coded values (PaymentType), and whether it moved stock. */
export interface PreviewSample { values: Record<string, number>; texts: Record<string, string>; hasStockMovement: boolean }

export interface EngineCatalog { companyAccountRoles: string[] }

export function usePostingScreens() {
  return useQuery({
    queryKey: ['posting-screens'],
    queryFn: async () => (await api.get<PostingScreen[]>(`${BASE}/screens`)).data
  });
}

export function usePostingEngineCatalog() {
  return useQuery({
    queryKey: ['posting-catalog'],
    queryFn: async () => (await api.get<EngineCatalog>(`${BASE}/catalog`)).data,
    staleTime: Infinity
  });
}

export function usePostingTemplate(id: number | null | undefined) {
  return useQuery({
    queryKey: ['posting-template', id],
    queryFn: async () => (await api.get<TemplateDetail>(`${BASE}/${id}`)).data,
    enabled: id != null
  });
}

function useInvalidateScreens() {
  const queryClient = useQueryClient();
  return () => {
    queryClient.invalidateQueries({ queryKey: ['posting-screens'] });
    queryClient.invalidateQueries({ queryKey: ['posting-template'] });
  };
}

export function useCreateDefaultTemplates() {
  const invalidate = useInvalidateScreens();
  return useMutation({
    mutationFn: async (screenCode: string) => (await api.post<{ ids: number[] }>(`${BASE}/defaults`, { screenCode })).data,
    onSuccess: invalidate
  });
}

export function useSetScreenActive() {
  const invalidate = useInvalidateScreens();
  return useMutation({
    mutationFn: async ({ screenCode, isActive }: { screenCode: string; isActive: boolean }) =>
      api.post(`${BASE}/screens/${screenCode}/active`, { isActive }),
    onSuccess: invalidate
  });
}

export function useCreateTemplate() {
  const invalidate = useInvalidateScreens();
  return useMutation({
    mutationFn: async (input: { screenCode: string; definition: TemplateDefinition; isActive: boolean }) =>
      (await api.post<{ id: number }>(BASE, input)).data,
    onSuccess: invalidate
  });
}

export function useUpdateTemplate() {
  const invalidate = useInvalidateScreens();
  return useMutation({
    mutationFn: async ({ id, definition }: { id: number; definition: TemplateDefinition }) =>
      (await api.put<{ id: number; versionNumber: number; isNewVersion: boolean }>(`${BASE}/${id}`, definition)).data,
    onSuccess: invalidate
  });
}

export function useSetTemplateActive() {
  const invalidate = useInvalidateScreens();
  return useMutation({
    mutationFn: async ({ id, isActive }: { id: number; isActive: boolean }) =>
      api.post(`${BASE}/${id}/${isActive ? 'activate' : 'deactivate'}`),
    onSuccess: invalidate
  });
}

export function usePreviewTemplate() {
  return useMutation({
    mutationFn: async (input: { screenCode: string; definition: TemplateDefinition; sample: PreviewSample }) =>
      (await api.post<Preview>(`${BASE}/preview`, input)).data
  });
}

export function usePreviewScreen() {
  return useMutation({
    mutationFn: async (input: { screenCode: string; sample: PreviewSample }) =>
      (await api.post<ScreenPreviewItem[]>(`${BASE}/preview-screen`, input)).data
  });
}
