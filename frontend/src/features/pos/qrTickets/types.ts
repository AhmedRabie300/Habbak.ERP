export interface QRTicketItemInput {
  itemId: number;
  quantity: number;
  unitPrice: number;
}

export interface QRTicketPreviewLine {
  itemId: number;
  quantity: number;
  unitPrice: number;
}

export interface QRTicketPreview {
  id: number;
  idempotencyKey: string;
  status: string;
  lines: QRTicketPreviewLine[];
}

export interface GeneratedQRTicket {
  id: number;
  idempotencyKey: string;
}
