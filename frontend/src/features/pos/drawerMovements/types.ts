export type DrawerMovementType = 'Drop' | 'Pickup';
export type DrawerMovementStatus = 'Recorded' | 'Approved' | 'Rejected';

export interface DrawerMovement {
  id: number;
  shiftId: number;
  movementType: DrawerMovementType;
  amount: number;
  reason?: string;
  status: DrawerMovementStatus;
  approvedByUserId?: number;
  approvedAtUtc?: string;
  createdAtUtc: string;
}
