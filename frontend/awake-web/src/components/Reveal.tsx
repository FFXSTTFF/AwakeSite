import { useEffect, useRef, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

// Плавное появление блока при попадании во вьюпорт (CSS-классы в index.css)
export function Reveal({
  children,
  className,
  delayMs = 0,
}: {
  children: ReactNode
  className?: string
  delayMs?: number
}) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const el = ref.current
    if (!el) return
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          window.setTimeout(() => el.classList.add('reveal-visible'), delayMs)
          observer.disconnect()
        }
      },
      { threshold: 0.15 },
    )
    observer.observe(el)
    return () => observer.disconnect()
  }, [delayMs])

  return (
    <div ref={ref} className={cn('reveal', className)}>
      {children}
    </div>
  )
}
