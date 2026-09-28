export interface DeliveryOrderListItem {
  id: number;
  deliveryNumber: string;
  deliveryDate: string;
  branchId?: number;
  customerId: number;
  customerNameAr: string;
  warehouseId: number;
  warehouseNameAr: string;
  sourceOrderId?: number;
  sourceInvoiceId?: number;
  lineCount: number;
  status: string;
}

export interface DeliveryOrderLine {
  id?: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  batchNumber?: string;
}

export interface DeliveryOrderLineInput {
  itemId: number;
  quantity: number;
  batchNumber?: string;
}

export interface DeliveryOrderDetail {
  id: number;
  deliveryNumber: string;
  deliveryDate: string;
  branchId?: number;
  customerId: number;
  customerNameAr: string;
  warehouseId: number;
  warehouseNameAr: string;
  sourceOrderId?: number;
  sourceOrderNumber?: string;
  sourceInvoiceId?: number;
  sourceInvoiceNumber?: string;
  status: string;
  rowVersion: string;
  lines: DeliveryOrderLine[];
}

export interface DeliveryOrderFormValues {
  branchId?: number;
  customerId: number;
  warehouseId: number;
  deliveryDate: string;
  sourceOrderId?: number;
  sourceInvoiceId?: number;
  lines: DeliveryOrderLineInput[];
}
