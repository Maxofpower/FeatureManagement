'use client'

import { useRouter } from 'next/navigation'
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationNext,
  PaginationPrevious,
} from '@/components/ui/pagination'
import type { OrderPagedResult, OrderQueryFilters } from '@/models'

interface Props {
  orders: OrderPagedResult
  currentFilters: OrderQueryFilters
}

export const OrdersPagination = ({ orders, currentFilters }: Props) => {
  const router = useRouter()

  const buildHref = (cursor: string) => {
    const params = new URLSearchParams({ limit: currentFilters.limit })
    if (cursor) params.set('cursor', cursor)
    return `/orders?${params.toString()}`
  }

  const canGoPrevious = orders.hasPrevious && !!orders.previousCursor
  const canGoNext = orders.hasMore && !!orders.nextCursor

  const goTo = (href: string, allowed: boolean) => {
    if (!allowed) return
    router.push(href)
  }

  if (!canGoPrevious && !canGoNext) return null

  return (
    <div className="flex flex-col items-center gap-2">
      <Pagination>
        <PaginationContent>
          <PaginationItem>
            <PaginationPrevious
              href={buildHref(orders.previousCursor)}
              onClick={(e) => {
                e.preventDefault()
                goTo(buildHref(orders.previousCursor), canGoPrevious)
              }}
              aria-disabled={!canGoPrevious}
              className={!canGoPrevious ? 'pointer-events-none opacity-50' : ''}
            />
          </PaginationItem>

          <PaginationItem>
            <PaginationNext
              href={buildHref(orders.nextCursor)}
              onClick={(e) => {
                e.preventDefault()
                goTo(buildHref(orders.nextCursor), canGoNext)
              }}
              aria-disabled={!canGoNext}
              className={!canGoNext ? 'pointer-events-none opacity-50' : ''}
            />
          </PaginationItem>
        </PaginationContent>
      </Pagination>
    </div>
  )
}
