export interface LoyaltyTierListItem {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  displayOrder: number;
  minPointsThreshold: number;
  earnRateMultiplier: number;
  isActive: boolean;
}

export interface LoyaltyTierDetail {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  displayOrder: number;
  minPointsThreshold: number;
  earnRateMultiplier: number;
  isActive: boolean;
}

export interface LoyaltyTierFormValues {
  code?: string;
  nameAr: string;
  nameEn: string;
  displayOrder: number;
  minPointsThreshold: number;
  earnRateMultiplier: number;
  isActive: boolean;
}
