import { fetcher } from "@/lib/fetcher"
import {
  ORDER_DEFAULT_LIMIT,
  ORDER_MAX_LIMIT,
  type OrderPagedResult,
} from "@/models"
import { API_ROUTES } from "@/lib/constants/routes"
import { OrdersPage } from "./_components/orders"

interface Props {
  searchParams: Promise<{
    limit?: string
    cursor?: string
  }>
}

export default async function Orders({ searchParams }: Props) {
  const params = await searchParams

  const rawLimit = Number(params.limit)
  const limit =
    params.limit && Number.isInteger(rawLimit) && rawLimit >= 1 && rawLimit <= ORDER_MAX_LIMIT
      ? params.limit
      : ORDER_DEFAULT_LIMIT
  const cursor = params.cursor ?? ""

  const query = new URLSearchParams({ limit })
  if (cursor) query.set("cursor", cursor)

  const getOrders = await fetcher<OrderPagedResult>(`${API_ROUTES.ORDERS}?${query}`, {
    cache: 'no-store'
  })

  if (!getOrders.success || getOrders.data === undefined) {
    return <div>Error: {getOrders.message}</div>
  }

  return (
    <OrdersPage
      orders={getOrders.data}
      currentFilters={{ limit, cursor }}
    />
  )
}
