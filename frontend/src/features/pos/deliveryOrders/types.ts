export interface DeliveryOrderItemInput {
  itemId: number;
  quantity: number;
  unitPrice?: number;
}

export interface DeliveryOrderListItem {
  checkId: number;
  checkCode: string;
  status: string;
  isExternalPlatform: boolean;
  platformName: string | null;
  platformOrderId: string | null;
  customerName: string | null;
  customerPhone: string | null;
  deliveryAddress: string | null;
  lineCount: number;
  total: number;
  createdAtUtc: string;
}

export interface ReceiveDeliveryOrderPayload {
  posTerminalId: number;
  platformName: string;
  platformOrderId: string;
  customerName?: string;
  customerPhone?: string;
  deliveryAddress?: string;
  items: DeliveryOrderItemInput[];
}
