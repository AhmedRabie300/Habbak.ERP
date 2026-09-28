export interface SupplierEvaluationListItem {
  id: number;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  evaluationDate: string;
  overallScore: number;
}

export interface SupplierEvaluationDetail {
  id: number;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  evaluationDate: string;
  qualityScore: number;
  deliveryTimeScore: number;
  quantityComplianceScore: number;
  overallScore: number;
  notes?: string;
  rowVersion: string;
}

export interface SupplierEvaluationFormValues {
  supplierId: number;
  evaluationDate: string;
  qualityScore: number;
  deliveryTimeScore: number;
  quantityComplianceScore: number;
  notes?: string;
}
