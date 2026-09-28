export interface POSPaymentMethodConfig {
  id: number;
  posTerminalId: number;
  paymentMethodId: number;
  paymentMethodNameAr: string;
  paymentMethodNameEn: string;
  isEnabled: boolean;
  linkedTreasuryAccountId: number;
  linkedTreasuryAccountNameAr: string;
}
