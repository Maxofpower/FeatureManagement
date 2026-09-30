export interface Order {
    id: number;
    orderNumber: string;
    customerId: number;
    status: string;
    total: number;
    currency: string;
    createdAt: string;
    lineCount: number;
}

export interface OrderPagedResult {
    items: Order[];
    nextCursor: string;
    previousCursor: string;
    hasMore: boolean;
    hasPrevious: boolean;
    totalCount: number;
}

export const ORDER_DEFAULT_LIMIT = "20";
export const ORDER_MAX_LIMIT = 50;

export interface OrderFilterValues {
    limit: string;
}

export interface OrderQueryFilters extends OrderFilterValues {
    cursor: string;
}
