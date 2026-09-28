export interface BranchPOSSettings {
  branchId: number;
  operationMode: string;
  tipsEnabled: boolean;
  serviceChargeEnabled: boolean;
  serviceChargeRate: number;
  vatEnabled: boolean;
  vatRate: number;
  allowSplitPayment: boolean;
  loyaltyRedemptionEnabledAtPOS: boolean;
  etaReceiptEnabled: boolean;
  cashRoundingIncrement: number;
  maxAllowedShiftCashDifference: number;
  postingMode: 'PerTransaction' | 'PerShift' | 'PerDay';
  shiftVarianceEmployeeLiabilityThreshold: number;
}
