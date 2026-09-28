export interface SalesOrderListItem {
  id: number;
  orderNumber: string;
  orderDate: string;
  branchId?: number;
  customerId: number;
  customerNameAr: string;
  sourceQuoteId?: number;
  subtotal: number;
  status: string;
}

export interface SalesOrderLine {
  id?: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  deliveredQuantity: number;
}

export interface SalesOrderLineInput {
  itemId: number;
  quantity: number;
  unitPrice: number;
}

export interface SalesOrderDetail {
  id: number;
  orderNumber: string;
  orderDate: string;
  branchId?: number;
  customerId: number;
  customerNameAr: string;
  sourceQuoteId?: number;
  sourceQuoteNumber?: string;
  subtotal: number;
  status: string;
  rowVersion: string;
  lines: SalesOrderLine[];
}

export interface SalesOrderFormValues {
  branchId?: number;
  customerId: number;
  orderDate: string;
  sourceQuoteId?: number;
  lines: SalesOrderLineInput[];
}

export interface ConfirmedSalesOrder {
  id: number;
  orderNumber: string;
  customerId: number;
  customerNameAr: string;
  subtotal: number;
}
