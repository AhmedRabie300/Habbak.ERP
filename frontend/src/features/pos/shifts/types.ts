export interface DenominationCountInput {
  denominationValue: number;
  count: number;
}

export interface ShiftDenominationCount {
  countType: string;
  denominationValue: number;
  count: number;
}

export interface ShiftListItem {
  id: number;
  posTerminalId: number;
  posTerminalNameAr: string;
  posTerminalNameEn: string;
  cashierUserId: number;
  status: string;
  openedAtUtc: string;
  closedAtUtc?: string;
  openingCashAmount: number;
  actualClosingCashAmount?: number;
  differenceAmount?: number;
}

export interface ShiftDetail {
  id: number;
  rowVersion: string;
  posTerminalId: number;
  posTerminalNameAr: string;
  posTerminalNameEn: string;
  cashierUserId: number;
  status: string;
  openedAtUtc: string;
  closedAtUtc?: string;
  openingCashAmount: number;
  expectedClosingCashAmount?: number;
  actualClosingCashAmount?: number;
  differenceAmount?: number;
  closedByUserId?: number;
  denominationCounts: ShiftDenominationCount[];
}
