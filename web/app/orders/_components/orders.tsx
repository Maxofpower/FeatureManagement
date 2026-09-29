'use client'

import { Suspense } from "react"
import { Badge } from "@/components/ui/badge"
import { Card } from "@/components/ui/card"
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from "@/components/ui/table"
import type { Order, OrderPagedResult, OrderQueryFilters } from "@/models"
import { ReceiptText } from "lucide-react"
import { OrdersFilters } from "./orders-filters"
import { OrdersPagination } from "./orders-pagination"
import { format } from "date-fns"

interface Props {
    orders: OrderPagedResult
    currentFilters: OrderQueryFilters
}

const STATUS_COLORS: Record<string, string> = {
    Pending: "bg-amber-500/10 text-amber-600 dark:text-amber-400",
    Placed: "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400",
    Cancelled: "bg-destructive/10 text-destructive dark:bg-destructive/20",
    PaymentFailed: "bg-destructive/10 text-destructive dark:bg-destructive/20",
}

const StatusBadge = ({ status }: { status: string }) => (
    <Badge variant="ghost" className={STATUS_COLORS[status] ?? "bg-muted text-muted-foreground"}>
        {status}
    </Badge>
)

export const OrdersPage = (props: Props) => {
    return (
        <div className="bg-background">
            <main className=" px-4 py-8 sm:px-6 lg:px-8 flex flex-col gap-4">
                <div className="flex justify-between items-start gap-4">
                    <div>
                        <h1 className="text-2xl font-bold tracking-tight text-foreground">Orders</h1>
                        <p className="mt-1 text-sm text-muted-foreground">
                            Demo Commerce orders, newest first. Keyset (cursor) paging — Previous/Next pass the opaque cursor back unchanged.
                        </p>
                    </div>
                    <Suspense>
                        <OrdersFilters currentFilters={props.currentFilters} />
                    </Suspense>
                </div>

                {props.orders.totalCount > 0 && (
                    <p className="text-sm text-muted-foreground">
                        {props.orders.totalCount} orders
                    </p>
                )}

                {
                    props.orders.items.length > 0 ?
                        <Card className="py-0">
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead>Order</TableHead>
                                        <TableHead>Customer</TableHead>
                                        <TableHead>Status</TableHead>
                                        <TableHead className="text-right">Items</TableHead>
                                        <TableHead className="text-right">Total</TableHead>
                                        <TableHead className="text-right">Created At</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {props.orders.items.map((order: Order) => (
                                        <TableRow key={order.id}>
                                            <TableCell className="font-medium">{order.orderNumber}</TableCell>
                                            <TableCell>#{order.customerId}</TableCell>
                                            <TableCell><StatusBadge status={order.status} /></TableCell>
                                            <TableCell className="text-right">{order.lineCount}</TableCell>
                                            <TableCell className="text-right">
                                                {new Intl.NumberFormat("en-US", {
                                                    style: "currency",
                                                    currency: order.currency,
                                                }).format(Number(order.total))}
                                            </TableCell>
                                            <TableCell className="text-right text-muted-foreground">
                                               {format(order.createdAt, "MMM d, yyyy")}
                                            </TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        </Card>

                        :
                        <EmptyState />
                }

                {props.orders.items.length > 0 && (
                    <div className="mt-4 flex justify-center">
                        <Suspense>
                            <OrdersPagination
                                orders={props.orders}
                                currentFilters={props.currentFilters}
                            />
                        </Suspense>
                    </div>
                )}
            </main>
        </div>
    )
}

function EmptyState() {
    return (
        <Card className="flex flex-col items-center justify-center gap-3 p-12 text-center">
            <ReceiptText className="mb-1 size-8 text-tab" />
            <p className="text-sm font-medium text-foreground">No orders found</p>
        </Card>
    )
}
